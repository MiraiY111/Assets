using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP bar di atas monster. Tersembunyi saat HP penuh, muncul setelah kena damage,
/// hilang lagi saat monster mati. Tidak ada teks damage.
/// Pasang di prefab monster (satu objek dengan EnemyHealth). Bar dibuat otomatis saat Play
/// sebagai Canvas World Space di bawah monster, jadi tidak perlu menyusun UI manual.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    const string RootName = "HPBarCanvas";

    [SerializeField] private EnemyHealth health;    // kosong = diambil dari objek ini

    [Tooltip("Urut dari PENUH ke KOSONG: 04_55, 04_56, 04_57, 04_58, 04_59, 04_60. " +
             "Sprite pertama (penuh) dan terakhir (kosong) praktis tidak terlihat karena bar disembunyikan di HP penuh dan saat mati.")]
    [SerializeField] private Sprite[] steps;

    [Header("Posisi dan ukuran")]
    [Tooltip("Geseran dari pusat monster (satuan Unity). y positif = ke atas.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.8f);
    [Tooltip("Lebar bar dalam satuan Unity. Tinggi mengikuti proporsi sprite.")]
    [SerializeField] private float barWorldWidth = 0.8f;

    [Header("Urutan gambar")]
    [Tooltip("Sorting Layer khusus (mis. WorldUI) yang diletakkan paling bawah di daftar Sorting Layers. " +
             "Nama harus persis sama, kalau salah Unity diam-diam memakai Default.")]
    [SerializeField] private string sortingLayerName = "WorldUI";
    [SerializeField] private int sortingOrder = 0;

    [Header("Debug")]
    [Tooltip("Cetak HP dan sprite yang dipilih ke Console setiap kena damage.")]
    [SerializeField] private bool debugLog = false;

    GameObject root;
    Image image;

    void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
    }

    void Start()
    {
        if (health == null || steps == null || steps.Length == 0 || steps[0] == null)
        {
            Debug.LogWarning($"{name}: EnemyHealthBar belum lengkap (health / steps).");
            enabled = false;
            return;
        }

        for (int i = 0; i < steps.Length; i++)
            if (steps[i] == null) Debug.LogWarning($"{name}: Steps[{i}] kosong.");

        Build();
        health.OnHealthChanged += Refresh;
        health.OnDied += HandleDied;
        Refresh(health.CurrentHP, health.MaxHP);
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
            health.OnDied -= HandleDied;
        }
    }

    void Build()
    {
        // Buang bar lama kalau ada. Ini terjadi kalau monster ini hasil Instantiate dari objek
        // Hierarchy yang sudah pernah Play: bar hasil Build() sebelumnya ikut tersalin, tidak
        // dikendalikan siapa pun, dan tampil terus dengan sprite terakhirnya.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name == RootName) Destroy(c.gameObject);
        }

        // Canvas diskalakan 0.01, jadi 100 satuan Canvas = 1 satuan Unity
        float aspect = steps[0].rect.height / steps[0].rect.width;

        root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        root.transform.localPosition = offset;
        root.transform.localScale = Vector3.one * 0.01f;

        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(barWorldWidth * 100f, barWorldWidth * 100f * aspect);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = sortingLayerName;
        canvas.sortingOrder = sortingOrder;

        var imgObj = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(root.transform, false);
        var irt = (RectTransform)imgObj.transform;
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;

        image = imgObj.GetComponent<Image>();
        image.raycastTarget = false;

        root.SetActive(false);
    }

    void Refresh(int current, int max)
    {
        if (root == null) return;

        // muncul hanya kalau sudah terluka tapi belum mati
        bool show = !health.IsDead && current > 0 && current < max;
        root.SetActive(show);
        if (!show) return;

        int i = StepIndex(current, max, steps.Length);
        image.sprite = steps[i];

        if (debugLog)
            Debug.Log($"{name} bar: {current}/{max} -> index {i} = {(steps[i] != null ? steps[i].name : "KOSONG")}");
    }

    void HandleDied(EnemyHealth e)
    {
        if (root != null) root.SetActive(false);
    }

    // Penuh -> sprite pertama, 0 -> sprite terakhir, HP di antaranya dibagi rata ke sprite tengah.
    static int StepIndex(int current, int max, int n)
    {
        if (n <= 1) return 0;
        if (max <= 0 || current <= 0) return n - 1;
        if (current >= max) return 0;

        int bands = n - 2;
        if (bands < 1) return n - 1;

        float ratio = (float)current / max;
        int band = Mathf.Clamp(Mathf.CeilToInt(ratio * bands - 0.0001f), 1, bands);
        return 1 + (bands - band);
    }
}