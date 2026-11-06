using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PathGrid : MonoBehaviour
{
    public static PathGrid Instance { get; private set; }

    [Header("Area grid")]
    [Tooltip("Isi dengan tilemap Floor/Walls agar area dihitung otomatis. Kosongkan untuk memakai Manual Min/Max.")]
    [SerializeField] private Tilemap[] boundsFrom;
    [SerializeField] private Vector2 manualMin = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 manualMax = new Vector2(10f, 10f);
    [SerializeField] private float cellSize = 0.5f;

    [Header("Rintangan")]
    public LayerMask obstacleMask;
    [Tooltip("Jarak aman dari tembok (kira-kira setengah lebar monster).")]
    [SerializeField] private float agentRadius = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    Vector2 origin;
    int width, height;
    bool[] walkable;

    // cache untuk A*
    float[] gCost;
    int[] parent;
    bool[] closed;
    bool[] inOpen;

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
        new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
    };

    void Awake() { Instance = this; }

    void Start() { Rebuild(); }

    public void Rebuild()
    {
        Vector2 min = manualMin, max = manualMax;

        if (boundsFrom != null && boundsFrom.Length > 0)
        {
            bool first = true;
            foreach (var tm in boundsFrom)
            {
                if (tm == null) continue;
                tm.CompressBounds();
                BoundsInt cb = tm.cellBounds;
                Vector2 l = tm.CellToWorld(cb.min);
                Vector2 r = tm.CellToWorld(cb.max);
                if (first) { min = l; max = r; first = false; }
                else { min = Vector2.Min(min, l); max = Vector2.Max(max, r); }
            }
        }

        origin = min;
        width = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / cellSize));
        height = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / cellSize));

        int n = width * height;
        walkable = new bool[n];
        gCost = new float[n];
        parent = new int[n];
        closed = new bool[n];
        inOpen = new bool[n];

        Physics2D.SyncTransforms();

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
        var results = new Collider2D[1];
        Vector2 boxSize = Vector2.one * (cellSize + agentRadius * 2f);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                walkable[y * width + x] = Physics2D.OverlapBox(CellCenter(x, y), boxSize, 0f, filter, results) == 0;
    }

    Vector2 CellCenter(int x, int y) => origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

    Vector2Int WorldToCell(Vector2 p)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / cellSize), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((p.y - origin.y) / cellSize), 0, height - 1);
        return new Vector2Int(x, y);
    }

    bool IsWalkable(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && walkable[y * width + x];

    /// <summary>
    /// True kalau titik dunia 'p' ada di sel yang aman (bukan tembok/rintangan, dan di dalam area grid).
    /// Dipakai spawner. False kalau grid belum dibangun (sebelum Start selesai).
    /// </summary>
    public bool IsWalkableAt(Vector2 p)
    {
        if (walkable == null) return false;
        int x = Mathf.FloorToInt((p.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((p.y - origin.y) / cellSize);
        return IsWalkable(x, y);
    }

    // kalau sel Player/monster ada di zona "tidak aman" dekat tembok, cari sel aman terdekat
    Vector2Int NearestWalkable(Vector2Int c)
    {
        if (IsWalkable(c.x, c.y)) return c;
        for (int r = 1; r <= 6; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    if (IsWalkable(c.x + dx, c.y + dy)) return new Vector2Int(c.x + dx, c.y + dy);
                }
        return c;
    }

    float Heuristic(int x, int y, int gx, int gy)
    {
        int dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
        return (dx + dy) + (1.41421356f - 2f) * Mathf.Min(dx, dy); // jarak octile
    }

    /// <summary>Mengembalikan daftar titik dunia dari 'from' ke 'to'. Kosong kalau tidak ada jalur.</summary>
    public List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        var path = new List<Vector2>();
        if (walkable == null) return path;

        Vector2Int s = NearestWalkable(WorldToCell(from));
        Vector2Int g = NearestWalkable(WorldToCell(to));
        if (!IsWalkable(s.x, s.y) || !IsWalkable(g.x, g.y)) return path;

        Array.Clear(closed, 0, closed.Length);
        Array.Clear(inOpen, 0, inOpen.Length);

        int start = s.y * width + s.x;
        int goal = g.y * width + g.x;

        var open = new List<(int idx, float f)>();
        gCost[start] = 0f;
        parent[start] = -1;
        open.Add((start, Heuristic(s.x, s.y, g.x, g.y)));
        inOpen[start] = true;

        bool found = false;
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (open[i].f < open[best].f) best = i;

            int cur = open[best].idx;
            open.RemoveAt(best);
            inOpen[cur] = false;

            if (cur == goal) { found = true; break; }
            closed[cur] = true;

            int cx = cur % width, cy = cur / width;

            foreach (var d in Dirs)
            {
                int nx = cx + d.x, ny = cy + d.y;
                if (!IsWalkable(nx, ny)) continue;

                // jangan memotong sudut tembok saat jalan diagonal
                if (d.x != 0 && d.y != 0 && (!IsWalkable(cx + d.x, cy) || !IsWalkable(cx, cy + d.y))) continue;

                int ni = ny * width + nx;
                if (closed[ni]) continue;

                float ng = gCost[cur] + ((d.x != 0 && d.y != 0) ? 1.41421356f : 1f);
                if (!inOpen[ni] || ng < gCost[ni])
                {
                    gCost[ni] = ng;
                    parent[ni] = cur;
                    if (!inOpen[ni])
                    {
                        open.Add((ni, 0f));
                        inOpen[ni] = true;
                    }
                    int oi = open.FindIndex(o => o.idx == ni);
                    open[oi] = (ni, ng + Heuristic(nx, ny, g.x, g.y));
                }
            }
        }

        if (!found) return path;

        for (int c = goal; c != -1 && c != start; c = parent[c])
            path.Add(CellCenter(c % width, c / width));
        path.Reverse();
        return path;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || walkable == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (!walkable[y * width + x])
                    Gizmos.DrawCube(CellCenter(x, y), Vector3.one * cellSize * 0.9f);
    }
}