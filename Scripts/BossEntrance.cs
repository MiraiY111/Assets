using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Miniboss berjalan masuk otomatis melewati daftar titik, lalu baru mulai bertarung.
/// Dipanggil WaveSpawner tepat setelah miniboss diaktifkan.
/// Selama berjalan: AI boss dimatikan (BossAttackController, StationaryBoss, EnemyAI) dan semua collider-nya
/// dimatikan, jadi boss menembus tembok dan TIDAK bisa dilukai. Semuanya dinyalakan lagi di titik terakhir.
/// </summary>
public class BossEntrance : MonoBehaviour
{
    [Header("Jalur masuk")]
    [Tooltip("Boss dipindahkan ke sini saat aktif (letakkan di luar layar/pintu). Kosong = mulai dari posisinya di scene.")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("Titik tujuan, urut dari atas ke bawah. Titik terakhir = tempat boss mulai bertarung. Klik + untuk menambah.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private float arriveDistance = 0.1f;
    [Tooltip("Batas waktu per titik (detik). Kalau boss macet, dia dipindahkan ke titik itu supaya game tidak macet.")]
    [SerializeField] private float timeoutPerPoint = 8f;

    [Header("Visual")]
    [Tooltip("Centang kalau sprite boss aslinya menghadap KIRI. Samakan dengan 'Sprite Faces Left' di script boss.")]
    [SerializeField] private bool spriteFacesLeft = false;
    [Tooltip("Parameter bool Animator untuk animasi jalan. Kosongkan atau biarkan kalau boss tidak punya (dilewati diam-diam).")]
    [SerializeField] private string walkParam = "isWalking";
    // [DRAGON]
    [Tooltip("Parameter bool Animator untuk animasi TERBANG selama jalan masuk (mis. isFlying). Kalau diisi dan ada di Animator, " +
             "boss terbang (bukan jalan) saat masuk, lalu otomatis kembali normal begitu sampai di titik terakhir. " +
             "Kosongkan untuk boss yang berjalan masuk seperti biasa.")]
    [SerializeField] private string flyParam = "";

    [Header("Tembus tembok")]
    [Tooltip("Aktif: collider boss dimatikan selama berjalan (menembus tembok, tidak bisa dilukai). " +
             "Titik terakhir HARUS jelas di lantai dan lega, karena boss besar.")]
    [SerializeField] private bool ignoreWallsWhileWalking = true;

    public bool IsPlaying { get; private set; }

    GameObject target;
    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    bool hasWalkParam;
    bool hasFlyParam;   // [DRAGON]

    Behaviour[] brains;
    bool[] brainWasEnabled;
    Collider2D[] bodyColliders;
    bool[] colliderWasEnabled;
    bool locked;
    Coroutine routine;

    /// <summary>Mulai jalan masuk untuk 'boss' (yang sudah aktif). Tunggu sampai IsPlaying menjadi false.</summary>
    public void Play(GameObject boss)
    {
        if (IsPlaying || boss == null) return;

        rb = boss.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError($"{name}: miniboss '{boss.name}' tidak punya Rigidbody2D di objek paling atas, jalan masuk dilewati.");
            return;
        }

        if (waypoints.Count == 0)
        {
            Debug.LogWarning($"{name}: Waypoints kosong, miniboss langsung bertarung.");
            return;
        }

        target = boss;
        anim = boss.GetComponentInChildren<Animator>();
        sr = boss.GetComponentInChildren<SpriteRenderer>();
        CacheWalkParam();

        var list = new List<Behaviour>();
        list.AddRange(boss.GetComponents<BossAttackController>());
        list.AddRange(boss.GetComponents<StationaryBoss>());
        list.AddRange(boss.GetComponents<EnemyAI>());
        brains = list.ToArray();
        brainWasEnabled = new bool[brains.Length];

        bodyColliders = boss.GetComponentsInChildren<Collider2D>();
        colliderWasEnabled = new bool[bodyColliders.Length];

        if (brains.Length == 0)
            Debug.LogWarning($"{name}: '{boss.name}' tidak punya BossAttackController/StationaryBoss/EnemyAI. " +
                             "Kalau boss memakai script AI lain, script itu tetap jalan dan bisa mengganggu jalan masuk.");

        WarnIfLastPointBlocked();

        SetLocked(true);
        if (startPoint != null) Teleport(startPoint.position);

        IsPlaying = true;
        routine = StartCoroutine(Run());
    }

    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        Finish();
    }

    IEnumerator Run()
    {
        foreach (var wp in waypoints)
        {
            if (wp == null) continue;

            float elapsed = 0f;
            while (true)
            {
                if (target == null || rb == null) { Finish(); yield break; }

                Vector2 to = (Vector2)wp.position - rb.position;
                float step = walkSpeed * Time.fixedDeltaTime;
                if (to.magnitude <= Mathf.Max(arriveDistance, step)) break;

                if (elapsed > timeoutPerPoint)
                {
                    Debug.LogWarning($"{name}: miniboss macet menuju '{wp.name}', dipindahkan ke titik itu.");
                    Teleport(wp.position);
                    break;
                }

                rb.linearVelocity = to.normalized * walkSpeed;
                Face(to.x);
                SetWalking(true);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        Finish();
    }

    void Finish()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetWalking(false);
        SetLocked(false);
        IsPlaying = false;
        routine = null;
    }

    // matikan/nyalakan AI dan collider boss; keadaan awal tiap komponen disimpan lalu dikembalikan apa adanya
    void SetLocked(bool on)
    {
        if (on == locked) return;
        locked = on;

        if (brains != null)
            for (int i = 0; i < brains.Length; i++)
            {
                var b = brains[i];
                if (b == null) continue;
                if (on) { brainWasEnabled[i] = b.enabled; b.enabled = false; }
                else b.enabled = brainWasEnabled[i];
            }

        if (ignoreWallsWhileWalking && bodyColliders != null)
            for (int i = 0; i < bodyColliders.Length; i++)
            {
                var c = bodyColliders[i];
                if (c == null) continue;
                if (on) { colliderWasEnabled[i] = c.enabled; c.enabled = false; }
                else c.enabled = colliderWasEnabled[i];
            }

        if (on && rb != null) rb.linearVelocity = Vector2.zero;
    }

    void Teleport(Vector3 p)
    {
        if (target == null) return;
        var pos = new Vector3(p.x, p.y, target.transform.position.z);
        target.transform.position = pos;
        if (rb != null) rb.position = pos;
    }

    // sama dengan script boss: flipX = true kalau perlu menghadap kiri (untuk sprite yang aslinya menghadap kanan)
    void Face(float dx)
    {
        if (sr == null || Mathf.Abs(dx) < 0.01f) return;
        sr.flipX = spriteFacesLeft ? dx > 0f : dx < 0f;
    }

    void CacheWalkParam()
    {
        hasWalkParam = false;
        hasFlyParam = false;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (var p in anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (!string.IsNullOrEmpty(walkParam) && p.name == walkParam) hasWalkParam = true;
            if (!string.IsNullOrEmpty(flyParam) && p.name == flyParam) hasFlyParam = true;
        }
    }

    // [DRAGON] kalau ada parameter terbang: selama masuk boss TERBANG (jalan dimatikan); selesai masuk keduanya mati
    // dan BossAttackController yang menyalakan isWalking lagi saat bertarung
    void SetWalking(bool on)
    {
        if (anim == null) return;
        if (hasFlyParam)
        {
            anim.SetBool(flyParam, on);
            if (hasWalkParam) anim.SetBool(walkParam, false);
        }
        else if (hasWalkParam) anim.SetBool(walkParam, on);
    }

    // PathGrid dihitung untuk monster kecil; boss lebih besar, jadi peringatan ini hanya batas minimum
    void WarnIfLastPointBlocked()
    {
        if (!ignoreWallsWhileWalking || PathGrid.Instance == null) return;

        for (int i = waypoints.Count - 1; i >= 0; i--)
        {
            if (waypoints[i] == null) continue;
            if (!PathGrid.Instance.IsWalkableAt(waypoints[i].position))
                Debug.LogWarning($"{name}: titik terakhir '{waypoints[i].name}' tidak berada di lantai yang aman " +
                                 "(dekat tembok atau di luar area). Boss bisa terjepit saat collider dinyalakan lagi.");
            return;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        bool hasPrev = false;
        Vector3 prev = default;

        if (startPoint != null)
        {
            Gizmos.DrawWireCube(startPoint.position, Vector3.one * 0.4f);
            prev = startPoint.position;
            hasPrev = true;
        }

        foreach (var wp in waypoints)
        {
            if (wp == null) continue;
            Gizmos.DrawWireSphere(wp.position, 0.25f);
            if (hasPrev) Gizmos.DrawLine(prev, wp.position);
            prev = wp.position;
            hasPrev = true;
        }
    }
}