using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pasang di tombol "UI Customize" (atau objek mana pun), lalu arahkan OnClick tombol ke Open().
/// Mengingat scene asal, supaya tombol Back di layar UICustomize bisa kembali ke sana.
/// Catatan: membuka dari dalam stage meninggalkan scene stage itu, jadi progres run di stage itu hilang
/// (kembali = stage dimuat ulang dari awal). Untuk sekarang, pasang hanya di Main Menu.
/// </summary>
public class UICustomizeLauncher : MonoBehaviour
{
    /// <summary>Scene yang membuka layar UICustomize terakhir.</summary>
    public static string ReturnScene { get; private set; } = "MainMenu";

    [Tooltip("Nama scene UICustomize. Harus ada di File > Build Settings.")]
    [SerializeField] private string customizeSceneName = "UICustomize";

    public void Open()
    {
        if (string.IsNullOrEmpty(customizeSceneName))
        {
            Debug.LogWarning($"{name}: Customize Scene Name kosong.");
            return;
        }

        ReturnScene = SceneManager.GetActiveScene().name;
        Time.timeScale = 1f;
        SceneManager.LoadScene(customizeSceneName);
    }
}