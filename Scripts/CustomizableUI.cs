using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Penyimpanan tata letak HUD (PlayerPrefs). Yang disimpan hanya SELISIH dari posisi/ukuran bawaan di scene,
/// bukan posisi mutlak, jadi masih masuk akal kalau tata letak bawaan sebuah stage berubah.
/// Kunci: UILayout_{id}_x / _y / _s (skala) / _a (transparansi).
/// </summary>
public static class UILayoutStore
{
    public const float MinScale = 0.6f;
    public const float MaxScale = 1.6f;
    public const float MinAlpha = 0.3f;   // sengaja tidak 0: tombol Pause dkk tidak boleh bisa dibuat hilang

    public struct Entry
    {
        public Vector2 offset;
        public float scale;
        public float alpha;
    }

    static string K(string id, string field) => $"UILayout_{id}_{field}";

    public static Entry Get(string id)
    {
        return new Entry
        {
            offset = new Vector2(PlayerPrefs.GetFloat(K(id, "x"), 0f), PlayerPrefs.GetFloat(K(id, "y"), 0f)),
            scale = Mathf.Clamp(PlayerPrefs.GetFloat(K(id, "s"), 1f), MinScale, MaxScale),
            alpha = Mathf.Clamp(PlayerPrefs.GetFloat(K(id, "a"), 1f), MinAlpha, 1f)
        };
    }

    public static void Set(string id, Entry e)
    {
        PlayerPrefs.SetFloat(K(id, "x"), e.offset.x);
        PlayerPrefs.SetFloat(K(id, "y"), e.offset.y);
        PlayerPrefs.SetFloat(K(id, "s"), e.scale);
        PlayerPrefs.SetFloat(K(id, "a"), e.alpha);
    }

    public static void Reset(string id)
    {
        PlayerPrefs.DeleteKey(K(id, "x"));
        PlayerPrefs.DeleteKey(K(id, "y"));
        PlayerPrefs.DeleteKey(K(id, "s"));
        PlayerPrefs.DeleteKey(K(id, "a"));
    }

    public static void Flush() => PlayerPrefs.Save();
}

/// <summary>
/// Pasang di tiap elemen HUD yang boleh diatur pemain (Joystick, Attack, Dash, Pause, bar HP, bar HP boss, dll).
/// Saat scene dimuat, elemen otomatis memakai posisi/ukuran/transparansi tersimpan.
/// Mode edit (dinyalakan UICustomizePanel): elemen bisa diseret, script fungsi aslinya dimatikan sementara,
/// dan elemen yang biasanya tersembunyi (bar HP boss) dimunculkan paksa.
/// ID harus SAMA PERSIS di semua scene (huruf besar-kecil juga dibedakan).
/// </summary>
[DisallowMultipleComponent]
public class CustomizableUI : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    /// <summary>Dipanggil saat elemen ditekan di mode edit.</summary>
    public static event Action<CustomizableUI> Selected;

    [Tooltip("Nama unik elemen, mis. Joystick, Attack, Dash, Pause, PlayerHP, BossHP. Sama di semua scene.")]
    [SerializeField] private string id;
    [Tooltip("Boleh diubah ukurannya. Matikan untuk Joystick kalau ukuran ikut mengubah jangkauan input.")]
    [SerializeField] private bool allowScale = true;
    [Tooltip("Aktifkan objek ini paksa selama mode edit supaya bisa digeser walau biasanya tersembunyi (mis. bar HP boss). " +
             "Dikembalikan ke keadaan semula setelah mode edit.")]
    [SerializeField] private bool showWhileEditing;
    [Tooltip("Script fungsi asli yang dimatikan selama mode edit (Joystick, AttackButton, komponen Button pause, dll), " +
             "supaya menyeret elemen tidak menggerakkan Player atau menekan tombol.")]
    [SerializeField] private Behaviour[] disableWhileEditing;

    public string Id => id;
    public bool AllowScale => allowScale;
    public bool IsEditing { get; private set; }
    public RectTransform Rect { get; private set; }
    public Vector2 Offset { get; private set; }
    public float Scale { get; private set; } = 1f;
    public float Alpha { get; private set; } = 1f;

    bool initialized;
    Vector2 defaultPos;
    Vector3 defaultScale;
    CanvasGroup group;
    Canvas canvas;
    bool[] behaviourWasEnabled;
    bool wasActive;
    readonly Vector3[] canvasCorners = new Vector3[4];
    readonly Vector3[] selfCorners = new Vector3[4];

    void Awake() { Init(); }

    void Start()
    {
        // layar bisa lebih sempit dari saat disimpan: jaga supaya tidak keluar layar
        Canvas.ForceUpdateCanvases();
        ClampToCanvas();
        Offset = Rect.anchoredPosition - defaultPos;
    }

    /// <summary>
    /// Aman dipanggil berkali-kali. Perlu karena objek yang tersembunyi sejak awal belum sempat menjalankan Awake
    /// ketika mode edit membukanya.
    /// </summary>
    public void Init()
    {
        if (initialized) return;
        initialized = true;

        Rect = (RectTransform)transform;
        defaultPos = Rect.anchoredPosition;
        defaultScale = Rect.localScale;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        Canvas c = GetComponentInParent<Canvas>(true);
        canvas = c != null ? c.rootCanvas : null;

        if (string.IsNullOrEmpty(id))
            Debug.LogWarning($"{name}: CustomizableUI belum punya ID.");

        LoadFromStore();
    }

    /// <summary>Pakai nilai yang tersimpan (atau bawaan kalau belum pernah disimpan). Membuang perubahan yang belum di-Save.</summary>
    public void LoadFromStore()
    {
        Init();
        var e = UILayoutStore.Get(id);
        Rect.anchoredPosition = defaultPos + e.offset;
        Offset = e.offset;
        SetScale(e.scale, false);
        SetAlpha(e.alpha);
    }

    /// <summary>Kembalikan ke bawaan scene (belum menghapus data tersimpan; itu terjadi saat Save).</summary>
    public void ResetToDefault()
    {
        Init();
        Rect.anchoredPosition = defaultPos;
        Offset = Vector2.zero;
        SetScale(1f, false);
        SetAlpha(1f);
    }

    public void SetScale(float s, bool clamp = true)
    {
        Init();
        Scale = allowScale ? Mathf.Clamp(s, UILayoutStore.MinScale, UILayoutStore.MaxScale) : 1f;
        Rect.localScale = defaultScale * Scale;
        if (clamp)
        {
            ClampToCanvas();
            Offset = Rect.anchoredPosition - defaultPos;
        }
    }

    public void SetAlpha(float a)
    {
        Init();
        Alpha = Mathf.Clamp(a, UILayoutStore.MinAlpha, 1f);
        if (group != null) group.alpha = Alpha;
    }

    /// <summary>Tulis nilai sekarang ke PlayerPrefs (panggil UILayoutStore.Flush() sesudahnya).</summary>
    public void Save()
    {
        UILayoutStore.Set(id, new UILayoutStore.Entry { offset = Offset, scale = Scale, alpha = Alpha });
    }

    /// <summary>Dipanggil UICustomizePanel saat mode edit dibuka/ditutup.</summary>
    public void SetEditing(bool on)
    {
        Init();
        if (on == IsEditing) return;
        IsEditing = on;

        // script fungsi asli: dimatikan selama edit, dikembalikan ke keadaan semula sesudahnya
        if (disableWhileEditing != null && disableWhileEditing.Length > 0)
        {
            if (on) behaviourWasEnabled = new bool[disableWhileEditing.Length];
            for (int i = 0; i < disableWhileEditing.Length; i++)
            {
                var b = disableWhileEditing[i];
                if (b == null) continue;
                if (on) { behaviourWasEnabled[i] = b.enabled; b.enabled = false; }
                else if (behaviourWasEnabled != null) b.enabled = behaviourWasEnabled[i];
            }
        }

        // elemen yang biasanya tersembunyi: nyalakan paksa selama edit
        if (showWhileEditing)
        {
            if (on)
            {
                wasActive = gameObject.activeSelf;
                if (!wasActive)
                {
                    gameObject.SetActive(true);
                    // script di objek ini (mis. bar HP boss) bisa mematikan dirinya di Awake pertamanya
                    if (!gameObject.activeSelf) gameObject.SetActive(true);
                }
            }
            else if (!wasActive) gameObject.SetActive(false);
        }

        if (on)
        {
            Canvas.ForceUpdateCanvases();
            ClampToCanvas();
            Offset = Rect.anchoredPosition - defaultPos;
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (IsEditing) Selected?.Invoke(this);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!IsEditing) return;

        float f = canvas != null ? canvas.scaleFactor : 1f;
        Rect.anchoredPosition += e.delta / Mathf.Max(0.0001f, f);
        ClampToCanvas();
        Offset = Rect.anchoredPosition - defaultPos;
    }

    // elemen tidak boleh keluar dari tepi Canvas
    void ClampToCanvas()
    {
        if (canvas == null) return;

        ((RectTransform)canvas.transform).GetWorldCorners(canvasCorners);
        Rect.GetWorldCorners(selfCorners);

        Vector3 shift = Vector3.zero;
        if (selfCorners[0].x < canvasCorners[0].x) shift.x = canvasCorners[0].x - selfCorners[0].x;
        else if (selfCorners[2].x > canvasCorners[2].x) shift.x = canvasCorners[2].x - selfCorners[2].x;
        if (selfCorners[0].y < canvasCorners[0].y) shift.y = canvasCorners[0].y - selfCorners[0].y;
        else if (selfCorners[2].y > canvasCorners[2].y) shift.y = canvasCorners[2].y - selfCorners[2].y;

        if (shift != Vector3.zero) Rect.position += shift;
    }
}