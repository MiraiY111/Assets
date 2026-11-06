using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Tombol UI dengan dua sprite: normal, dan pressed selama ditekan/ditahan.
/// Pasang di objek tombol (yang punya Image + Button). Set Transition di Button menjadi None.
/// Sprite kembali normal saat dilepas, atau saat jari/kursor bergeser keluar dari tombol
/// (klik juga dibatalkan kalau dilepas di luar tombol, sama seperti Button biasa).
/// </summary>
[RequireComponent(typeof(Image))]
public class PressSpriteButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite pressedSprite;

    bool held;

    void Reset() { image = GetComponent<Image>(); }

    void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        // kalau Normal Sprite dikosongkan, pakai sprite yang sedang terpasang di Image
        if (normalSprite == null && image != null) normalSprite = image.sprite;
    }

    void OnEnable() { held = false; SetSprite(normalSprite); }

    public void OnPointerDown(PointerEventData e) { held = true; SetSprite(pressedSprite); }

    public void OnPointerUp(PointerEventData e) { held = false; SetSprite(normalSprite); }

    public void OnPointerExit(PointerEventData e) { SetSprite(normalSprite); }

    // masuk lagi saat masih ditahan: tampil pressed lagi
    public void OnPointerEnter(PointerEventData e) { if (held) SetSprite(pressedSprite); }

    void SetSprite(Sprite s)
    {
        if (image != null && s != null) image.sprite = s;
    }
}