using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tembok arena boss. Pasang di objek Tilemap "Wall(BossMode)" (yang sudah punya TilemapCollider2D).
///
/// Awal game: tembok tersembunyi dan tidak padat (Player bisa lewat).
/// Saat miniboss muncul (WaveSpawner.OnMinibossSpawned): tembok memudar muncul dan langsung padat,
/// jadi Player tidak bisa keluar dari area boss.
/// Setelah miniboss kalah (WaveSpawner.OnMinibossDefeated): tembok hilang lagi, kalau 'Open When Boss Defeated' aktif.
///
/// JANGAN menonaktifkan GameObject-nya di Hierarchy: script inilah yang menyembunyikannya, dan script di objek
/// nonaktif tidak berjalan.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class BossArenaWall : MonoBehaviour
{
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private WaveSpawner spawner;
    [Tooltip("Lama tembok memudar saat muncul/hilang (detik). 0 = langsung.")]
    [SerializeField] private float fadeTime = 0.35f;
    [Tooltip("Aktif: tembok hilang lagi setelah miniboss kalah, supaya jalan keluar tidak terhalang.")]
    [SerializeField] private bool openWhenBossDefeated = true;

    Tilemap map;
    TilemapRenderer rend;
    Collider2D[] cols;
    Color baseColor;
    Coroutine fade;

    void Awake()
    {
        map = GetComponent<Tilemap>();
        rend = GetComponent<TilemapRenderer>();
        cols = GetComponents<Collider2D>();
        baseColor = map.color;

        // mulai dalam keadaan terbuka: tidak terlihat, tidak padat
        SetColliders(false);
        if (rend != null) rend.enabled = false;
    }

    void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning($"{name}: WaveSpawner tidak ditemukan, tembok tidak akan muncul.");
            return;
        }

        spawner.OnMinibossSpawned += Close;
        spawner.OnMinibossDefeated += OnBossDefeated;
    }

    void OnDestroy()
    {
        if (spawner == null) return;
        spawner.OnMinibossSpawned -= Close;
        spawner.OnMinibossDefeated -= OnBossDefeated;
    }

    /// <summary>Tembok muncul dan padat.</summary>
    public void Close()
    {
        SetColliders(true);
        WarnIfPlayerInside();
        if (rend != null) rend.enabled = true;
        StartFade(0f, baseColor.a, false);
    }

    /// <summary>Tembok tidak padat lagi dan memudar hilang.</summary>
    public void Open()
    {
        SetColliders(false);
        StartFade(map.color.a, 0f, true);
    }

    void OnBossDefeated()
    {
        if (openWhenBossDefeated) Open();
    }

    void SetColliders(bool on)
    {
        if (cols == null) return;
        foreach (var c in cols) if (c != null) c.enabled = on;
    }

    void StartFade(float from, float to, bool hideAtEnd)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(from, to, hideAtEnd));
    }

    IEnumerator FadeRoutine(float from, float to, bool hideAtEnd)
    {
        Color c = baseColor;

        if (fadeTime > 0f)
        {
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                c.a = Mathf.Lerp(from, to, t / fadeTime);
                map.color = c;
                yield return null;
            }
        }

        c.a = to;
        map.color = c;
        if (hideAtEnd && rend != null) rend.enabled = false;
        fade = null;
    }

    // Player dibekukan selama intro boss; kalau dia berdiri di tempat tembok, dia akan terjepit.
    // Letakkan tembok di luar zona miniboss dengan jarak sedikit dari tepinya.
    void WarnIfPlayerInside()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        var pcol = pc.GetComponentInChildren<Collider2D>();
        if (pcol == null) return;

        foreach (var c in cols)
        {
            if (c == null || !c.enabled) continue;
            if (c.bounds.Intersects(pcol.bounds))
            {
                Debug.LogWarning($"{name}: Player mungkin berada di dalam tembok saat tembok muncul. " +
                                 "Geser tembok sedikit keluar dari Minibosszone, jauh dari tepi tempat Player masuk zona.");
                return;
            }
        }
    }
}