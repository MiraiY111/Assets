using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Layar Defeated saat Player mati.
/// Pasang di objek yang SELALU aktif (mis. Canvas atau objek kosong GameOverManager), bukan di panelnya sendiri,
/// karena panel dimatikan sampai Player mati.
/// Saat Player mati: save dihapus (SaveGameStore.Delete), jadi Continue di Main Menu abu-abu.
/// Restart  : memuat ulang stage (kosong = scene yang sedang aktif, sekarang stage 1).
/// Main Menu: sementara hanya bisa diklik tanpa efek; isi Main Menu Scene Name nanti kalau menu sudah dibuat.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Panel Defeated (berisi sprite Defeated + 2 tombol). Dimatikan sampai Player mati. Beri komponen CanvasGroup supaya bisa fade.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [Tooltip("Kosong = dicari otomatis")]
    [SerializeField] private PlayerHealth player;
    [Tooltip("Opsional: dihentikan saat Player mati supaya monster tidak terus muncul di belakang layar Defeated.")]
    [SerializeField] private WaveSpawner spawner;

    [Header("Scene")]
    [Tooltip("Kosong = muat ulang scene yang sedang aktif. Isi nama scene stage 1 kalau mau eksplisit.")]
    [SerializeField] private string stageSceneName = "";
    [Tooltip("Kosong = tombol Main Menu belum melakukan apa-apa (hanya mencatat log).")]
    [SerializeField] private string mainMenuSceneName = "";

    [Header("Tampilan")]
    [SerializeField] private float showDelay = 0.3f;
    [SerializeField] private float fadeDuration = 0.6f;

    CanvasGroup group;
    bool shown;
    bool loading;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        if (player == null || panel == null)
        {
            Debug.LogWarning($"{name}: GameOverUI belum lengkap (player / panel).");
            enabled = false;
            return;
        }

        group = panel.GetComponent<CanvasGroup>();
        panel.SetActive(false);

        player.OnDied += HandlePlayerDied;
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(MainMenu);
    }

    void OnDestroy()
    {
        if (player != null) player.OnDied -= HandlePlayerDied;
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(MainMenu);
    }

    void HandlePlayerDied()
    {
        if (shown) return;
        shown = true;
        SaveGameStore.Delete();   // mati = semua save hilang, tombol Continue di Main Menu jadi abu-abu
        if (spawner != null) spawner.StopWaves();
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

    public void Restart()
    {
        if (loading) return;
        loading = true;

        Time.timeScale = 1f;
        string target = string.IsNullOrEmpty(stageSceneName) ? SceneManager.GetActiveScene().name : stageSceneName;
        SceneManager.LoadScene(target);
    }

    public void MainMenu()
    {
        if (loading) return;

        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.Log("[GameOverUI] Main Menu belum dibuat, tombol belum melakukan apa-apa.");
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}