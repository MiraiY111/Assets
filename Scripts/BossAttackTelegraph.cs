using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Indikator serangan boss: area merah di tanah yang SAMA PERSIS dengan area damage serangan.
/// Pasang di objek boss (satu objek dengan BossAttackController). Tidak dipasang = tanpa indikator.
///
/// Cara kerja (dipanggil otomatis oleh BossAttackController, tidak perlu diatur manual):
///   Begin  : area elips muncul di tanah (garis tepi + isi tipis). Isi merah membesar dari tengah menuju tepi,
///            selesai tepat di momen damage dihitung. Beberapa saat terakhir area berkedip.
///   Hit    : kilat singkat lalu hilang.
///   Cancel : langsung hilang (boss mati).
/// Ukuran dan posisi diambil dari nilai yang dipakai untuk damage (rangeSize x hitScale, rangeOffset, arah hadap),
/// jadi kalau kamu mengubah area serangan di Inspector, indikatornya ikut berubah dan tidak pernah beda dari hitbox.
/// Animasi memakai waktu normal (Time.deltaTime): ikut berhenti saat game di-pause.
/// </summary>
public class BossAttackTelegraph : MonoBehaviour
{
    [Header("Warna")]
    [Tooltip("Garis tepi area.")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.2f, 0.15f, 0.95f);
    [Tooltip("Area yang belum terisi (tipis).")]
    [SerializeField] private Color areaColor = new Color(1f, 0.1f, 0.1f, 0.15f);
    [Tooltip("Isi yang membesar dari tengah menuju tepi.")]
    [SerializeField] private Color fillColor = new Color(1f, 0.2f, 0.15f, 0.4f);
    [Tooltip("Kilat singkat saat damage dihitung.")]
    [SerializeField] private Color hitColor = new Color(1f, 0.95f, 0.85f, 0.9f);

    [Header("Bentuk")]
    [Tooltip("Piksel per satuan Unity untuk gambar area. Samakan dengan Pixels Per Unit sprite game-mu agar pikselnya seragam.")]
    [SerializeField] private float pixelsPerUnit = 32f;
    [Tooltip("Tebal garis tepi (piksel).")]
    [SerializeField, Min(1)] private int outlineThickness = 2;

    [Header("Animasi")]
    [Tooltip("Lama area memudar masuk saat muncul (detik).")]
    [SerializeField] private float fadeInTime = 0.12f;
    [Tooltip("Lama kilat saat damage dihitung (detik).")]
    [SerializeField] private float hitFlashTime = 0.15f;
    [Tooltip("Pada sekian detik terakhir sebelum damage, area berkedip sebagai peringatan terakhir. 0 = tanpa kedip.")]
    [SerializeField] private float blinkLastSeconds = 0.25f;

    [Header("Urutan gambar")]
    [Tooltip("Dibanding sprite boss. Negatif = di bawah boss (di tanah). Isi merah memakai urutan 1 di atas garis tepi, " +
             "jadi pakai -2 atau lebih kecil supaya tetap di bawah boss. Kalau area tertutup lantai, naikkan sedikit; " +
             "kalau menutupi Player atau boss, turunkan.")]
    [SerializeField] private int sortingOffset = -2;

    enum State { Idle, Charging, Flash }

    State state = State.Idle;
    SpriteRenderer bossSr;
    GameObject root;
    Transform areaTf, fillTf;
    SpriteRenderer areaSr, fillSr;
    Func<Vector2> center;
    Vector3 baseScale = Vector3.one;
    float t, dur, flashT;

    readonly Dictionary<long, Sprite[]> cache = new Dictionary<long, Sprite[]>();
    readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    void Awake()
    {
        bossSr = GetComponentInChildren<SpriteRenderer>();
    }

    void OnDisable()
    {
        Hide();
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
        foreach (var o in created)
            if (o != null) Destroy(o);
        created.Clear();
        cache.Clear();
    }

    // ---------- dipanggil BossAttackController ----------

    /// <param name="centerFunc">Pusat area saat ini (dipanggil tiap frame, jadi mengikuti boss).</param>
    /// <param name="size">Lebar dan tinggi elips (satuan Unity), sama dengan area yang dipakai untuk damage.</param>
    /// <param name="duration">Detik dari sekarang sampai damage dihitung.</param>
    public void Begin(Func<Vector2> centerFunc, Vector2 size, float duration)
    {
        if (!isActiveAndEnabled || centerFunc == null || size.x <= 0f || size.y <= 0f) return;

        Build();
        center = centerFunc;

        GetSprites(size, out Sprite areaSprite, out Sprite fillSprite);
        areaSr.sprite = areaSprite;
        fillSr.sprite = fillSprite;

        Vector3 unitSize = areaSprite.bounds.size;
        baseScale = new Vector3(size.x / unitSize.x, size.y / unitSize.y, 1f);
        areaTf.localScale = baseScale;
        fillTf.localScale = Vector3.zero;

        t = 0f;
        dur = Mathf.Max(0.05f, duration);
        state = State.Charging;

        root.SetActive(true);
        Follow();
        UpdateSorting();
        areaSr.color = new Color(1f, 1f, 1f, 0f);
        fillSr.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0f);
    }

    /// <summary>Damage dihitung sekarang: kilat singkat lalu hilang.</summary>
    public void Hit()
    {
        if (state != State.Charging) return;
        state = State.Flash;
        flashT = 0f;
        fillTf.localScale = baseScale;
    }

    /// <summary>Hilang langsung (boss mati atau serangan dibatalkan).</summary>
    public void Cancel()
    {
        Hide();
    }

    // ---------- tampilan ----------
    void LateUpdate()
    {
        if (state == State.Idle) return;
        if (center == null) { Hide(); return; }

        Follow();

        if (state == State.Charging)
        {
            t += Time.deltaTime;
            if (t > dur + 2f) { Hide(); return; }   // pengaman: Hit()/Cancel() tidak pernah datang

            float p = Mathf.Clamp01(t / dur);
            fillTf.localScale = baseScale * p;

            float fade = fadeInTime > 0f ? Mathf.Clamp01(t / fadeInTime) : 1f;
            float blink = 1f;
            if (blinkLastSeconds > 0f && dur - t < blinkLastSeconds)
                blink = (Mathf.FloorToInt(t * 30f) % 2 == 0) ? 1f : 0.55f;

            float a = fade * blink;
            areaSr.color = new Color(1f, 1f, 1f, a);
            fillSr.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * a);
        }
        else
        {
            flashT += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(flashT / Mathf.Max(0.01f, hitFlashTime));
            areaSr.color = new Color(1f, 1f, 1f, k);
            fillSr.color = new Color(hitColor.r, hitColor.g, hitColor.b, hitColor.a * k);
            if (flashT >= hitFlashTime) { Hide(); return; }
        }

        UpdateSorting();
    }

    void Follow()
    {
        Vector2 c = center();
        root.transform.position = new Vector3(c.x, c.y, transform.position.z);
    }

    void UpdateSorting()
    {
        int layerId = 0, order = 0;
        if (bossSr != null)
        {
            layerId = bossSr.sortingLayerID;
            order = bossSr.sortingOrder;
        }
        areaSr.sortingLayerID = layerId;
        fillSr.sortingLayerID = layerId;
        areaSr.sortingOrder = order + sortingOffset;
        fillSr.sortingOrder = order + sortingOffset + 1;
    }

    void Hide()
    {
        state = State.Idle;
        center = null;
        if (root != null) root.SetActive(false);
    }

    void Build()
    {
        if (root != null) return;

        // objek terpisah (bukan anak boss): tidak ikut skala/rotasi boss, jadi ukurannya persis sama dengan area damage
        root = new GameObject("BossAttackTelegraph (" + name + ")");

        var areaGo = new GameObject("Area");
        areaGo.transform.SetParent(root.transform, false);
        areaTf = areaGo.transform;
        areaSr = areaGo.AddComponent<SpriteRenderer>();

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        fillTf = fillGo.transform;
        fillSr = fillGo.AddComponent<SpriteRenderer>();

        root.SetActive(false);
    }

    // ---------- gambar area (dibuat sekali per ukuran, lalu disimpan) ----------
    void GetSprites(Vector2 size, out Sprite area, out Sprite fill)
    {
        long key = (long)Mathf.RoundToInt(size.x * 100f) * 1000000L + Mathf.RoundToInt(size.y * 100f);
        if (!cache.TryGetValue(key, out Sprite[] pair) || pair[0] == null || pair[1] == null)
        {
            pair = BuildSprites(size);
            cache[key] = pair;
        }
        area = pair[0];
        fill = pair[1];
    }

    Sprite[] BuildSprites(Vector2 size)
    {
        int w = Mathf.Max(4, Mathf.RoundToInt(size.x * pixelsPerUnit));
        int h = Mathf.Max(4, Mathf.RoundToInt(size.y * pixelsPerUnit));

        var areaPix = new Color32[w * h];
        var fillPix = new Color32[w * h];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 outline = outlineColor;
        Color32 area = areaColor;

        float a = w * 0.5f, b = h * 0.5f;
        float thickness = Mathf.Max(1, outlineThickness);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - a, dy = y + 0.5f - b;
                float f = (dx * dx) / (a * a) + (dy * dy) / (b * b);
                int i = y * w + x;

                if (f > 1f) { areaPix[i] = clear; fillPix[i] = clear; continue; }

                // jarak (piksel) dari tepi elips ke arah dalam, supaya garis tepi tebalnya rata di semua sisi
                float r = Mathf.Sqrt(f);
                float dist = float.MaxValue;
                if (r > 0.0001f)
                {
                    float gx = dx / (a * a), gy = dy / (b * b);
                    float g = Mathf.Sqrt(gx * gx + gy * gy) / r;
                    dist = (1f - r) / g;
                }

                if (dist < thickness) { areaPix[i] = outline; fillPix[i] = clear; }
                else { areaPix[i] = area; fillPix[i] = white; }
            }
        }

        return new[] { MakeSprite(areaPix, w, h, "TelegraphArea"), MakeSprite(fillPix, w, h, "TelegraphFill") };
    }

    Sprite MakeSprite(Color32[] pixels, int w, int h, string spriteName)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.name = spriteName;

        created.Add(tex);
        created.Add(sprite);
        return sprite;
    }
}
