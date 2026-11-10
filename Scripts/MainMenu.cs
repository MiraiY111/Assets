using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main Menu: Play, Setting, Exit, dengan animasi.
/// Pasang di objek kosong "MainMenuManager" (selalu aktif), lalu hubungkan fungsi publik
/// ke OnClick tiap tombol: Play(), OpenSettings(), CloseSettings(), Quit().
/// Tombol memakai PressSpriteButton untuk ganti sprite normal/pressed; script ini hanya menangani aksinya.
/// BGM: AudioSource ada di scene Main Menu saja (tanpa DontDestroyOnLoad), jadi berhenti sendiri saat pindah scene.
///
/// Play     : selalu mulai dari Stage 1 dengan Level 0. Save yang ada TIDAK dihapus (baru tertimpa kalau
///            pemain menekan Save di menu pause), jadi salah tekan Play tidak menghilangkan save.
/// Continue : memuat stage yang tersimpan (mengulang stage itu dari awal, dengan Level/EXP yang dimiliki
///            sebelum masuk stage). Kalau belum ada save (atau datanya rusak / scene-nya tidak ada di
///            Build Settings), tombol berwarna abu-abu dan tidak bisa diklik.
/// NewGame  : menghapus save dan mulai dari Stage 1. Belum dipasang ke tombol; beri dialog konfirmasi dulu.
///
/// Buka Setting : tombol (ButtonGroup) mundur sedikit ke kanan lalu meluncur halus ke kiri keluar layar,
///                panel Setting membesar dari skala 0. Selama panel terbuka, tombol kiri tidak bisa diklik.
/// Tutup Setting: panel mengecil sampai hilang, tombol meluncur masuk halus dari kiri.
/// Semua animasi memakai waktu unscaled, jadi tetap jalan walau Time.timeScale = 0.
/// </summary>
public class MainMenu : MonoBehaviour
{
    const string VolumeKey = "MasterVolume";

    [Header("Scene")]
    [Tooltip("Nama scene stage 1. Harus sudah ada di File > Build Settings.")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Tombol kiri (Play, Setting, Exit)")]
    [Tooltip("Objek induk yang berisi 3 tombol. Ukuran (Rect) objek ini harus membungkus semua tombol.")]
    [SerializeField] private RectTransform buttonGroup;
    [Tooltip("Mundur sedikit ke kanan sebelum meluncur ke kiri (satuan Canvas).")]
    [SerializeField] private float anticipationDistance = 40f;
    [SerializeField] private float anticipationTime = 0.15f;
    [SerializeField] private float slideOutTime = 0.45f;
    [SerializeField] private float slideInTime = 0.5f;
    [Tooltip("Tambahan jarak di luar tepi kiri layar supaya tombol benar-benar tidak kelihatan.")]
    [SerializeField] private float offscreenMargin = 60f;

    [Header("Continue")]
    [Tooltip("Tombol Continue. Letakkan di DALAM Button Group supaya ikut meluncur saat Setting dibuka. " +
             "Kosong = tombol Continue tidak diatur oleh script ini.")]
    [SerializeField] private Button continueButton;
    [Tooltip("Warna pengali saat Continue tidak tersedia (putih = tanpa perubahan, makin gelap = makin abu-abu).")]
    [SerializeField] private Color disabledTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Panel")]
    [Tooltip("Panel Setting (dimatikan di awal). Pivot sebaiknya 0.5, 0.5 supaya membesar dari tengah. Kosong = tombol Setting belum melakukan apa-apa.")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private float panelOpenTime = 0.35f;
    [SerializeField] private float panelCloseTime = 0.25f;
    [Tooltip("Seberapa melewati ukuran penuh saat membuka (0 = tanpa membal, 1.2 = sedikit membal).")]
    [SerializeField] private float panelOvershoot = 1.2f;

    [Header("Musik")]
    [Tooltip("Opsional: AudioSource BGM Main Menu (objek MenuBGM). Kosong = tanpa fade-out saat Play.")]
    [SerializeField] private AudioSource bgm;
    [Tooltip("Lama musik memudar sebelum pindah ke stage (detik).")]
    [SerializeField] private float fadeOutTime = 0.4f;

    [Header("Jeda klik")]
    [Tooltip("Jeda sebelum aksi dijalankan supaya efek klik (sprite pressed/suara) sempat terlihat.")]
    [SerializeField] private float clickDelay = 0.15f;

    bool busy;
    bool settingsOpen;

    RectTransform panelRect;
    CanvasGroup panelGroup;
    CanvasGroup buttonsCanvasGroup;
    Vector2 shownPos;
    float hiddenOffset = -1f;
    readonly Vector3[] cornersA = new Vector3[4];
    readonly Vector3[] cornersB = new Vector3[4];

    Graphic[] continueGraphics;
    Color[] continueOriginalColors;

    void Start()
    {
        Time.timeScale = 1f; // jaga-jaga kalau datang dari scene yang sempat di-pause

        if (buttonGroup != null)
        {
            shownPos = buttonGroup.anchoredPosition;
            buttonsCanvasGroup = GetOrAddGroup(buttonGroup.gameObject);
        }

        if (settingsPanel != null)
        {
            panelRect = settingsPanel.GetComponent<RectTransform>();
            panelGroup = GetOrAddGroup(settingsPanel);
            panelRect.localScale = Vector3.zero;
            settingsPanel.SetActive(false);
        }

        // volume awal; pengaturan volume di panel Setting diurus SettingsController
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

        RefreshContinue();
    }

    // ---------- tombol Continue ----------
    /// <summary>True kalau ada save yang valid dan scene-nya ada di Build Settings.</summary>
    bool CanContinue()
    {
        return SaveGameStore.TryLoad(out SaveData save) && Application.CanStreamedLevelBeLoaded(save.sceneName);
    }

    /// <summary>
    /// Atur tampilan Continue: normal kalau ada save, abu-abu dan tidak bisa diklik kalau tidak.
    /// Button.Transition = None (sprite diganti PressSpriteButton), jadi warna diatur langsung di semua gambar tombol.
    /// </summary>
    void RefreshContinue()
    {
        if (continueButton == null) return;

        // simpan warna asli sekali, supaya bisa dikembalikan kalau save muncul lagi
        if (continueGraphics == null)
        {
            continueGraphics = continueButton.GetComponentsInChildren<Graphic>(true);
            continueOriginalColors = new Color[continueGraphics.Length];
            for (int i = 0; i < continueGraphics.Length; i++)
                continueOriginalColors[i] = continueGraphics[i].color;
        }

        bool available = CanContinue();
        continueButton.interactable = available;

        for (int i = 0; i < continueGraphics.Length; i++)
        {
            if (continueGraphics[i] == null) continue;
            continueGraphics[i].color = available
                ? continueOriginalColors[i]
                : continueOriginalColors[i] * disabledTint;
        }
    }

    static CanvasGroup GetOrAddGroup(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }

    // ---------- aksi tombol ----------
    /// <summary>Run baru dari Stage 1, Level 0. Save yang ada dibiarkan (tidak dihapus).</summary>
    public void Play()
    {
        if (busy || settingsOpen) return;
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogWarning($"{name}: Game Scene Name kosong.");
            return;
        }

        ExpSystem.ResetRun();
        StartCoroutine(PlayRoutine(gameSceneName));
    }

    /// <summary>Lanjut dari save: stage tersimpan diulang dari awal dengan Level/EXP sebelum masuk stage itu.</summary>
    public void ContinueGame()
    {
        if (busy || settingsOpen) return;

        if (!SaveGameStore.TryLoad(out SaveData save) || !Application.CanStreamedLevelBeLoaded(save.sceneName))
        {
            RefreshContinue();   // save hilang atau rusak sejak menu dibuka: tombol kembali abu-abu
            return;
        }

        ExpSystem.LoadSnapshot(save.level, save.exp);
        StartCoroutine(PlayRoutine(save.sceneName));
    }

    /// <summary>Hapus save dan mulai dari Stage 1. Pasang ke tombol hanya setelah ada dialog konfirmasi.</summary>
    public void NewGame()
    {
        if (busy || settingsOpen) return;
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogWarning($"{name}: Game Scene Name kosong.");
            return;
        }

        SaveGameStore.Delete();
        ExpSystem.ResetRun();
        StartCoroutine(PlayRoutine(gameSceneName));
    }

    // jeda klik, musik memudar, baru pindah scene
    IEnumerator PlayRoutine(string sceneName)
    {
        busy = true;
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);

        if (bgm != null && fadeOutTime > 0f)
        {
            float start = bgm.volume;
            float t = 0f;
            while (t < fadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                bgm.volume = Mathf.Lerp(start, 0f, t / fadeOutTime);
                yield return null;
            }
        }

        SceneManager.LoadScene(sceneName);
    }

    public void Quit()
    {
        if (busy || settingsOpen) return;
        StartCoroutine(Delayed(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // Application.Quit() tidak bekerja di Editor
#else
            Application.Quit();
#endif
        }));
    }

    public void OpenSettings()
    {
        if (busy || settingsOpen || settingsPanel == null) return;
        StartCoroutine(OpenRoutine());
    }

    public void CloseSettings()
    {
        if (busy || !settingsOpen || settingsPanel == null) return;
        StartCoroutine(CloseRoutine());
    }

    // aksi yang keluar dari menu: tombol lain diabaikan setelah klik
    IEnumerator Delayed(Action action)
    {
        busy = true;
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);
        action();
    }

    // ---------- urutan buka / tutup ----------
    IEnumerator OpenRoutine()
    {
        busy = true;

        SetInteractable(buttonsCanvasGroup, false);   // tombol kiri tidak bisa diklik mulai sekarang
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);

        settingsPanel.SetActive(true);
        panelRect.localScale = Vector3.zero;
        SetInteractable(panelGroup, false);           // baru bisa diklik setelah selesai membesar

        Coroutine a = buttonGroup != null ? StartCoroutine(ButtonsOut()) : null;
        Coroutine b = StartCoroutine(PanelScale(true));
        if (a != null) yield return a;
        yield return b;

        SetInteractable(panelGroup, true);
        settingsOpen = true;
        busy = false;
    }

    IEnumerator CloseRoutine()
    {
        busy = true;

        SetInteractable(panelGroup, false);
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);

        Coroutine a = buttonGroup != null ? StartCoroutine(ButtonsIn()) : null;
        Coroutine b = StartCoroutine(PanelScale(false));
        yield return b;
        if (a != null) yield return a;

        settingsPanel.SetActive(false);
        SetInteractable(buttonsCanvasGroup, true);
        settingsOpen = false;
        busy = false;
    }

    static void SetInteractable(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    // ---------- animasi ----------
    IEnumerator ButtonsOut()
    {
        if (hiddenOffset < 0f) hiddenOffset = ComputeHiddenOffset();

        Vector2 shown = shownPos;
        Vector2 back = shown + Vector2.right * anticipationDistance;
        Vector2 hidden = shown + Vector2.left * hiddenOffset;

        // 1) mundur sedikit ke kanan
        yield return Tween(anticipationTime, t =>
            buttonGroup.anchoredPosition = Vector2.LerpUnclamped(shown, back, EaseOutQuad(t)));

        // 2) meluncur halus ke kiri sampai keluar layar (mulai pelan, makin cepat)
        yield return Tween(slideOutTime, t =>
            buttonGroup.anchoredPosition = Vector2.LerpUnclamped(back, hidden, EaseInCubic(t)));

        buttonGroup.anchoredPosition = hidden;
    }

    IEnumerator ButtonsIn()
    {
        if (hiddenOffset < 0f) hiddenOffset = ComputeHiddenOffset();

        Vector2 hidden = shownPos + Vector2.left * hiddenOffset;
        Vector2 shown = shownPos;

        // masuk dari kiri, cepat di awal lalu melambat halus di posisi akhir
        yield return Tween(slideInTime, t =>
            buttonGroup.anchoredPosition = Vector2.LerpUnclamped(hidden, shown, EaseOutCubic(t)));

        buttonGroup.anchoredPosition = shown;
    }

    IEnumerator PanelScale(bool opening)
    {
        if (opening)
        {
            yield return Tween(panelOpenTime, t =>
                panelRect.localScale = Vector3.one * EaseOutBack(t, panelOvershoot));
            panelRect.localScale = Vector3.one;
        }
        else
        {
            yield return Tween(panelCloseTime, t =>
                panelRect.localScale = Vector3.one * (1f - EaseInCubic(t)));
            panelRect.localScale = Vector3.zero;
        }
    }

    // Jarak (satuan Canvas) yang dibutuhkan supaya tepi KANAN ButtonGroup melewati tepi KIRI layar.
    float ComputeHiddenOffset()
    {
        Canvas canvas = buttonGroup.GetComponentInParent<Canvas>();
        if (canvas == null) return 1200f;
        canvas = canvas.rootCanvas;

        ((RectTransform)canvas.transform).GetWorldCorners(cornersA);   // [0] = kiri bawah canvas
        buttonGroup.GetWorldCorners(cornersB);                         // [2] = kanan atas group

        float worldDist = cornersB[2].x - cornersA[0].x;
        float scale = Mathf.Max(0.0001f, canvas.transform.lossyScale.x);
        return Mathf.Max(0f, worldDist / scale) + offscreenMargin;
    }

    static IEnumerator Tween(float duration, Action<float> apply)
    {
        if (duration <= 0f) { apply(1f); yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            apply(Mathf.Clamp01(t));
            yield return null;
        }
    }

    // ---------- easing ----------
    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    static float EaseInCubic(float t) => t * t * t;
    static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

    // s = kekuatan membal (0 = tanpa membal)
    static float EaseOutBack(float t, float s)
    {
        float u = t - 1f;
        return 1f + (s + 1f) * u * u * u + s * u * u;
    }
}