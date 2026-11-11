using System;
using System.Collections;
using System.Collections.Generic;
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
/// Buka Setting : tiap tombol di dalam ButtonGroup mundur sedikit ke kanan lalu meluncur halus ke kiri keluar layar,
///                BERGANTIAN dari atas ke bawah (jeda Stagger Delay), sementara panel Setting membesar dari skala 0.
///                Selama panel terbuka, tombol kiri tidak bisa diklik.
/// Tutup Setting: panel mengecil sampai hilang, tombol meluncur masuk halus dari kiri, juga bergantian.
/// Semua animasi memakai waktu unscaled, jadi tetap jalan walau Time.timeScale = 0.
/// </summary>
public class MainMenu : MonoBehaviour
{
    const string VolumeKey = "MasterVolume";

    [Header("Scene")]
    [Tooltip("Nama scene stage 1. Harus sudah ada di File > Build Settings.")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Tombol kiri (Play, Setting, Exit)")]
    [Tooltip("Objek induk semua tombol kiri (Play, Continue, Setting, Exit). Tiap anak langsungnya yang aktif meluncur sendiri-sendiri. " +
             "Ukuran (Rect) objek ini tidak berpengaruh pada jarak luncur.")]
    [SerializeField] private RectTransform buttonGroup;
    [Tooltip("Opsional: urutan tombol meluncur, dari yang pertama ke yang terakhir. " +
             "Kosong = otomatis dari atas ke bawah, dan kiri ke kanan kalau satu baris (mis. Play lalu Continue).")]
    [SerializeField] private RectTransform[] slideOrder;
    [Tooltip("Jeda antar tombol saat meluncur (detik). 0.06 = 60 milidetik. 0 = semua bersamaan.")]
    [SerializeField] private float staggerDelay = 0.06f;
    [Tooltip("Dua tombol dengan selisih tinggi di bawah angka ini dianggap satu baris (satuan Canvas).")]
    [SerializeField] private float rowTolerance = 30f;
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
    // tombol yang meluncur: posisi akhir (tampil) dan jarak ke luar layar dicatat sekali, saat pertama dipakai
    RectTransform[] slideButtons;
    Vector2[] shownPos;
    float[] hiddenOffset;
    bool slideReady;
    readonly Vector3[] cornersA = new Vector3[4];
    readonly Vector3[] cornersB = new Vector3[4];

    Graphic[] continueGraphics;
    Color[] continueOriginalColors;

    void Start()
    {
        Time.timeScale = 1f; // jaga-jaga kalau datang dari scene yang sempat di-pause

        if (buttonGroup != null)
            buttonsCanvasGroup = GetOrAddGroup(buttonGroup.gameObject);

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
        CoinSystem.ResetRun();
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
        CoinSystem.LoadSnapshot(save.coins);
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
        CoinSystem.ResetRun();
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
        EnsureSlideSetup();
        yield return StartCoroutine(Staggered(SlideOutOne));
    }

    IEnumerator ButtonsIn()
    {
        EnsureSlideSetup();
        yield return StartCoroutine(Staggered(SlideInOne));
    }

    // Menjalankan 'make' untuk tiap tombol dengan jeda i * staggerDelay (urut dari yang pertama),
    // lalu menunggu semuanya selesai.
    IEnumerator Staggered(Func<int, IEnumerator> make)
    {
        int pending = 0;
        for (int k = 0; k < slideButtons.Length; k++)
        {
            int i = k;
            if (slideButtons[i] == null) continue;
            pending++;
            StartCoroutine(RunDelayed(i, make, () => pending--));
        }
        while (pending > 0) yield return null;
    }

    IEnumerator RunDelayed(int i, Func<int, IEnumerator> make, Action done)
    {
        float delay = i * Mathf.Max(0f, staggerDelay);
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        yield return StartCoroutine(make(i));
        done();
    }

    IEnumerator SlideOutOne(int i)
    {
        RectTransform rt = slideButtons[i];
        Vector2 shown = shownPos[i];
        Vector2 back = shown + Vector2.right * anticipationDistance;
        Vector2 hidden = shown + Vector2.left * hiddenOffset[i];

        // 1) mundur sedikit ke kanan
        yield return Tween(anticipationTime, t =>
            rt.anchoredPosition = Vector2.LerpUnclamped(shown, back, EaseOutQuad(t)));

        // 2) meluncur halus ke kiri sampai keluar layar (mulai pelan, makin cepat)
        yield return Tween(slideOutTime, t =>
            rt.anchoredPosition = Vector2.LerpUnclamped(back, hidden, EaseInCubic(t)));

        rt.anchoredPosition = hidden;
    }

    IEnumerator SlideInOne(int i)
    {
        RectTransform rt = slideButtons[i];
        Vector2 shown = shownPos[i];
        Vector2 hidden = shown + Vector2.left * hiddenOffset[i];

        // masuk dari kiri, cepat di awal lalu melambat halus di posisi akhir
        yield return Tween(slideInTime, t =>
            rt.anchoredPosition = Vector2.LerpUnclamped(hidden, shown, EaseOutCubic(t)));

        rt.anchoredPosition = shown;
    }

    // Dipanggil sekali, saat semua tombol masih di posisi tampil: kumpulkan tombol, urutkan,
    // catat posisi tampil, dan hitung jarak tiap tombol ke luar layar.
    void EnsureSlideSetup()
    {
        if (slideReady) return;
        slideReady = true;

        slideButtons = BuildSlideList();
        shownPos = new Vector2[slideButtons.Length];
        hiddenOffset = new float[slideButtons.Length];

        Canvas canvas = buttonGroup.GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        if (canvas != null) ((RectTransform)canvas.transform).GetWorldCorners(cornersA);   // [0] = kiri bawah canvas
        float scale = canvas != null ? Mathf.Max(0.0001f, canvas.transform.lossyScale.x) : 1f;

        for (int i = 0; i < slideButtons.Length; i++)
        {
            if (slideButtons[i] == null) continue;
            shownPos[i] = slideButtons[i].anchoredPosition;

            // jarak (satuan Canvas) sampai tepi KANAN tombol ini melewati tepi KIRI layar, ditambah margin
            if (canvas != null)
            {
                slideButtons[i].GetWorldCorners(cornersB);                                  // [2] = kanan atas tombol
                hiddenOffset[i] = Mathf.Max(0f, (cornersB[2].x - cornersA[0].x) / scale) + offscreenMargin;
            }
            else hiddenOffset[i] = 1200f;
        }
    }

    // Urutan tombol: pakai Slide Order kalau diisi. Kalau kosong, semua anak langsung ButtonGroup yang aktif,
    // dari atas ke bawah; tombol yang satu baris (selisih tinggi <= Row Tolerance) dari kiri ke kanan.
    RectTransform[] BuildSlideList()
    {
        if (slideOrder != null && slideOrder.Length > 0) return slideOrder;

        var list = new List<RectTransform>();
        foreach (Transform child in buttonGroup)
            if (child.gameObject.activeSelf && child is RectTransform rt) list.Add(rt);

        list.Sort((a, b) => b.localPosition.y.CompareTo(a.localPosition.y));   // atas dulu

        var result = new List<RectTransform>();
        int idx = 0;
        while (idx < list.Count)
        {
            float rowY = list[idx].localPosition.y;
            var row = new List<RectTransform>();
            while (idx < list.Count && Mathf.Abs(list[idx].localPosition.y - rowY) <= rowTolerance)
            {
                row.Add(list[idx]);
                idx++;
            }
            row.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));  // kiri dulu
            result.AddRange(row);
        }
        return result.ToArray();
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