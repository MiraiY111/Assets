using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mode edit UI sebagai overlay di scene yang sedang berjalan (Main Menu maupun stage), tanpa pindah scene.
/// Pasang di objek kosong yang SELALU aktif (mis. "UICustomizeManager"), bukan di Controls Root-nya sendiri.
/// Hubungkan tombol "UI Customize" di panel Setting ke Open().
///
/// Open  : panel pause/setting disembunyikan (game tetap berhenti di belakang), semua CustomizableUI masuk mode edit,
///         panel kontrol (slider ukuran/transparansi, Reset, Done) muncul. Tidak ada elemen yang terpilih di awal.
/// Stroke: selama edit, tiap elemen yang bisa digeser diberi garis tepi tipis (tanda "bisa ditekan dan digeser"),
///         dan elemen yang terpilih diberi garis tebal berwarna. Garis dibuat otomatis, tanpa sprite.
/// Reset : mengembalikan semua elemen ke posisi bawaan scene (masih tertunda sampai disimpan).
/// Done  : keluar mode edit dan kembali ke panel Setting. Perubahan TETAP tertunda; belum tersimpan.
///
/// Menyimpan atau membuang perubahan diurus SettingsController (tombol Save Settings / konfirmasi saat Close):
///   CommitAll() = tulis ke PlayerPrefs, RevertAll() = kembalikan ke nilai tersimpan, HasChanges = ada yang belum disimpan.
/// Panel ini tidak menyentuh Time.timeScale; stage yang di-pause tetap berhenti.
/// Hanya mendukung Canvas Screen Space (Overlay atau Camera), bukan World Space.
/// </summary>
public class UICustomizePanel : MonoBehaviour
{
    [Header("Panel kontrol (dimatikan di awal, muncul saat mode edit)")]
    [Tooltip("Letakkan di Canvas terpisah dengan Sort Order lebih tinggi dari HUD. Beri gambar latar dan komponen DragHandle " +
             "supaya panel ini bisa dipindah. Jangan ada Image selayar penuh dengan Raycast Target aktif di dalamnya.")]
    [SerializeField] private GameObject controlsRoot;
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button doneButton;

    [Header("Selama mode edit")]
    [Tooltip("Objek yang disembunyikan selama edit (panel pause, panel setting). Dikembalikan setelah Done.")]
    [SerializeField] private GameObject[] hideWhileEditing;
    [Tooltip("Opsional, untuk Main Menu: HUD contoh (nonaktif di awal) yang hanya dinyalakan selama mode edit. Kosongkan di stage.")]
    [SerializeField] private GameObject hudPreview;

    [Header("Stroke (garis tepi)")]
    [Tooltip("Aktif: SEMUA elemen yang bisa digeser diberi garis tipis, yang terpilih diberi garis tebal. " +
             "Mati: hanya elemen terpilih yang diberi garis.")]
    [SerializeField] private bool showStrokeOnAll = true;
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color otherColor = new Color(1f, 1f, 1f, 0.45f);
    [Tooltip("Tebal garis (satuan Canvas).")]
    [SerializeField] private float selectedThickness = 4f;
    [SerializeField] private float otherThickness = 2f;
    [Tooltip("Jarak garis dari tepi elemen.")]
    [SerializeField] private float strokePadding = 6f;
    [Tooltip("Opsional. Kosong = dibuat otomatis di Canvas yang sama dengan Controls Root, di belakang Controls Root. " +
             "Kalau diisi: harus stretch penuh di Canvas, pivot 0.5/0.5.")]
    [SerializeField] private RectTransform strokeLayer;

    [Header("Debug")]
    [Tooltip("Cetak ke Console: jumlah elemen yang ditemukan, Canvas tempat stroke dibuat, dan elemen yang dipilih. Matikan kalau sudah beres.")]
    [SerializeField] private bool debugLog = true;

    public bool IsOpen { get; private set; }

    /// <summary>True kalau ada elemen yang posisi/ukuran/transparansinya belum disimpan.</summary>
    public bool HasChanges
    {
        get
        {
            foreach (var e in elements)
                if (e != null && e.IsDirty) return true;
            return false;
        }
    }

    CustomizableUI[] elements = new CustomizableUI[0];
    RectStroke[] strokes = new RectStroke[0];
    CustomizableUI selected;
    bool refreshing;
    bool[] hiddenWasActive = new bool[0];
    bool previewWasActive;

    void Start()
    {
        if (controlsRoot != null) controlsRoot.SetActive(false);

        if (sizeSlider != null)
        {
            sizeSlider.minValue = UILayoutStore.MinScale;
            sizeSlider.maxValue = UILayoutStore.MaxScale;
            sizeSlider.wholeNumbers = false;
            sizeSlider.onValueChanged.AddListener(OnSize);
        }
        if (opacitySlider != null)
        {
            opacitySlider.minValue = UILayoutStore.MinAlpha;
            opacitySlider.maxValue = 1f;
            opacitySlider.wholeNumbers = false;
            opacitySlider.onValueChanged.AddListener(OnOpacity);
        }

        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);
        if (doneButton != null) doneButton.onClick.AddListener(Done);
    }

    void OnDestroy()
    {
        CustomizableUI.Selected -= Select;
        if (sizeSlider != null) sizeSlider.onValueChanged.RemoveListener(OnSize);
        if (opacitySlider != null) opacitySlider.onValueChanged.RemoveListener(OnOpacity);
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetAll);
        if (doneButton != null) doneButton.onClick.RemoveListener(Done);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        // HUD contoh (Main Menu) harus aktif dulu supaya elemen di dalamnya ikut ditemukan
        previewWasActive = hudPreview != null && hudPreview.activeSelf;
        if (hudPreview != null) hudPreview.SetActive(true);

        hiddenWasActive = new bool[hideWhileEditing != null ? hideWhileEditing.Length : 0];
        for (int i = 0; i < hiddenWasActive.Length; i++)
        {
            var g = hideWhileEditing[i];
            if (g == null) continue;
            hiddenWasActive[i] = g.activeSelf;
            g.SetActive(false);
        }

        elements = FindObjectsByType<CustomizableUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var e in elements) e.SetEditing(true);

        if (controlsRoot != null) controlsRoot.SetActive(true);
        BuildStrokes();

        if (debugLog)
        {
            Debug.Log($"[UICustomizePanel] Open: {elements.Length} elemen ditemukan, {CountStrokes()} stroke dibuat.");
            foreach (var e in elements)
                if (e != null)
                    Debug.Log($"[UICustomizePanel]   elemen '{e.Id}' (objek {e.name}) aktif={e.gameObject.activeInHierarchy} ukuran={e.Rect.rect.size}");
        }

        CustomizableUI.Selected += Select;
        Select(null);   // belum ada yang terpilih; pemain menyentuh elemen untuk memilih
        if (elements.Length == 0) Debug.LogWarning($"{name}: tidak ada CustomizableUI di scene ini.");
    }

    /// <summary>Keluar mode edit. Perubahan tidak disimpan dan tidak dibuang: tetap tertunda.</summary>
    public void Done()
    {
        if (!IsOpen) return;
        IsOpen = false;

        CustomizableUI.Selected -= Select;
        foreach (var e in elements) if (e != null) e.SetEditing(false);

        DestroyStrokes();
        if (controlsRoot != null) controlsRoot.SetActive(false);
        selected = null;

        for (int i = 0; i < hiddenWasActive.Length; i++)
            if (hideWhileEditing[i] != null) hideWhileEditing[i].SetActive(hiddenWasActive[i]);

        if (hudPreview != null) hudPreview.SetActive(previewWasActive);
    }

    /// <summary>Tulis semua elemen ke PlayerPrefs.</summary>
    public void CommitAll()
    {
        foreach (var e in elements) if (e != null) e.Save();
        UILayoutStore.Flush();
    }

    /// <summary>Kembalikan semua elemen ke nilai yang tersimpan (membuang perubahan yang tertunda).</summary>
    public void RevertAll()
    {
        foreach (var e in elements) if (e != null) e.LoadFromStore();
        RefreshSliders();
    }

    public void ResetAll()
    {
        foreach (var e in elements) if (e != null) e.ResetToDefault();
        RefreshSliders();
    }

    // ---------- stroke ----------
    void LateUpdate()
    {
        if (!IsOpen || strokes.Length != elements.Length) return;

        for (int i = 0; i < strokes.Length; i++)
        {
            CustomizableUI e = elements[i];
            RectStroke s = strokes[i];
            if (e == null || s == null) continue;

            bool isSelected = e == selected;
            bool visible = e.gameObject.activeInHierarchy && (showStrokeOnAll || isSelected);
            s.SetVisible(visible);
            if (!visible) continue;

            s.Place(e.Rect,
                isSelected ? selectedThickness : otherThickness,
                isSelected ? selectedColor : otherColor,
                strokePadding);
        }
    }

    void BuildStrokes()
    {
        DestroyStrokes();

        RectTransform layer = EnsureStrokeLayer();
        if (layer == null) return;
        layer.gameObject.SetActive(true);

        Camera layerCam = CameraOf(layer);
        strokes = new RectStroke[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] == null) continue;
            strokes[i] = new RectStroke(layer, layerCam, CameraOf(elements[i].Rect));
        }
    }

    int CountStrokes()
    {
        int n = 0;
        foreach (var s in strokes) if (s != null) n++;
        return n;
    }

    void DestroyStrokes()
    {
        foreach (var s in strokes) if (s != null) s.Destroy();
        strokes = new RectStroke[0];
        if (strokeLayer != null) strokeLayer.gameObject.SetActive(false);
    }

    RectTransform EnsureStrokeLayer()
    {
        if (strokeLayer != null) return strokeLayer;

        // Canvas tempat garis dibuat: Canvas milik Controls Root; kalau Controls Root belum diisi, Canvas elemen pertama
        Canvas c = null;
        if (controlsRoot != null) c = controlsRoot.GetComponentInParent<Canvas>(true);
        else
            foreach (var e in elements)
                if (e != null) { c = e.GetComponentInParent<Canvas>(true); break; }

        if (c == null)
        {
            Debug.LogWarning($"{name}: Stroke tidak bisa dibuat: tidak ada Canvas yang ditemukan. " +
                             "Pastikan Controls Root berada di dalam Canvas, atau isi Stroke Layer.");
            return null;
        }
        c = c.rootCanvas;

        var go = new GameObject("StrokeLayer", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(c.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // tepat di belakang Controls Root, tetapi di depan semua isi Canvas yang lebih awal (mis. HUD kalau satu Canvas)
        Transform top = controlsRoot != null ? controlsRoot.transform : null;
        while (top != null && top.parent != c.transform) top = top.parent;
        if (top != null && top != rt) rt.SetSiblingIndex(top.GetSiblingIndex());
        else rt.SetAsLastSibling();

        strokeLayer = rt;
        if (debugLog)
            Debug.Log($"[UICustomizePanel] StrokeLayer dibuat di Canvas '{c.name}' (mode {c.renderMode}, sort order {c.sortingOrder}).");
        return rt;
    }

    static Camera CameraOf(RectTransform rt)
    {
        Canvas c = rt.GetComponentInParent<Canvas>(true);
        if (c == null) return null;
        c = c.rootCanvas;
        return c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
    }

    // ---------- pilihan dan slider ----------
    void Select(CustomizableUI e)
    {
        selected = e;
        if (debugLog) Debug.Log($"[UICustomizePanel] Dipilih: {(e != null ? e.Id : "(tidak ada)")}");
        RefreshSliders();

        // garis elemen terpilih digambar paling depan
        if (e != null && strokes.Length == elements.Length)
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] == e && strokes[i] != null) strokes[i].BringToFront();
    }

    void RefreshSliders()
    {
        refreshing = true;

        bool any = selected != null;
        if (sizeSlider != null)
        {
            if (any) sizeSlider.SetValueWithoutNotify(selected.Scale);
            sizeSlider.interactable = any && selected.AllowScale;
        }
        if (opacitySlider != null)
        {
            if (any) opacitySlider.SetValueWithoutNotify(selected.Alpha);
            opacitySlider.interactable = any;
        }

        refreshing = false;
    }

    void OnSize(float v)
    {
        if (refreshing || selected == null) return;
        selected.SetScale(v);
    }

    void OnOpacity(float v)
    {
        if (refreshing || selected == null) return;
        selected.SetAlpha(v);
    }

    /// <summary>
    /// Bingkai empat garis di sekeliling sebuah elemen. Posisi dihitung lewat koordinat layar, jadi tetap pas
    /// walau elemen dan lapisan stroke ada di Canvas yang berbeda (mis. HUD di satu Canvas, panel kontrol di Canvas lain).
    /// </summary>
    class RectStroke
    {
        static readonly Vector3[] corners = new Vector3[4];

        readonly RectTransform root;
        readonly RectTransform layer;
        readonly Camera layerCam;
        readonly Camera elementCam;
        readonly Image top, bottom, left, right;

        public RectStroke(RectTransform layer, Camera layerCam, Camera elementCam)
        {
            this.layer = layer;
            this.layerCam = layerCam;
            this.elementCam = elementCam;

            var go = new GameObject("Stroke", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(layer, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);

            top = Edge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            bottom = Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            left = Edge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            right = Edge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
        }

        Image Edge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;   // garis tidak boleh menghalangi sentuhan ke elemen
            return img;
        }

        public void SetVisible(bool on)
        {
            if (root != null && root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
        }

        public void BringToFront()
        {
            if (root != null) root.SetAsLastSibling();
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root.gameObject);
        }

        public void Place(RectTransform element, float thickness, Color color, float padding)
        {
            if (root == null) return;

            element.GetWorldCorners(corners);   // [0] kiri bawah, [2] kanan atas
            Vector2 s0 = RectTransformUtility.WorldToScreenPoint(elementCam, corners[0]);
            Vector2 s2 = RectTransformUtility.WorldToScreenPoint(elementCam, corners[2]);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, s0, layerCam, out Vector2 l0);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, s2, layerCam, out Vector2 l2);

            root.anchoredPosition = (l0 + l2) * 0.5f - layer.rect.center;
            root.sizeDelta = new Vector2(Mathf.Abs(l2.x - l0.x), Mathf.Abs(l2.y - l0.y)) + Vector2.one * (padding * 2f);

            top.rectTransform.sizeDelta = new Vector2(0f, thickness);
            bottom.rectTransform.sizeDelta = new Vector2(0f, thickness);
            left.rectTransform.sizeDelta = new Vector2(thickness, 0f);
            right.rectTransform.sizeDelta = new Vector2(thickness, 0f);

            top.color = bottom.color = left.color = right.color = color;
        }
    }
}