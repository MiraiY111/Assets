using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Bendera global: true selama stage di-pause oleh PauseMenu. Script lain boleh membacanya.</summary>
public static class GamePause
{
    public static bool IsPaused { get; internal set; }
}

/// <summary>
/// Pause untuk stage, dengan animasi. Pasang di objek kosong yang SELALU aktif (mis. "PauseManager").
/// Pause menghentikan game lewat Time.timeScale = 0, jadi SEMUA animasi di sini memakai waktu unscaled.
///
/// Susunan yang diharapkan:
///   PausePanel (nonaktif di awal)  = gambar peredup selayar penuh (Raycast Target aktif, menahan klik ke HUD)
///       PauseButtons               = objek induk, berisi tombol Resume / Settings / Main Menu
///   SettingsPanel (nonaktif di awal) = panel Setting (volume, Close)
/// Letakkan ketiga tombol di posisi AKHIR (di tengah) saat edit; animasi mulai dari luar layar lalu berhenti di sana.
/// JANGAN pakai Layout Group / Content Size Fitter pada PauseButtons: posisi tombol akan ditimpa dan animasi rusak.
///
/// Pause   : tombol turun dari atas ke tengah, satu per satu.
/// Settings: panel Setting membesar dari skala 0; tombol mundur sedikit ke kanan lalu meluncur ke kiri keluar layar,
///           bergantian dari atas ke bawah (jeda Stagger Delay).
/// Close   : panel mengecil; tombol meluncur masuk dari kiri, bergantian dari atas ke bawah.
/// Resume  : tombol naik ke atas keluar layar, baru game lanjut.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    const string VolumeKey = "MasterVolume";

    [Header("Panel")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Objek induk ketiga tombol. Dipakai untuk menahan klik selama animasi.")]
    [SerializeField] private GameObject pauseButtons;
    [Tooltip("Urut dari atas ke bawah: Resume, Settings, Main Menu.")]
    [SerializeField] private RectTransform[] buttons;
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Opsional: slider volume di panel Setting stage ini.")]
    [SerializeField] private Slider volumeSlider;

    [Header("Animasi tombol")]
    [Tooltip("Lama tiap tombol turun dari atas saat Pause (detik).")]
    [SerializeField] private float enterTime = 0.4f;
    [Tooltip("Lama tiap tombol naik saat Resume (detik). 0 = langsung hilang.")]
    [SerializeField] private float exitTime = 0.25f;
    [Tooltip("Jeda antar tombol (detik). 0.06 = 60 milidetik. 0 = semua bersamaan.")]
    [SerializeField] private float staggerDelay = 0.06f;
    [Tooltip("Mundur sedikit ke kanan sebelum meluncur ke kiri (satuan Canvas).")]
    [SerializeField] private float anticipationDistance = 40f;
    [SerializeField] private float anticipationTime = 0.12f;
    [SerializeField] private float slideOutTime = 0.4f;
    [SerializeField] private float slideInTime = 0.45f;
    [Tooltip("Tambahan jarak di luar tepi layar supaya tombol benar-benar tidak kelihatan.")]
    [SerializeField] private float offscreenMargin = 60f;

    [Header("Animasi panel Setting")]
    [Tooltip("Pivot panel sebaiknya 0.5, 0.5 supaya membesar dari tengah.")]
    [SerializeField] private float panelOpenTime = 0.35f;
    [SerializeField] private float panelCloseTime = 0.25f;
    [Tooltip("Seberapa melewati ukuran penuh saat membuka (0 = tanpa membal, 1.2 = sedikit membal).")]
    [SerializeField] private float panelOvershoot = 1.2f;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Jeda sebelum aksi dijalankan supaya efek klik (sprite pressed/suara) sempat terlihat (detik).")]
    [SerializeField] private float clickDelay = 0.1f;

    [Header("Terkait")]
    [Tooltip("Opsional. Selama mode edit UI terbuka, tombol Back/Escape diabaikan.")]
    [SerializeField] private UICustomizePanel customize;
    [Tooltip("Opsional. Kalau diisi, Back/Escape di panel Setting memicu konfirmasi simpan, bukan langsung menutup.")]
    [SerializeField] private SettingsController settingsController;
    [Tooltip("Kosong = dicari otomatis. Pause tidak bisa dipakai saat Player mati.")]
    [SerializeField] private PlayerHealth player;
    [Tooltip("Kosong = dicari otomatis. Pause tidak bisa dipakai setelah stage selesai.")]
    [SerializeField] private WaveSpawner spawner;

    [Header("Opsi")]
    [Tooltip("Tombol Back Android (terbaca sebagai Escape) membuka pause, menutup Setting, atau melanjutkan.")]
    [SerializeField] private bool backKeyTogglesPause = true;
    [Tooltip("Pause otomatis kalau aplikasi dikecilkan (tombol Home).")]
    [SerializeField] private bool pauseWhenAppLosesFocus = true;

    bool paused;
    bool settingsOpen;
    bool busy;

    RectTransform panelRect;
    CanvasGroup panelGroup;
    CanvasGroup buttonsGroup;
    Vector2[] shownPos;
    float[] leftOffset;
    float[] upOffset;
    bool offsetsReady;
    readonly Vector3[] cornersA = new Vector3[4];
    readonly Vector3[] cornersB = new Vector3[4];

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();

        GamePause.IsPaused = false;

        int n = buttons != null ? buttons.Length : 0;
        shownPos = new Vector2[n];
        leftOffset = new float[n];
        upOffset = new float[n];
        for (int i = 0; i < n; i++)
            if (buttons[i] != null) shownPos[i] = buttons[i].anchoredPosition;

        if (pauseButtons != null) buttonsGroup = GetOrAddGroup(pauseButtons);

        if (settingsPanel != null)
        {
            panelRect = settingsPanel.GetComponent<RectTransform>();
            panelGroup = GetOrAddGroup(settingsPanel);
            panelRect.localScale = Vector3.zero;
            settingsPanel.SetActive(false);
        }

        if (pausePanel != null) pausePanel.SetActive(false);

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, 1f));
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    void OnDestroy()
    {
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);
        if (paused) { Time.timeScale = 1f; GamePause.IsPaused = false; }
    }

    static CanvasGroup GetOrAddGroup(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        if (!backKeyTogglesPause || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (busy) return;
        if (customize != null && customize.IsOpen) return;

        if (!paused) Pause();
        else if (settingsOpen)
        {
            if (settingsController != null) settingsController.RequestClose();
            else CloseSettings();
        }
        else Resume();
    }

    void OnApplicationPause(bool pausedByOs)
    {
        if (pausedByOs && pauseWhenAppLosesFocus) Pause();
    }

    bool CanPause()
    {
        if (player != null && player.IsDead) return false;
        if (spawner != null && spawner.CurrentPhase == WaveSpawner.Phase.Done) return false;
        return true;
    }

    // ---------- aksi (dipasang di OnClick) ----------
    public void Pause()
    {
        if (paused || busy || !CanPause()) return;
        if (pausePanel == null)
        {
            Debug.LogWarning($"{name}: Pause Panel belum diisi.");
            return;
        }

        paused = true;
        settingsOpen = false;
        GamePause.IsPaused = true;
        Time.timeScale = 0f;
        StartCoroutine(EnterRoutine());
    }

    public void Resume()
    {
        if (!paused || busy || settingsOpen) return;
        StartCoroutine(ResumeRoutine());
    }

    public void OpenSettings()
    {
        if (!paused || busy || settingsOpen || settingsPanel == null) return;
        StartCoroutine(OpenSettingsRoutine());
    }

    public void CloseSettings()
    {
        if (!paused || busy || !settingsOpen) return;
        StartCoroutine(CloseSettingsRoutine());
    }

    public void GoToMainMenu()
    {
        if (busy) return;
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning($"{name}: Main Menu Scene Name kosong.");
            return;
        }
        StartCoroutine(GoToMainMenuRoutine());
    }

    void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(VolumeKey, v);
    }

    // ---------- urutan ----------
    IEnumerator EnterRoutine()
    {
        busy = true;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
        SetInteractable(buttonsGroup, false);

        // posisi akhir dulu (untuk mengukur jarak), baru dipindah ke atas layar
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].anchoredPosition = shownPos[i];
        EnsureOffsets();
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].anchoredPosition = shownPos[i] + Vector2.up * upOffset[i];

        yield return StartCoroutine(Staggered(EnterOne));

        SetInteractable(buttonsGroup, true);
        busy = false;
    }

    IEnumerator ResumeRoutine()
    {
        busy = true;
        SetInteractable(buttonsGroup, false);

        if (exitTime > 0f) yield return StartCoroutine(Staggered(ExitOne));

        pausePanel.SetActive(false);
        paused = false;
        GamePause.IsPaused = false;
        Time.timeScale = 1f;
        busy = false;
    }

    IEnumerator OpenSettingsRoutine()
    {
        busy = true;

        SetInteractable(buttonsGroup, false);
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);

        settingsPanel.SetActive(true);
        panelRect.localScale = Vector3.zero;
        SetInteractable(panelGroup, false);   // baru bisa diklik setelah selesai membesar

        Coroutine a = StartCoroutine(Staggered(SlideOutOne));
        Coroutine b = StartCoroutine(PanelScale(true));
        yield return a;
        yield return b;

        SetInteractable(panelGroup, true);
        settingsOpen = true;
        busy = false;
    }

    IEnumerator CloseSettingsRoutine()
    {
        busy = true;

        SetInteractable(panelGroup, false);
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);

        Coroutine a = StartCoroutine(Staggered(SlideInOne));
        Coroutine b = StartCoroutine(PanelScale(false));
        yield return b;
        yield return a;

        settingsPanel.SetActive(false);
        SetInteractable(buttonsGroup, true);
        settingsOpen = false;
        busy = false;
    }

    IEnumerator GoToMainMenuRoutine()
    {
        busy = true;
        SetInteractable(buttonsGroup, false);
        if (clickDelay > 0f) yield return new WaitForSecondsRealtime(clickDelay);

        paused = false;
        GamePause.IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    static void SetInteractable(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    // ---------- animasi tombol ----------
    // Menjalankan 'make' untuk tiap tombol dengan jeda i * staggerDelay (urut dari atas), menunggu semuanya selesai.
    IEnumerator Staggered(Func<int, IEnumerator> make)
    {
        int pending = 0;
        for (int k = 0; k < buttons.Length; k++)
        {
            int i = k;
            if (buttons[i] == null) continue;
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

    IEnumerator EnterOne(int i)
    {
        RectTransform rt = buttons[i];
        Vector2 shown = shownPos[i];
        Vector2 from = shown + Vector2.up * upOffset[i];

        yield return Tween(enterTime, t =>
            rt.anchoredPosition = Vector2.LerpUnclamped(from, shown, EaseOutCubic(t)));
        rt.anchoredPosition = shown;
    }

    IEnumerator ExitOne(int i)
    {
        RectTransform rt = buttons[i];
        Vector2 shown = shownPos[i];
        Vector2 to = shown + Vector2.up * upOffset[i];

        yield return Tween(exitTime, t =>
            rt.anchoredPosition = Vector2.LerpUnclamped(shown, to, EaseInCubic(t)));
        rt.anchoredPosition = to;
    }

    IEnumerator SlideOutOne(int i)
    {
        RectTransform rt = buttons[i];
        Vector2 shown = shownPos[i];
        Vector2 back = shown + Vector2.right * anticipationDistance;
        Vector2 hidden = shown + Vector2.left * leftOffset[i];

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
        RectTransform rt = buttons[i];
        Vector2 shown = shownPos[i];
        Vector2 hidden = shown + Vector2.left * leftOffset[i];

        // masuk dari kiri, cepat di awal lalu melambat halus di posisi akhir
        yield return Tween(slideInTime, t =>
            rt.anchoredPosition = Vector2.LerpUnclamped(hidden, shown, EaseOutCubic(t)));
        rt.anchoredPosition = shown;
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

    // Jarak (satuan Canvas) supaya tiap tombol keluar dari tepi KIRI layar (kiri) atau dari tepi ATAS layar (atas).
    // Dihitung sekali, saat semua tombol ada di posisi akhir.
    void EnsureOffsets()
    {
        if (offsetsReady) return;

        Canvas canvas = pausePanel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            for (int i = 0; i < buttons.Length; i++) { leftOffset[i] = 1200f; upOffset[i] = 800f; }
            offsetsReady = true;
            return;
        }
        canvas = canvas.rootCanvas;

        ((RectTransform)canvas.transform).GetWorldCorners(cornersA);   // [0] kiri bawah, [1] kiri atas
        float sx = Mathf.Max(0.0001f, canvas.transform.lossyScale.x);
        float sy = Mathf.Max(0.0001f, canvas.transform.lossyScale.y);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].GetWorldCorners(cornersB);                      // [0] kiri bawah, [2] kanan atas
            leftOffset[i] = Mathf.Max(0f, (cornersB[2].x - cornersA[0].x) / sx) + offscreenMargin;
            upOffset[i] = Mathf.Max(0f, (cornersA[1].y - cornersB[0].y) / sy) + offscreenMargin;
        }
        offsetsReady = true;
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