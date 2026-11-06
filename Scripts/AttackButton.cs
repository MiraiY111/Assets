using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Image image;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite pressedSprite;

    void Reset() { image = GetComponent<Image>(); }

    public void OnPointerDown(PointerEventData e)
    {
        image.sprite = pressedSprite;
        player.OnAttackButton();
    }

    public void OnPointerUp(PointerEventData e)
    {
        image.sprite = normalSprite;
    }
}