using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;

    [Header("Tilemap yang membentuk batas map (Walls, Floor)")]
    public Tilemap[] mapTilemaps;

    [Header("Opsi")]
    public float edgePadding = 0f;        // geser batas ke dalam/luar bila perlu
    public bool shrinkIfMapTooNarrow = true;

    [Header("Fokus (cutscene)")]
    [Tooltip("Kehalusan geser saat kamera pindah ke ujung kanan dan saat kembali ke Player. Makin kecil makin cepat.")]
    public float focusSmoothTime = 0.5f;
    [Tooltip("Dianggap sudah sampai kalau jaraknya segini dari tujuan (satuan Unity).")]
    public float arriveTolerance = 0.2f;

    enum Mode { Follow, RightEdge }

    /// <summary>True selama kamera diarahkan ke ujung kanan (belum dilepas).</summary>
    public bool IsFocusing => mode == Mode.RightEdge;
    /// <summary>True kalau kamera sudah sampai di ujung kanan.</summary>
    public bool IsAtFocus { get; private set; }
    /// <summary>True selama kamera masih bergeser kembali ke Player setelah fokus dilepas.</summary>
    public bool IsReturning => returning;

    Camera cam;
    Vector3 velocity;
    float fixedY;
    float mapLeft, mapRight;
    Mode mode = Mode.Follow;
    bool returning;

    void Start()
    {
        cam = GetComponent<Camera>();
        fixedY = transform.position.y;
        CalculateMapBounds();
    }

    /// <summary>Geser kamera ke posisi paling kanan yang masih di dalam batas map.</summary>
    public void FocusRightEdge()
    {
        mode = Mode.RightEdge;
        returning = false;
    }

    /// <summary>Lepaskan fokus: kamera bergeser pelan kembali mengikuti target.</summary>
    public void ReleaseFocus()
    {
        if (mode == Mode.Follow) return;
        mode = Mode.Follow;
        returning = true;
        IsAtFocus = false;
    }

    void CalculateMapBounds()
    {
        bool first = true;
        foreach (var tm in mapTilemaps)
        {
            if (tm == null) continue;
            tm.CompressBounds();                       // buang sel kosong
            BoundsInt cb = tm.cellBounds;
            float l = tm.CellToWorld(cb.min).x;
            float r = tm.CellToWorld(cb.max).x;        // max bersifat eksklusif = tepi kanan

            if (first) { mapLeft = l; mapRight = r; first = false; }
            else { mapLeft = Mathf.Min(mapLeft, l); mapRight = Mathf.Max(mapRight, r); }
        }
        mapLeft += edgePadding;
        mapRight -= edgePadding;
    }

    void LateUpdate()
    {
        // mode fokus tidak butuh target; hanya mode ikut Player yang butuh
        if (target == null && mode == Mode.Follow) return;

        float mapWidth = mapRight - mapLeft;

        // layar lebih lebar dari map: perkecil zoom supaya tidak ada area kosong
        if (shrinkIfMapTooNarrow)
        {
            float maxSize = mapWidth / (2f * cam.aspect);
            if (cam.orthographicSize > maxSize) cam.orthographicSize = maxSize;
        }

        float halfWidth = cam.orthographicSize * cam.aspect;
        float minX = mapLeft + halfWidth;
        float maxX = mapRight - halfWidth;
        bool narrow = minX > maxX;
        float centerX = (mapLeft + mapRight) * 0.5f;

        // batas yang sama dipakai untuk ikut Player maupun fokus, jadi kamera tidak pernah keluar map
        float targetX;
        if (mode == Mode.RightEdge) targetX = narrow ? centerX : maxX;
        else targetX = narrow ? centerX : Mathf.Clamp(target.position.x, minX, maxX);

        float smooth = (mode == Mode.RightEdge || returning) ? focusSmoothTime : smoothTime;

        Vector3 desired = new Vector3(targetX, fixedY, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smooth);

        float dist = Mathf.Abs(transform.position.x - targetX);
        IsAtFocus = mode == Mode.RightEdge && dist <= arriveTolerance;
        if (returning && dist <= arriveTolerance) returning = false;
    }
}