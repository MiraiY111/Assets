using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Layar UI Customize (scene "UICustomize"). Pasang di objek kosong "UICustomizeManager".
/// Elemen tiruan (Joystick, Attack, Dash) diseret langsung; slider mengatur ukuran dan transparansi elemen terpilih.
/// Save  : menulis semua elemen ke PlayerPrefs.
/// Reset : mengembalikan semua elemen ke posisi bawaan (baru permanen setelah Save).
/// Back  : kembali ke scene yang membuka layar ini. Perubahan yang belum di-Save dibuang.
/// </summary>
public class UICustomizeScreen : MonoBehaviour
{
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private CustomizableUI[] elements;

    [Header("Kontrol")]
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    [Header("Penanda terpilih (opsional)")]
    [Tooltip("Image bingkai kosong, anak langsung Canvas, pivot 0.5/0.5, Raycast Target mati. Mengikuti elemen terpilih.")]
    [SerializeField] private RectTransform selectionMarker;
    [SerializeField] private float markerPadding = 12f;

    CustomizableUI selected;
    bool refreshing;

    void Start()
    {
        Time.timeScale = 1f;

        if (elements == null || elements.Length == 0)
            elements = FindObjectsByType<CustomizableUI>(FindObjectsSortMode.None);

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
        if (backButton != null) backButton.onClick.AddListener(Back);

        CustomizableUI.Selected += Select;
        if (elements.Length > 0) Select(elements[0]);
        else Debug.LogWarning($"{name}: tidak ada CustomizableUI di scene ini.");
    }

    void OnDestroy()
    {
        CustomizableUI.Selected -= Select;
        if (sizeSlider != null) sizeSlider.onValueChanged.RemoveListener(OnSize);
        if (opacitySlider != null) opacitySlider.onValueChanged.RemoveListener(OnOpacity);
        if (saveButton != null) saveButton.onClick.RemoveListener(Save);
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetAll);
        if (backButton != null) backButton.onClick.RemoveListener(Back);
    }

    void LateUpdate()
    {
        if (selectionMarker == null) return;
        selectionMarker.gameObject.SetActive(selected != null);
        if (selected == null) return;

        RectTransform r = selected.Rect;
        selectionMarker.position = r.TransformPoint(r.rect.center);
        Vector2 size = r.rect.size * r.localScale.x + Vector2.one * markerPadding;
        selectionMarker.sizeDelta = size;
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
            if (sizeSlider != null) sizeSlider.SetValueWithoutNotify(selected.Scale);
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

    public void Back()
    {
        string target = UICustomizeLauncher.ReturnScene;
        if (string.IsNullOrEmpty(target)) target = "MainMenu";
        SceneManager.LoadScene(target);
    }
}