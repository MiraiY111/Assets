using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Layar Stage Cleared. Muncul setelah WaveSpawner.OnArenaCompleted,
/// yaitu setelah Player selesai berjalan keluar map (PlayerExit).
/// Pasang di objek yang SELALU aktif (mis. GameObject kosong "StageClearedManager"), bukan di panelnya sendiri,
/// karena panel dimatikan sampai stage selesai.
/// Next Stage dan Main Menu: kalau nama scene dikosongkan, tombol hanya bisa diklik tanpa efek.
/// </summary>
public class StageClearedUI : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Panel Stage Cleared (sprite + 2 tombol). Dimatikan sampai stage selesai. Beri CanvasGroup supaya bisa fade.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button nextStageButton;
    [SerializeField] private Button mainMenuButton;
    [Tooltip("Kosong = dicari otomatis")]
    [SerializeField] private WaveSpawner spawner;

    [Header("Scene")]
    [Tooltip("Kosong = tombol Next Stage belum melakukan apa-apa (sekarang baru ada stage 1).")]
    [SerializeField] private string nextStageSceneName = "";
    [Tooltip("Kosong = tombol Main Menu belum melakukan apa-apa.")]
    [SerializeField] private string mainMenuSceneName = "";

    [Header("Tampilan")]
    [SerializeField] private float showDelay = 0.5f;
    [SerializeField] private float fadeDuration = 0.6f;

    CanvasGroup group;
    bool shown;
    bool loading;

    void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();
        if (spawner == null || panel == null)
        {
            Debug.LogWarning($"{name}: StageClearedUI belum lengkap (spawner / panel).");
            enabled = false;
            return;
        }

        group = panel.GetComponent<CanvasGroup>();
        panel.SetActive(false);

        spawner.OnArenaCompleted += HandleArenaCompleted;
        if (nextStageButton != null) nextStageButton.onClick.AddListener(NextStage);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(MainMenu);
    }

    void OnDestroy()
    {
        if (spawner != null) spawner.OnArenaCompleted -= HandleArenaCompleted;
        if (nextStageButton != null) nextStageButton.onClick.RemoveListener(NextStage);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(MainMenu);
    }

    void HandleArenaCompleted()
    {
        if (shown) return;
        shown = true;
        StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);

        panel.SetActive(true);
        if (group == null) yield break;

        group.alpha = 0f;
        group.interactable = false;      // tombol baru bisa diklik setelah terlihat penuh
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
            yield return null;
        }
        group.alpha = 1f;
        group.interactable = true;
    }

    public void NextStage()
    {
        if (loading) return;

        if (string.IsNullOrEmpty(nextStageSceneName))
        {
            Debug.Log("[StageClearedUI] Stage berikutnya belum ada, tombol belum melakukan apa-apa.");
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }

    public void MainMenu()
    {
        if (loading) return;

        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.Log("[StageClearedUI] Main Menu belum dibuat, tombol belum melakukan apa-apa.");
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}