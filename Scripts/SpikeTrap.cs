using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Jebakan duri. Pasang di objek Tilemap "Spike" (yang punya TilemapCollider2D), atau di satu objek
/// spike biasa yang punya Collider2D. Collider otomatis dijadikan Trigger.
/// HANYA Player yang kena: damage, knockback pendek (seperti MineCart), lalu kebal dan berkedip
/// (diatur PlayerHealth.TakeDamageBlink). Monster dan boss tidak terpengaruh.
/// Kalau Player masih berdiri di duri setelah kebalnya habis, dia kena lagi.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [Tooltip("Lama kebal dan berkedip setelah kena duri (detik). Selama ini Player kebal dari SEMUA sumber damage.")]
    [SerializeField] private float immuneTime = 1.5f;

    [Header("Knockback ke Player")]
    [Tooltip("0 = tanpa knockback.")]
    [SerializeField] private float knockbackSpeed = 6f;
    [SerializeField] private float knockbackDuration = 0.2f;

    Collider2D[] cols;
    Tilemap map;    // kosong kalau script ini dipasang di objek spike biasa (bukan Tilemap)

    void Awake()
    {
        map = GetComponent<Tilemap>();

        // SEMUA collider di objek ini dijadikan Trigger. Kalau ada beberapa BoxCollider2D dan satu saja
        // yang jadi Trigger, sisanya tetap padat dan Player hanya menabraknya.
        cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // hanya Player; monster, boss, dan cart diabaikan
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead || health.IsInvulnerable) return;

        health.TakeDamageBlink(damage, immuneTime);

        var pc = health.GetComponent<PlayerController>();
        if (pc != null && knockbackSpeed > 0f && knockbackDuration > 0f)
            pc.ApplyKnockback(KnockDir(other, pc) * knockbackSpeed, knockbackDuration);
    }

    // pusat collider milik objek ini yang paling dekat dengan titik p
    Vector2 NearestColliderCenter(Vector2 p)
    {
        Vector2 best = transform.position;
        float bestDist = float.MaxValue;
        foreach (var c in cols)
        {
            if (c == null) continue;
            float d = ((Vector2)c.ClosestPoint(p) - p).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c.bounds.center; }
        }
        return best;
    }

    // dorong menjauh dari duri terdekat; kalau tidak bisa ditentukan, mundur berlawanan arah jalan
    Vector2 KnockDir(Collider2D player, PlayerController pc)
    {
        Vector2 p = player.bounds.center;
        Vector2 spikeCenter = NearestColliderCenter(p);
        bool found = false;

        if (map != null)
        {
            // collider Tilemap membungkus SELURUH map, jadi pusatnya tidak berguna: cari sel duri terdekat
            Vector3Int c = map.WorldToCell(p);
            float best = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector3Int(c.x + dx, c.y + dy, c.z);
                    if (!map.HasTile(cell)) continue;

                    Vector2 center = map.GetCellCenterWorld(cell);
                    float d = (p - center).sqrMagnitude;
                    if (d < best) { best = d; spikeCenter = center; found = true; }
                }
        }
        else found = true;

        Vector2 dir = p - spikeCenter;
        if (!found || dir.sqrMagnitude < 0.0001f)
            dir = pc.moveInput.sqrMagnitude > 0.01f ? -pc.moveInput : Vector2.left;

        return dir.normalized;
    }
}