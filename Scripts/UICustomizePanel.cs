using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mode edit UI sebagai overlay di scene yang sedang berjalan (Main Menu maupun stage), tanpa pindah scene.
/// Pasang di objek kosong yang SELALU aktif (mis. "UICustomizeManager"), bukan di Controls Root-nya sendiri.
/// Hubungkan tombol "UI Customize" di panel Setting ke Open().
///
/// Open  : panel pause/setting disembunyikan (game tetap berhenti di belakang), semua CustomizableUI masuk mode edit,
///         panel kontrol (slider ukuran/transparansi, Save, Reset, Close) muncul.
/// Save  : menulis semua elemen ke PlayerPrefs; mode edit tetap terbuka.
/// Reset : mengembalikan semua elemen ke posisi bawaan (permanen setelah Save).
/// Close : keluar mode edit. Perubahan yang belum di-Save dibuang.
/// Panel ini tidak menyentuh Time.timeScale; stage yang di-pause tetap berhenti.
/// </summary>
public class UICustomizePanel : MonoBehaviour
{
    [Header("Panel kontrol (dimatikan di awal, muncul saat mode edit)")]
    [Tooltip("Letakkan di Canvas terpisah dengan Sort Order lebih tinggi dari HUD. Jangan ada Image selayar penuh dengan Raycast Target aktif di dalamnya, " +
             "karena akan menghalangi penyeretan elemen HUD.")]
    [SerializeField] private GameObject controlsRoot;
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;

    [Header("Selama mode edit")]
    [Tooltip("Objek yang disembunyikan selama edit (panel pause, panel setting). Dikembalikan setelah Close.")]
    [SerializeField] private GameObject[] hideWhileEditing;
    [Tooltip("Opsional, untuk Main Menu: HUD contoh (nonaktif di awal) yang hanya dinyalakan selama mode edit. Kosongkan di stage.")]
    [SerializeField] private GameObject hudPreview;

    [Header("Penanda terpilih (opsional)")]
    [Tooltip("Image bingkai kosong, anak langsung Canvas, pivot 0.5/0.5, Raycast Target mati. Mengikuti elemen terpilih.")]
    [SerializeField] private RectTransform selectionMarker;
    [SerializeField] private float markerPadding = 12f;

    public bool IsOpen { get; private set; }

    CustomizableUI[] elements = new CustomizableUI[0];
    CustomizableUI selected;
    bool refreshing;
    bool[] hiddenWasActive;
    bool previewWasActive;

    void Start()
    {
        if (controlsRoot != null) controlsRoot.SetActive(false);
        if (selectionMarker != null) selectionMarker.gameObject.SetActive(false);

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

        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    void OnDestroy()
    {
        CustomizableUI.Selected -= Select;
        if (sizeSlider != null) sizeSlider.onValueChanged.RemoveListener(OnSize);
        if (opacitySlider != null) opacitySlider.onValueChanged.RemoveListener(OnOpacity);
        if (saveButton != null) saveButton.onClick.RemoveListener(Save);
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetAll);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
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

        CustomizableUI.Selected += Select;
        if (elements.Length > 0) Select(elements[0]);
        else Debug.LogWarning($"{name}: tidak ada CustomizableUI di scene ini.");
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        CustomizableUI.Selected -= Select;

        foreach (var e in elements)
        {
            if (e == null) continue;
            e.LoadFromStore();      // buang perubahan yang belum di-Save
            e.SetEditing(false);
        }

        if (controlsRoot != null) controlsRoot.SetActive(false);
        if (selectionMarker != null) selectionMarker.gameObject.SetActive(false);
        selected = null;

        for (int i = 0; i < hiddenWasActive.Length; i++)
            if (hideWhileEditing[i] != null) hideWhileEditing[i].SetActive(hiddenWasActive[i]);

        if (hudPreview != null) hudPreview.SetActive(previewWasActive);
    }

    public void Save()
    {
        foreach (var e in elements) if (e != null) e.Save();
        UILayoutStore.Flush();
    }

    public void ResetAll()
    {
        foreach (var e in elements) if (e != null) e.ResetToDefault();
        RefreshSliders();
    }

    void LateUpdate()
    {
        if (selectionMarker == null) return;

        bool show = IsOpen && selected != null && selected.gameObject.activeInHierarchy;
        selectionMarker.gameObject.SetActive(show);
        if (!show) return;

        RectTransform r = selected.Rect;
        selectionMarker.position = r.TransformPoint(r.rect.center);
        selectionMarker.sizeDelta = r.rect.size * r.localScale.x + Vector2.one * markerPadding;
    }

    void Select(CustomizableUI e)
    {
        selected = e;
        RefreshSliders();
    }

    void RefreshSliders()
    {
        refreshing = true;
        if (selected != null)
        {
            if (sizeSlider != null)
            {
                sizeSlider.SetValueWithoutNotify(selected.Scale);
                sizeSlider.interactable = selected.AllowScale;
            }
            if (opacitySlider != null) opacitySlider.SetValueWithoutNotify(selected.Alpha);
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
}