using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Tombol Dash di UI. Pasang di objek tombol (yang punya Image).
///
/// - Ditekan: memanggil PlayerController.TryDash(). Sprite berganti ke 'pressed' hanya kalau dash benar-benar jalan.
/// - Selama cooldown: penutup abu-abu berputar seperti jam (menghilang searah jarum jam dari atas) dan
///   angka sisa waktu tampil di tengah tombol. Selesai cooldown, keduanya hilang sendiri.
///
/// Butuh 3 anggota di PlayerController (lihat DashPatch.txt):
///   bool TryDash(), float DashRemaining, float DashCooldown
/// </summary>
[RequireComponent(typeof(Image))]
public class DashButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private PlayerController player;    // kosong = dicari otomatis
    [SerializeField] private Image image;                // kosong = Image di objek ini
    [SerializeField] private Sprite normalSprite;        // kosong = sprite yang terpasang di Image
    [SerializeField] private Sprite pressedSprite;

    [Header("Cooldown")]
    [Tooltip("Image anak yang menutupi tombol. Isi Source Image dengan sprite tombol yang sama, warna abu-abu/hitam transparan.")]
    [SerializeField] private Image cooldownOverlay;
    [Tooltip("Teks TMP anak untuk angka sisa cooldown. Kosong = tanpa angka.")]
    [SerializeField] private TMP_Text cooldownText;

    int lastShownKey = int.MinValue;

    void Reset() { image = GetComponent<Image>(); }

    void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (normalSprite == null && image != null) normalSprite = image.sprite;

        if (cooldownOverlay != null)
        {
            // penutup berputar seperti jam: terisi dari atas, menghilang searah jarum jam
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            cooldownOverlay.fillClockwise = false;
            cooldownOverlay.raycastTarget = false;   // supaya sentuhan tetap sampai ke tombol
            cooldownOverlay.enabled = false;
        }

        if (cooldownText != null)
        {
            cooldownText.raycastTarget = false;
            cooldownText.enabled = false;
        }
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null) Debug.LogWarning($"{name}: PlayerController tidak ditemukan.");
    }

    void OnDisable() { SetSprite(normalSprite); }

    void Update()
    {
        if (player == null) return;

        float remain = player.DashRemaining;
        float total = Mathf.Max(0.01f, player.DashCooldown);
        bool cooling = remain > 0f;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.enabled = cooling;
            if (cooling) cooldownOverlay.fillAmount = Mathf.Clamp01(remain / total);
        }

        if (cooldownText != null)
        {
            cooldownText.enabled = cooling;
            if (!cooling) { lastShownKey = int.MinValue; return; }

            // >= 1 detik: bilangan bulat (dibulatkan ke atas); < 1 detik: satu desimal
            int key = remain >= 1f ? Mathf.CeilToInt(remain) : -Mathf.CeilToInt(remain * 10f);
            if (key != lastShownKey)
            {
                lastShownKey = key;
                cooldownText.text = remain >= 1f ? key.ToString() : (-key / 10f).ToString("0.0");
            }
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (player == null) return;
        if (player.TryDash()) SetSprite(pressedSprite);
    }

    public void OnPointerUp(PointerEventData e) { SetSprite(normalSprite); }

    public void OnPointerExit(PointerEventData e) { SetSprite(normalSprite); }

    void SetSprite(Sprite s)
    {
        if (image != null && s != null) image.sprite = s;
    }
}