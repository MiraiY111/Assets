using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Membuat sebuah objek UI bisa dipindah dengan menekan-tahan lalu menyeret.
/// Dipakai untuk panel kontrol UI Customize (ControlsRoot) supaya tidak menutupi elemen yang sedang diatur.
///
/// Cara pasang: di ControlsRoot beri komponen Image (warna latar, Raycast Target aktif), lalu pasang script ini di objek yang sama.
/// Slider dan tombol di dalamnya tetap bekerja seperti biasa (menerima sentuhan lebih dulu); panel hanya bergeser kalau
/// yang ditekan-tahan adalah area kosong. Boleh juga dipasang di objek "pegangan" kecil (mis. batang di atas panel),
/// dengan Target diisi ControlsRoot.
/// Panel ditahan supaya tidak keluar dari tepi Canvas. Posisinya bertahan selama scene berjalan.
/// </summary>
public class DragHandle : MonoBehaviour, IDragHandler
{
    [Tooltip("Objek yang digeser. Kosong = objek ini sendiri.")]
    [SerializeField] private RectTransform target;

    Canvas canvas;
    readonly Vector3[] canvasCorners = new Vector3[4];
    readonly Vector3[] targetCorners = new Vector3[4];

    void Awake()
    {
        if (target == null) target = (RectTransform)transform;

        Canvas c = GetComponentInParent<Canvas>(true);
        canvas = c != null ? c.rootCanvas : null;
    }

    public void OnDrag(PointerEventData e)
    {
        float f = canvas != null ? canvas.scaleFactor : 1f;
        target.anchoredPosition += e.delta / Mathf.Max(0.0001f, f);
        ClampToCanvas();
    }

    void ClampToCanvas()
    {
        if (canvas == null) return;

        ((RectTransform)canvas.transform).GetWorldCorners(canvasCorners);
        target.GetWorldCorners(targetCorners);

        Vector3 shift = Vector3.zero;
        if (targetCorners[0].x < canvasCorners[0].x) shift.x = canvasCorners[0].x - targetCorners[0].x;
        else if (targetCorners[2].x > canvasCorners[2].x) shift.x = canvasCorners[2].x - targetCorners[2].x;
        if (targetCorners[0].y < canvasCorners[0].y) shift.y = canvasCorners[0].y - targetCorners[0].y;
        else if (targetCorners[2].y > canvasCorners[2].y) shift.y = canvasCorners[2].y - targetCorners[2].y;

        if (shift != Vector3.zero) target.position += shift;
    }
}