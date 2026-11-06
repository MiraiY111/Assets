using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemySpawnOption
{
    [Tooltip("Seret PREFAB dari Project window, BUKAN objek dari Hierarchy.")]
    public GameObject prefab;
    [Tooltip("Makin besar, makin sering dipilih. 1 = normal.")]
    [Min(1)] public int weight = 1;
}

/// <summary>
/// Alur satu arena:
/// 1) Player berjalan masuk otomatis (opsional, PlayerEntrance)
/// 2) Beberapa wave monster; wave berikutnya mulai setelah SEMUA monster wave ini mati
/// 3) Miniboss: Player masuk zona (setelah wave habis) -> kamera geser ke ujung kanan -> boss muncul dan
///    berjalan masuk (opsional, BossEntrance) -> kamera kembali ke Player -> bertarung
/// 4) Setelah boss kalah: Player berjalan sendiri ke zona keluar (opsional) -> Player berjalan keluar otomatis
///    (opsional, PlayerExit) -> OnArenaCompleted (game selesai)
/// Posisi spawn monster ditentukan EnemySpawnZones.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public enum Phase { Idle, Entrance, Waves, WaitingMiniboss, MinibossEntrance, Miniboss, WaitingExit, Exit, Done }

    [SerializeField] private EnemySpawnZones zones;

    [Header("Player masuk (opsional)")]
    [Tooltip("Kosong = Player langsung bisa digerakkan, tanpa jalan masuk otomatis.")]
    [SerializeField] private PlayerEntrance entrance;

    [Header("Pengaturan wave")]
    [Tooltip("Jumlah monster yang muncul di tiap wave.")]
    [Min(1)][SerializeField] private int monstersPerWave = 3;
    [Tooltip("Berapa kali wave muncul sebelum lanjut ke miniboss / selesai.")]
    [Min(1)][SerializeField] private int totalWaves = 3;
    [Tooltip("Jeda antar monster saat satu wave sedang di-spawn (detik).")]
    [SerializeField] private float spawnDelay = 0.4f;
    [Tooltip("Jeda setelah wave habis sebelum wave berikutnya mulai (detik).")]
    [SerializeField] private float delayBetweenWaves = 1.5f;

    [Header("Jenis monster (klik + untuk menambah)")]
    [SerializeField] private List<EnemySpawnOption> enemies = new List<EnemySpawnOption>();

    [Header("Kalau posisi spawn tidak ketemu")]
    [Tooltip("Berapa kali mencoba lagi untuk satu monster sebelum dilewati.")]
    [SerializeField] private int retriesPerEnemy = 5;
    [SerializeField] private float retryDelay = 0.5f;

    [Header("Miniboss (opsional)")]
    [Tooltip("Objek miniboss di Hierarchy (dimatikan sampai waktunya), atau Prefab (di-clone saat waktunya). Kosong = tanpa miniboss.")]
    [SerializeField] private GameObject miniboss;
    [Tooltip("BoxCollider2D sebagai penanda zona. Miniboss muncul saat Player ada di dalamnya. Kosong = muncul langsung setelah wave habis.")]
    [SerializeField] private BoxCollider2D minibossZone;
    [Tooltip("Jalan masuk miniboss. Kosong = miniboss langsung muncul di posisinya di scene dan bertarung.")]
    [SerializeField] private BossEntrance minibossEntrance;
    [Tooltip("Bar HP miniboss di layar (objek BossBar dengan BossHealthBarUI). Kosong = tanpa bar.")]
    [SerializeField] private BossHealthBarUI bossBar;
    [Tooltip("Kosong = dicari otomatis")]
    [SerializeField] private Transform player;

    [Header("Kamera saat miniboss muncul (opsional)")]
    [Tooltip("Kosong = diambil dari Main Camera. Kalau tidak ada, kamera tidak digeser.")]
    [SerializeField] private CameraFollow cameraFollow;
    [Tooltip("Player dibekukan selama kamera bergeser dan boss berjalan masuk. Butuh field Entrance terisi.")]
    [SerializeField] private bool freezePlayerDuringBossIntro = true;
    [Tooltip("Jeda setelah kamera sampai di ujung kanan, sebelum boss muncul (detik).")]
    [SerializeField] private float pauseBeforeBoss = 0.5f;
    [Tooltip("Jeda setelah boss selesai berjalan masuk, sebelum kamera kembali ke Player (detik).")]
    [SerializeField] private float holdAfterBossEntrance = 1f;
    [Tooltip("Batas menunggu kamera sampai di ujung kanan (detik), supaya alur tidak macet.")]
    [SerializeField] private float cameraArriveTimeout = 5f;

    [Header("Player keluar setelah selesai (opsional)")]
    [Tooltip("Kosong = tidak ada jalan keluar, alur langsung selesai setelah boss kalah.")]
    [SerializeField] private PlayerExit playerExit;
    [Tooltip("BoxCollider2D sebagai penanda zona keluar (taruh di depan pintu keluar). Player harus berjalan sendiri ke dalam zona ini " +
             "dulu, baru jalan keluar otomatis dimulai. Kosong = jalan keluar langsung dimulai setelah jeda.")]
    [SerializeField] private BoxCollider2D exitZone;
    [Tooltip("Jeda setelah boss mati (dan setelah Player masuk zona keluar, kalau ada) sebelum Player mulai berjalan keluar (detik).")]
    [SerializeField] private float delayBeforeExit = 2f;

    [Header("Mulai")]
    [Tooltip("Aktif: alur mulai otomatis saat Play. Matikan kalau dimulai dari sistem lain (panggil StartWaves()).")]
    [SerializeField] private bool autoStartOnPlay = true;
    [SerializeField] private float autoStartDelay = 1f;

    [Header("Debug")]
    [Tooltip("Cetak perpindahan fase ke Console.")]
    [SerializeField] private bool debugLog = true;

    public bool IsRunning { get; private set; }
    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    /// <summary>Nomor wave yang sedang berjalan, mulai dari 1. 0 = belum mulai.</summary>
    public int CurrentWave { get; private set; }
    public int TotalWaves => totalWaves;
    public int AliveCount => active.Count;

    public event Action<int> OnWaveStarted;     // nomor wave (mulai 1)
    public event Action<int> OnWaveCleared;     // nomor wave (mulai 1)
    public event Action OnAllWavesCleared;      // wave terakhir habis (sebelum miniboss)
    public event Action OnMinibossSpawned;      // miniboss aktif (jalan masuk belum tentu selesai)
    public event Action OnMinibossDefeated;     // tanpa miniboss: tidak pernah dipanggil
    public event Action OnArenaCompleted;       // Player sudah keluar (atau tanpa PlayerExit: boss/wave habis) = game selesai

    readonly List<EnemyHealth> active = new List<EnemyHealth>();
    PlayerHealth playerHealth;

    // objek di Hierarchy (scene) bukan prefab; objek itu bisa mati/hancur dan membawa kondisi runtime
    static bool IsPrefabAsset(GameObject g) => g != null && !g.scene.IsValid();

    void OnValidate()
    {
        if (enemies == null) return;
        for (int i = 0; i < enemies.Count; i++)
        {
            var p = enemies[i] != null ? enemies[i].prefab : null;
            if (p != null && p.scene.IsValid())
                Debug.LogWarning($"{name}: Enemies[{i}] ('{p.name}') adalah objek Hierarchy, bukan Prefab. " +
                                 "Seret objek itu ke Project window dulu untuk membuat Prefab, lalu pakai Prefab-nya.", this);
        }
    }

    void Awake()
    {
        // miniboss tidak boleh aktif sebelum waktunya; jangan sentuh kalau yang dimasukkan Prefab (asset)
        if (miniboss != null && miniboss.scene.IsValid()) miniboss.SetActive(false);

        // collider zona hanya untuk menggambar kotak di Scene view; kotaknya dihitung sendiri
        if (minibossZone != null) minibossZone.enabled = false;
        if (exitZone != null) exitZone.enabled = false;
    }

    void Start()
    {
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (player != null) playerHealth = player.GetComponentInParent<PlayerHealth>();

        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        if (autoStartOnPlay) StartCoroutine(AutoStart());
    }

    IEnumerator AutoStart()
    {
        // kunci Player dari awal, supaya tidak bisa digerakkan selama jeda sebelum jalan masuk
        if (entrance != null) entrance.LockPlayer();
        yield return new WaitForSeconds(autoStartDelay);
        StartWaves();
    }

    public void StartWaves()
    {
        if (IsRunning)
        {
            Debug.LogWarning($"{name}: alur sudah berjalan.");
            return;
        }
        if (zones == null)
        {
            Debug.LogError($"{name}: field Zones belum diisi.");
            return;
        }
        if (!ValidateEnemies())
        {
            Debug.LogError($"{name}: tidak ada monster valid di daftar Enemies, alur tidak dimulai.");
            return;
        }

        StartCoroutine(MainLoop());
    }

    public void StopWaves()
    {
        StopAllCoroutines();
        if (entrance != null) entrance.Stop();
        if (minibossEntrance != null) minibossEntrance.Stop();
        if (playerExit != null) playerExit.Stop();
        if (cameraFollow != null) cameraFollow.ReleaseFocus();
        IsRunning = false;
        SetPhase(Phase.Idle);
    }

    void SetPhase(Phase p)
    {
        CurrentPhase = p;
        if (debugLog) Debug.Log($"[WaveSpawner] fase: {p}");
    }

    bool ValidateEnemies()
    {
        bool anyValid = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            var p = enemies[i] != null ? enemies[i].prefab : null;
            if (p == null)
                Debug.LogError($"{name}: Enemies[{i}] kosong, atau objeknya sudah dihancurkan " +
                               "(biasanya karena yang dimasukkan objek Hierarchy, bukan Prefab).", this);
            else if (!IsPrefabAsset(p))
                Debug.LogError($"{name}: Enemies[{i}] ('{p.name}') adalah objek Hierarchy, bukan Prefab. Diabaikan.", this);
            else
                anyValid = true;
        }
        return anyValid;
    }

    bool PlayerDead() => playerHealth != null && playerHealth.IsDead;

    IEnumerator MainLoop()
    {
        IsRunning = true;

        // ---- 1) Player masuk ----
        if (entrance != null)
        {
            SetPhase(Phase.Entrance);
            entrance.Play();
            while (entrance.IsPlaying) yield return null;

            if (entrance.Aborted)
            {
                IsRunning = false;
                SetPhase(Phase.Idle);
                yield break;
            }
        }

        // tunggu satu frame supaya PathGrid.Start() pasti sudah selesai
        yield return null;

        // ---- 2) Wave ----
        SetPhase(Phase.Waves);
        for (int w = 1; w <= totalWaves; w++)
        {
            CurrentWave = w;
            zones.ClearHistory();
            active.Clear();
            OnWaveStarted?.Invoke(w);

            for (int i = 0; i < monstersPerWave; i++)
            {
                yield return StartCoroutine(SpawnOne());
                yield return new WaitForSeconds(spawnDelay);
            }

            if (active.Count == 0)
                Debug.LogWarning($"{name}: wave {w} tidak berhasil memunculkan monster sama sekali.");

            // tunggu semua monster wave ini mati (atau hilang dari scene)
            while (true)
            {
                active.RemoveAll(h => h == null || h.IsDead);
                if (active.Count == 0) break;
                yield return null;
            }

            OnWaveCleared?.Invoke(w);

            if (w < totalWaves) yield return new WaitForSeconds(delayBetweenWaves);
        }

        Debug.Log("Semua wave selesai.");
        OnAllWavesCleared?.Invoke();

        // ---- 3) Miniboss ----
        if (miniboss != null) yield return StartCoroutine(MinibossStage());

        // ---- 4) Player keluar ----
        if (playerExit != null)
        {
            // 4a) Player berjalan sendiri ke zona keluar (kontrol masih di tangan pemain)
            if (exitZone != null)
            {
                SetPhase(Phase.WaitingExit);
                while (!PlayerInRect(exitZone))
                {
                    if (PlayerDead())
                    {
                        // Player mati sebelum sampai zona keluar: bukan kemenangan
                        IsRunning = false;
                        SetPhase(Phase.Idle);
                        yield break;
                    }
                    yield return null;
                }
            }

            // 4b) jalan keluar otomatis
            SetPhase(Phase.Exit);
            yield return new WaitForSeconds(delayBeforeExit);

            playerExit.Play();
            while (playerExit.IsPlaying) yield return null;

            if (playerExit.Aborted)
            {
                // Player mati sebelum sempat keluar: bukan kemenangan
                IsRunning = false;
                SetPhase(Phase.Idle);
                yield break;
            }
        }

        IsRunning = false;
        SetPhase(Phase.Done);
        OnArenaCompleted?.Invoke();
    }

    IEnumerator MinibossStage()
    {
        if (minibossZone != null)
        {
            SetPhase(Phase.WaitingMiniboss);
            while (!PlayerInRect(minibossZone)) yield return null;
        }

        bool hasIntro = cameraFollow != null || minibossEntrance != null;
        if (hasIntro) SetPhase(Phase.MinibossEntrance);

        // 1) bekukan Player, kamera geser ke ujung kanan (tetap di dalam batas map)
        bool froze = freezePlayerDuringBossIntro && entrance != null;
        if (froze) entrance.LockPlayer();

        if (cameraFollow != null)
        {
            cameraFollow.FocusRightEdge();
            float waited = 0f;
            while (!cameraFollow.IsAtFocus && waited < cameraArriveTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (waited >= cameraArriveTimeout)
                Debug.LogWarning($"{name}: kamera tidak sampai di ujung kanan dalam {cameraArriveTimeout} detik, boss tetap dimunculkan.");

            if (pauseBeforeBoss > 0f) yield return new WaitForSeconds(pauseBeforeBoss);
        }

        // 2) boss muncul; jalan masuk dimulai di frame yang sama, sebelum AI boss sempat jalan
        GameObject boss = miniboss.scene.IsValid()
            ? miniboss
            : Instantiate(miniboss, miniboss.transform.position, miniboss.transform.rotation);
        boss.SetActive(true);
        OnMinibossSpawned?.Invoke();
        if (bossBar != null) bossBar.Bind(boss.GetComponent<EnemyHealth>());

        if (minibossEntrance != null)
        {
            minibossEntrance.Play(boss);
            // bar HP (yang tadi kosong) terisi selama boss berjalan masuk
            if (bossBar != null) bossBar.FillUp();
            while (minibossEntrance.IsPlaying) yield return null;
        }
        else if (bossBar != null)
        {
            bossBar.FillUp();   // tanpa jalan masuk: terisi langsung
        }

        // 3) tahan sebentar, lalu kamera kembali ke Player dan Player bebas lagi
        if (cameraFollow != null && holdAfterBossEntrance > 0f)
            yield return new WaitForSeconds(holdAfterBossEntrance);
        if (cameraFollow != null) cameraFollow.ReleaseFocus();
        if (froze) entrance.Release();

        SetPhase(Phase.Miniboss);

        var bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth == null)
        {
            Debug.LogWarning($"{name}: miniboss '{boss.name}' tidak punya EnemyHealth di objek paling atas, " +
                             "kematiannya tidak bisa dipantau dan Player tidak bisa melukainya.");
            yield break;
        }

        while (bossHealth != null && !bossHealth.IsDead) yield return null;

        Debug.Log("Miniboss dikalahkan.");
        OnMinibossDefeated?.Invoke();
    }

    bool PlayerInRect(BoxCollider2D zone)
    {
        if (player == null || zone == null) return false;
        return GetRect(zone).Contains((Vector2)player.position);
    }

    // kotak dunia dari BoxCollider2D (rotasi diabaikan)
    static Rect GetRect(BoxCollider2D b)
    {
        Vector3 c = b.transform.TransformPoint(b.offset);
        Vector3 s = b.transform.lossyScale;
        Vector2 size = new Vector2(b.size.x * Mathf.Abs(s.x), b.size.y * Mathf.Abs(s.y));
        return new Rect((Vector2)c - size * 0.5f, size);
    }

    GameObject PickPrefab()
    {
        int total = 0;
        foreach (var e in enemies)
            if (e != null && IsPrefabAsset(e.prefab)) total += Mathf.Max(1, e.weight);

        if (total <= 0)
        {
            Debug.LogError($"{name}: tidak ada Prefab valid untuk di-spawn.");
            return null;
        }

        int roll = UnityEngine.Random.Range(0, total);
        foreach (var e in enemies)
        {
            if (e == null || !IsPrefabAsset(e.prefab)) continue;
            roll -= Mathf.Max(1, e.weight);
            if (roll < 0) return e.prefab;
        }
        return null;
    }

    IEnumerator SpawnOne()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) yield break;

        for (int attempt = 0; attempt <= retriesPerEnemy; attempt++)
        {
            if (zones.TryGetSpawnPosition(out Vector2 pos))
            {
                var go = Instantiate(prefab, pos, Quaternion.identity);
                var health = go.GetComponent<EnemyHealth>();
                if (health == null)
                {
                    Debug.LogError($"{prefab.name}: tidak punya EnemyHealth di objek paling atas, tidak bisa dihitung untuk wave.");
                    Destroy(go);
                    yield break;
                }
                active.Add(health);
                yield break;
            }

            yield return new WaitForSeconds(retryDelay);
        }

        Debug.LogWarning($"{name}: gagal spawn {prefab.name} setelah {retriesPerEnemy + 1} percobaan, dilewati.");
    }

    void OnDrawGizmos()
    {
        DrawZoneGizmo(minibossZone, new Color(0.7f, 0.2f, 1f));   // ungu = zona miniboss
        DrawZoneGizmo(exitZone, new Color(0.2f, 1f, 0.4f));       // hijau terang = zona keluar
    }

    static void DrawZoneGizmo(BoxCollider2D z, Color c)
    {
        if (z == null) return;
        Rect r = GetRect(z);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.2f);
        Gizmos.DrawCube(r.center, r.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 1f);
        Gizmos.DrawWireCube(r.center, r.size);
    }
}