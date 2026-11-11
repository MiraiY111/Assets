using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efek koin terbang: saat monster mati, beberapa koin muncul dari posisi monster, meletus sebentar,
/// lalu melesat (makin cepat) ke ikon koin di HUD. Tiap koin yang tiba menambah angka dan membuat ikon membal
/// (diatur CoinCounterUI.Receive).
/// Pasang di objek HUD mana saja (mis. objek yang sama dengan CoinCounterUI). Lapisan penuh layar tempat koin
/// terbang dibuat otomatis di Canvas yang sama dengan penghitung koin, dan koin ditampilkan di atas HUD lain.
/// Koin dipakai ulang (pool), jadi tidak membuat sampah tiap monster mati.
/// Animasi memakai waktu normal (ikut berhenti saat game di-pause).
/// </summary>
public class CoinFlyEffect : MonoBehaviour
{
    [SerializeField] private CoinSystem coins;       // kosong = dicari otomatis
    [SerializeField] private CoinCounterUI counter;  // kosong = dicari otomatis
    [Tooltip("Kamera dunia untuk mengubah posisi monster ke layar. Kosong = Camera.main.")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Opsional: lapisan penuh layar tempat koin terbang. Kosong = dibuat otomatis di Canvas penghitung koin.")]
    [SerializeField] private RectTransform flyLayer;

    [Header("Tampilan koin")]
    [Tooltip("Frame putar koin (6 frame dari coin_spin_sheet). Kosong = pakai Single Sprite dan putar lewat skala.")]
    [SerializeField] private Sprite[] spinFrames;
    [Tooltip("Dipakai kalau Spin Frames kosong.")]
    [SerializeField] private Sprite singleSprite;
    [Tooltip("Ukuran koin di layar (satuan Canvas). Pakai kelipatan 16 supaya pikselnya rata, mis. 32 atau 48.")]
    [SerializeField] private Vector2 coinSize = new Vector2(32f, 32f);
    [SerializeField] private float spinFps = 12f;

    [Header("Jumlah koin")]
    [Tooltip("Paling banyak koin yang terlihat terbang per monster. Kalau koinnya lebih banyak, nilainya dibagi rata.")]
    [SerializeField] private int maxVisualCoins = 6;
    [Tooltip("Jeda antar koin saat muncul (detik).")]
    [SerializeField] private float spawnStagger = 0.04f;

    [Header("Gerak (satuan Canvas)")]
    [Tooltip("Seberapa jauh koin meletus dari monster.")]
    [SerializeField] private float burstRadius = 70f;
    [SerializeField] private float burstTime = 0.25f;
    [Tooltip("Jeda koin melayang sebelum melesat ke HUD (detik).")]
    [SerializeField] private float holdTime = 0.1f;
    [SerializeField] private float flyTime = 0.5f;
    [Tooltip("Variasi waktu terbang tiap koin, supaya tiba tidak serempak (detik).")]
    [SerializeField] private float flyTimeJitter = 0.1f;
    [Tooltip("Lengkungan lintasan ke atas.")]
    [SerializeField] private float arcHeight = 60f;

    class CoinVisual
    {
        public RectTransform rt;
        public Image img;
    }

    readonly Stack<CoinVisual> pool = new Stack<CoinVisual>();
    readonly List<CoinVisual> active = new List<CoinVisual>();
    Canvas rootCanvas;
    Camera canvasCam;
    int pendingAmount;     // koin yang masih terbang (belum ditambahkan ke angka HUD)
    bool subscribed;

    void Start()
    {
        if (coins == null) coins = FindAnyObjectByType<CoinSystem>();
        if (counter == null) counter = FindAnyObjectByType<CoinCounterUI>();
        if (coins == null || counter == null)
        {
            Debug.LogWarning($"{name}: CoinFlyEffect butuh CoinSystem dan CoinCounterUI di scene. Efek dimatikan.");
            enabled = false;
            return;
        }

        bool hasSprite = (spinFrames != null && spinFrames.Length > 0) || singleSprite != null;
        if (!hasSprite)
        {
            Debug.LogWarning($"{name}: Spin Frames dan Single Sprite kosong, efek koin terbang dimatikan " +
                             "(angka koin tetap naik bertahap).");
            enabled = false;
            return;
        }

        Canvas c = counter.GetComponentInParent<Canvas>();
        if (c == null)
        {
            Debug.LogWarning($"{name}: CoinCounterUI tidak ada di dalam Canvas. Efek dimatikan.");
            enabled = false;
            return;
        }
        rootCanvas = c.rootCanvas;
        canvasCam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        if (flyLayer == null)
        {
            var go = new GameObject("CoinFlyLayer", typeof(RectTransform));
            flyLayer = (RectTransform)go.transform;
            flyLayer.SetParent(rootCanvas.transform, false);
            flyLayer.anchorMin = Vector2.zero;
            flyLayer.anchorMax = Vector2.one;
            flyLayer.offsetMin = Vector2.zero;
            flyLayer.offsetMax = Vector2.zero;
            flyLayer.SetAsLastSibling();   // koin di atas HUD lain; tanpa Graphic, jadi tidak menahan klik
        }

        counter.SetFlyMode(true);
        coins.OnCoinsDropped += OnDropped;
        subscribed = true;
    }

    void OnDisable()
    {
        // koin yang sedang terbang dihentikan: tambahkan sisanya langsung supaya angka HUD tidak tertinggal
        if (pendingAmount > 0 && counter != null) counter.Receive(pendingAmount);
        pendingAmount = 0;

        for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
    }

    void OnDestroy()
    {
        if (subscribed && coins != null) coins.OnCoinsDropped -= OnDropped;
    }

    void OnDropped(int amount, Vector3 worldPos)
    {
        int n = Mathf.Clamp(amount, 1, Mathf.Max(1, maxVisualCoins));
        int each = amount / n;
        int extra = amount % n;
        Vector2 start = WorldToLayer(worldPos);

        pendingAmount += amount;
        for (int i = 0; i < n; i++)
            StartCoroutine(FlyCoin(start, each + (i < extra ? 1 : 0), i * spawnStagger));
    }

    // ---------- gerak satu koin ----------
    IEnumerator FlyCoin(Vector2 start, int share, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        CoinVisual c = Rent();
        float phase = Random.value;
        float age = 0f;

        Vector2 burstDir = Random.insideUnitCircle;
        if (burstDir.sqrMagnitude < 0.01f) burstDir = Vector2.up;
        Vector2 burst = start + burstDir.normalized * Random.Range(burstRadius * 0.4f, burstRadius);

        SetCoin(c, start, 0.3f, phase, age);

        // 1) meletus keluar dari monster dengan sedikit lompatan, sambil membesar
        float t = 0f;
        while (t < 1f)
        {
            float dt = Time.deltaTime;
            age += dt;
            t += dt / Mathf.Max(0.01f, burstTime);
            float k = Mathf.Clamp01(t);
            float e = 1f - (1f - k) * (1f - k);
            Vector2 p = Vector2.LerpUnclamped(start, burst, e);
            p.y += Mathf.Sin(k * Mathf.PI) * 18f;
            SetCoin(c, p, Mathf.Lerp(0.3f, 1f, e), phase, age);
            yield return null;
        }

        // 2) melayang sebentar, terus berputar
        float hold = 0f;
        while (hold < holdTime)
        {
            float dt = Time.deltaTime;
            age += dt;
            hold += dt;
            SetCoin(c, burst, 1f, phase, age);
            yield return null;
        }

        // 3) melesat ke ikon di HUD (makin cepat), mengikuti kalau HUD digeser
        float dur = Mathf.Max(0.1f, flyTime + Random.Range(-flyTimeJitter, flyTimeJitter));
        t = 0f;
        while (t < 1f)
        {
            float dt = Time.deltaTime;
            age += dt;
            t += dt / dur;
            float k = Mathf.Clamp01(t);
            float e = k * k;

            Vector2 to = TargetLocal();
            Vector2 mid = (burst + to) * 0.5f + Vector2.up * arcHeight;
            Vector2 a = Vector2.Lerp(burst, mid, e);
            Vector2 b = Vector2.Lerp(mid, to, e);
            SetCoin(c, Vector2.Lerp(a, b, e), Mathf.Lerp(1f, 0.75f, e), phase, age);
            yield return null;
        }

        Release(c);
        pendingAmount = Mathf.Max(0, pendingAmount - share);
        if (counter != null) counter.Receive(share);
    }

    void SetCoin(CoinVisual c, Vector2 pos, float scale, float phase, float age)
    {
        c.rt.localPosition = new Vector3(pos.x, pos.y, 0f);

        if (spinFrames != null && spinFrames.Length > 0)
        {
            int f = Mathf.FloorToInt((age + phase) * spinFps) % spinFrames.Length;
            c.img.sprite = spinFrames[f];
            c.rt.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            // satu sprite: berputar lewat skala X (tidak serapi frame piksel, tapi tanpa setup tambahan)
            float sx = Mathf.Abs(Mathf.Cos((age + phase) * spinFps * 0.5f));
            c.rt.localScale = new Vector3(scale * Mathf.Max(0.15f, sx), scale, 1f);
        }
    }

    // ---------- koordinat ----------
    Vector2 WorldToLayer(Vector3 worldPos)
    {
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        Vector2 screen = cam != null
            ? (Vector2)cam.WorldToScreenPoint(worldPos)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(flyLayer, screen, canvasCam, out Vector2 local);
        return local;
    }

    Vector2 TargetLocal()
    {
        Vector3 p = flyLayer.InverseTransformPoint(counter.IconTarget.position);
        return new Vector2(p.x, p.y);
    }

    // ---------- pool ----------
    CoinVisual Rent()
    {
        CoinVisual c = null;
        while (pool.Count > 0 && (c == null || c.rt == null)) c = pool.Pop();

        if (c == null || c.rt == null)
        {
            var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
            c = new CoinVisual { rt = (RectTransform)go.transform, img = go.GetComponent<Image>() };
            c.rt.SetParent(flyLayer, false);
            c.rt.anchorMin = c.rt.anchorMax = c.rt.pivot = new Vector2(0.5f, 0.5f);
            c.rt.sizeDelta = coinSize;
            c.img.raycastTarget = false;
            c.img.preserveAspect = true;
        }

        c.img.sprite = (spinFrames != null && spinFrames.Length > 0) ? spinFrames[0] : singleSprite;
        c.rt.gameObject.SetActive(true);
        active.Add(c);
        return c;
    }

    void Release(CoinVisual c)
    {
        active.Remove(c);
        if (c == null || c.rt == null) return;
        c.rt.gameObject.SetActive(false);
        pool.Push(c);
    }
}
