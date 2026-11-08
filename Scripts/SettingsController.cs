using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Mengatur "perubahan yang belum disimpan" di panel Setting, dipakai bersama oleh Main Menu dan stage (pause).
/// Pasang di objek kosong yang SELALU aktif (mis. "SettingsManager"), bukan di panel Setting-nya sendiri
/// (panel itu sempat dimatikan, misalnya saat mode edit UI).
///
/// Volume : slider langsung terdengar (AudioListener.volume), tetapi baru ditulis ke PlayerPrefs saat Save.
///          Nilai tersimpan diambil sekali di Start dan hanya berubah lewat Save(). Tidak ada script lain
///          yang boleh menulis volume ke PlayerPrefs.
/// Tata letak UI : lewat UICustomizePanel (CommitAll / RevertAll / HasChanges).
///
/// Alur:
///   tombol Save     -> Save()
///   tombol Close    -> RequestClose()   (tanpa perubahan: langsung menutup; ada perubahan: muncul dialog)
///   dialog Yes      -> simpan lalu menutup
///   dialog No       -> semua perubahan dikembalikan ke yang tersimpan, lalu menutup
/// Tombol Yes dan No di dialog dihubungkan oleh script ini, tidak perlu diisi di OnClick.
/// </summary>
public class SettingsController : MonoBehaviour
{
    const string VolumeKey = "MasterVolume";

    [Header("Pengaturan")]
    [Tooltip("Slider volume 0-1 di panel Setting.")]
    [SerializeField] private Slider volumeSlider;
    [Tooltip("Opsional. Kosong = tidak ada pengaturan tata letak UI.")]
    [SerializeField] private UICustomizePanel customize;

    [Header("Dialog konfirmasi")]
    [Tooltip("Panel 'Do you want to save changes?'. Nonaktif di awal. Harus berada di depan panel Setting dan punya gambar selayar penuh " +
             "(Raycast Target aktif) di belakang kotaknya, supaya panel Setting tidak bisa diklik selama dialog tampil.")]
    [SerializeField] private GameObject confirmDialog;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Setelah selesai")]
    [Tooltip("Dipanggil setelah Close tanpa perubahan, atau setelah menjawab Yes/No. Arahkan ke MainMenu.CloseSettings (Main Menu) " +
             "atau PauseMenu.CloseSettings (stage).")]
    [SerializeField] private UnityEvent onClose;

    float savedVolume = 1f;
    bool dialogOpen;
    bool closing;   // true selama event On Close berjalan; mencegah pemanggilan berulang tanpa henti

    void Start()
    {
        // nilai tersimpan diambil sekali di sini; setelahnya hanya berubah lewat Save()
        savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(savedVolume);
        AudioListener.volume = savedVolume;

        if (confirmDialog != null) confirmDialog.SetActive(false);

        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (yesButton != null) yesButton.onClick.AddListener(OnYes);
        if (noButton != null) noButton.onClick.AddListener(OnNo);
    }

    void OnDestroy()
    {
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if (yesButton != null) yesButton.onClick.RemoveListener(OnYes);
        if (noButton != null) noButton.onClick.RemoveListener(OnNo);
    }

    /// <summary>True kalau ada perubahan (volume atau tata letak UI) yang belum disimpan.</summary>
    public bool HasChanges
    {
        get
        {
            if (volumeSlider != null && Mathf.Abs(volumeSlider.value - savedVolume) > 0.001f) return true;
            if (customize != null && customize.HasChanges) return true;
            return false;
        }
    }

    /// <summary>
    /// Tidak wajib lagi: nilai tersimpan sudah diambil di Start. Dibiarkan supaya OnClick lama
    /// yang menunjuk ke sini tidak rusak.
    /// </summary>
    public void BeginSession()
    {
        dialogOpen = false;
        if (confirmDialog != null) confirmDialog.SetActive(false);
    }

    public void Save()
    {
        if (volumeSlider != null)
        {
            savedVolume = volumeSlider.value;
            PlayerPrefs.SetFloat(VolumeKey, savedVolume);
        }
        if (customize != null) customize.CommitAll();
        PlayerPrefs.Save();
    }

    /// <summary>Tombol Close. Ada perubahan: tampilkan dialog. Tidak ada: langsung menutup.</summary>
    public void RequestClose()
    {
        if (closing)
        {
            Debug.LogWarning($"{name}: RequestClose dipanggil lagi dari event On Close. " +
                             "Field On Close harus menunjuk ke MainMenu.CloseSettings (atau PauseMenu.CloseSettings), bukan ke RequestClose.");
            return;
        }
        if (dialogOpen) return;
        if (customize != null && customize.IsOpen) return;   // sedang mode edit: keluar dulu lewat Done

        if (HasChanges && confirmDialog != null)
        {
            dialogOpen = true;
            confirmDialog.SetActive(true);
            return;
        }

        FinishClose();
    }

    void OnYes()
    {
        Save();
        FinishClose();
    }

    void OnNo()
    {
        Revert();
        FinishClose();
    }

    void Revert()
    {
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(savedVolume);
        AudioListener.volume = savedVolume;
        if (customize != null) customize.RevertAll();
    }

    void FinishClose()
    {
        dialogOpen = false;
        if (confirmDialog != null) confirmDialog.SetActive(false);
        closing = true;
        try { onClose?.Invoke(); }
        finally { closing = false; }
    }

    // slider langsung terdengar, tetapi belum disimpan
    void OnVolumeChanged(float v)
    {
        AudioListener.volume = v;
    }
}