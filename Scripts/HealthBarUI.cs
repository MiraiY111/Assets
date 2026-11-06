using UnityEngine;
using TMPro;
using HeyHeyThere.PixelBarsFree;

/// <summary>
/// HUD HP bar player memakai PixelStatBar (kit Pixel Bars Free) + teks angka HP + getar saat kena damage.
/// Pasang di objek yang sama dengan PixelStatBar. Max Value dan Value bar mengikuti PlayerHealth,
/// jadi bar tetap benar kalau MaxHP berubah (skill, upgrade).
/// PixelStatBar sendiri sudah mengedipkan isi bar dan menurunkan jejak putih saat HP turun.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;   // kosong = dicari otomatis
    [SerializeField] private PixelStatBar bar;      // kosong = diambil dari objek ini
    [Tooltip("Opsional. Kosong = diambil dari objek ini kalau ada.")]
    [SerializeField] private UIShake shake;

    [Header("Teks HP (opsional)")]
    [Tooltip("Sebaiknya anak dari objek bar ini, supaya ikut bergetar bersama bar.")]
    [SerializeField] private TMP_Text hpText;
    [Tooltip("{0} = HP sekarang, {1} = HP maksimum. Contoh: \"{0}\" atau \"{0}/{1}\"")]
    [SerializeField] private string format = "{0}";

    int lastHP;

    void Reset() { bar = GetComponent<PixelStatBar>(); }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        if (bar == null) bar = GetComponent<PixelStatBar>();
        if (shake == null) shake = GetComponent<UIShake>();

        if (player == null || bar == null)
        {
            Debug.LogWarning($"{name}: HealthBarUI belum lengkap (player / bar).");
            enabled = false;
            return;
        }

        // PixelStatBar membuat bingkai dan isi sebagai anak; teks harus paling akhir supaya tampil di depan
        if (hpText != null)
        {
            hpText.raycastTarget = false;
            if (hpText.transform.parent == bar.transform) hpText.transform.SetAsLastSibling();
        }

        player.OnHealthChanged += Refresh;
        lastHP = player.CurrentHP;
        Apply(player.CurrentHP, player.MaxHP);
        bar.Settle();   // awal langsung pas, tanpa kedip
    }

    void OnDestroy()
    {
        if (player != null) player.OnHealthChanged -= Refresh;
    }

    void Refresh(int current, int max)
    {
        bool damaged = current < lastHP;
        lastHP = current;

        Apply(current, max);
        if (damaged && shake != null) shake.Play();
    }

    void Apply(int current, int max)
    {
        bar.maxValue = max;     // Max dulu: PixelStatBar menjepit Value ke Max Value
        bar.value = current;

        if (hpText != null) hpText.text = string.Format(format, current, max);
    }
}