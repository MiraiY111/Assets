using UnityEngine;

public class SortByY : MonoBehaviour
{
    [Tooltip("Geser titik acuan urutan. Negatif = acuan turun ke arah kaki. Atur sampai urutannya terasa benar.")]
    public float footOffset = -0.3f;

    [Tooltip("Presisi urutan. Naikkan kalau dua objek yang sangat dekat masih berebut depan.")]
    public float precision = 100f;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) Debug.LogWarning($"{name}: SpriteRenderer tidak ditemukan, SortByY tidak aktif.");
    }

    void LateUpdate()
    {
        if (sr == null) return;
        sr.sortingOrder = Mathf.RoundToInt(-(transform.position.y + footOffset) * precision);
    }
}