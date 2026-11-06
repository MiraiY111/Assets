using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Penentu posisi spawn monster.
/// Zona 1 (hijau)  : area BOLEH spawn   -> daftar BoxCollider2D
/// Zona 2 (kuning) : area kamera + margin, dihitung otomatis, DILARANG
/// Zona 3 (merah)  : area DILARANG      -> daftar BoxCollider2D (tombol + di Inspector)
/// Satu posisi sah kalau: di dalam salah satu zona 1, di luar zona 2, di luar semua zona 3,
/// sel PathGrid-nya walkable, cukup jauh dari Player dan spawn sebelumnya, dan punya jalur ke Player.
/// BoxCollider2D hanya dipakai sebagai "handle" di Scene view; script ini mematikannya saat Play
/// dan menghitung kotaknya sendiri, jadi tidak ikut tabrakan fisika.
/// </summary>
public class EnemySpawnZones : MonoBehaviour
{
    [Header("Zona 1: area BOLEH spawn")]
    [SerializeField] private List<BoxCollider2D> allowedZones = new List<BoxCollider2D>();

    [Header("Zona 3: area DILARANG (rel cart, dekat start Player, dll)")]
    [SerializeField] private List<BoxCollider2D> forbiddenZones = new List<BoxCollider2D>();

    [Header("Zona 2: area kamera (otomatis)")]
    [Tooltip("Kosong = Camera.main")]
    [SerializeField] private Camera cam;
    [Tooltip("Tambahan di luar tepi layar (satuan Unity). Minimal setengah ukuran monster.")]
    [SerializeField] private float cameraMargin = 1.5f;

    [Header("Aturan tambahan")]
    [Tooltip("Kosong = dicari otomatis")]
    [SerializeField] private Transform player;
    [SerializeField] private float minPlayerDistance = 4f;
    [Tooltip("Jarak minimum antar posisi spawn (supaya tidak menumpuk). Panggil ClearHistory() tiap ronde baru.")]
    [SerializeField] private float minDistanceBetweenSpawns = 1f;
    [Tooltip("Spawn ditolak kalau PathGrid tidak menemukan jalur ke Player (kantong lantai terkurung).")]
    [SerializeField] private bool requirePathToPlayer = true;
    [SerializeField] private int maxAttempts = 40;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    readonly List<Vector2> recentSpawns = new List<Vector2>();

    void Awake()
    {
        // collider hanya untuk menggambar kotak di Scene view
        foreach (var z in allowedZones) if (z != null) z.enabled = false;
        foreach (var z in forbiddenZones) if (z != null) z.enabled = false;
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerController>();
            if (p != null) player = p.transform;
        }
    }

    public void ClearHistory() { recentSpawns.Clear(); }

    /// <summary>
    /// Cari posisi spawn yang sah. False kalau semua percobaan gagal
    /// (zona terlalu sempit, atau PathGrid belum dibangun); pemanggil harus menangani ini.
    /// </summary>
    public bool TryGetSpawnPosition(out Vector2 result)
    {
        result = Vector2.zero;

        if (allowedZones.Count == 0)
        {
            Debug.LogWarning($"{name}: Allowed Zones kosong.");
            return false;
        }

        if (cam == null) cam = Camera.main;
        Rect camRect = GetCameraRect();

        // pilih kotak zona 1 berdasarkan luas, supaya kotak kecil tidak kebagian terlalu sering
        var rects = new List<Rect>();
        float totalArea = 0f;
        foreach (var z in allowedZones)
        {
            if (z == null) continue;
            Rect r = GetRect(z);
            rects.Add(r);
            totalArea += r.width * r.height;
        }
        if (rects.Count == 0 || totalArea <= 0f) return false;

        for (int i = 0; i < maxAttempts; i++)
        {
            Rect pick = PickByArea(rects, totalArea);
            Vector2 p = new Vector2(
                Random.Range(pick.xMin, pick.xMax),
                Random.Range(pick.yMin, pick.yMax));

            if (IsValid(p, camRect))
            {
                result = p;
                recentSpawns.Add(p);
                return true;
            }
        }

        Debug.LogWarning($"{name}: tidak menemukan posisi spawn setelah {maxAttempts} percobaan. " +
                         "Zona 1 terlalu sempit, zona terlarang terlalu luas, atau PathGrid belum selesai dibangun.");
        return false;
    }

    bool IsValid(Vector2 p, Rect camRect)
    {
        // zona 2: kelihatan di layar
        if (cam != null && camRect.Contains(p)) return false;

        // zona 3: area terlarang
        foreach (var z in forbiddenZones)
            if (z != null && GetRect(z).Contains(p)) return false;

        // PathGrid: bukan tembok/rintangan
        if (PathGrid.Instance != null && !PathGrid.Instance.IsWalkableAt(p)) return false;

        // jauh dari Player
        if (player != null && Vector2.Distance(p, player.position) < minPlayerDistance) return false;

        // tidak menumpuk dengan spawn sebelumnya
        foreach (var s in recentSpawns)
            if (Vector2.Distance(p, s) < minDistanceBetweenSpawns) return false;

        // terakhir (paling mahal): harus punya jalur ke Player
        if (requirePathToPlayer && PathGrid.Instance != null && player != null)
        {
            var path = PathGrid.Instance.FindPath(p, player.position);
            if (path.Count == 0) return false;
        }

        return true;
    }

    static Rect PickByArea(List<Rect> rects, float totalArea)
    {
        float roll = Random.value * totalArea;
        foreach (var r in rects)
        {
            roll -= r.width * r.height;
            if (roll <= 0f) return r;
        }
        return rects[rects.Count - 1];
    }

    // kotak dunia dari BoxCollider2D (rotasi diabaikan; jangan memutar objek zona)
    static Rect GetRect(BoxCollider2D b)
    {
        Vector3 c = b.transform.TransformPoint(b.offset);
        Vector3 s = b.transform.lossyScale;
        Vector2 size = new Vector2(b.size.x * Mathf.Abs(s.x), b.size.y * Mathf.Abs(s.y));
        return new Rect((Vector2)c - size * 0.5f, size);
    }

    Rect GetCameraRect()
    {
        if (cam == null) return new Rect(0, 0, 0, 0);
        float halfH = cam.orthographicSize + cameraMargin;
        float halfW = cam.orthographicSize * cam.aspect + cameraMargin;
        Vector2 c = cam.transform.position;
        return new Rect(c - new Vector2(halfW, halfH), new Vector2(halfW * 2f, halfH * 2f));
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        foreach (var z in allowedZones) DrawZone(z, new Color(0f, 1f, 0f, 0.15f), Color.green);
        foreach (var z in forbiddenZones) DrawZone(z, new Color(1f, 0f, 0f, 0.25f), Color.red);

        var c = cam != null ? cam : Camera.main;
        if (c != null)
        {
            var saved = cam;
            cam = c;
            Rect r = GetCameraRect();
            cam = saved;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(r.center, r.size);
        }
    }

    static void DrawZone(BoxCollider2D z, Color fill, Color line)
    {
        if (z == null) return;
        Rect r = GetRect(z);
        Gizmos.color = fill;
        Gizmos.DrawCube(r.center, r.size);
        Gizmos.color = line;
        Gizmos.DrawWireCube(r.center, r.size);
    }
}