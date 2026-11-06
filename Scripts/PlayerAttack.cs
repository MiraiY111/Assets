using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Pindahkan ke file sendiri (IDamageable.cs) saat mulai hari 12 kalau mau lebih rapi.
public interface IDamageable
{
    void TakeDamage(int amount);
}

/// <summary>
/// Hitbox Sword. Tidak ada input dan tidak ada cooldown di sini;
/// keduanya ditangani PlayerController (dipanggil dari AttackButton).
/// Target dikunci saat serangan DIMULAI, damage dijatuhkan saat titik hit animasi.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Hitbox")]
    [Tooltip("Jarak pusat hitbox dari pusat pemain (satuan Unity).")]
    [SerializeField] private float reach = 0.6f;
    [Tooltip("x = panjang searah serangan, y = tinggi.")]
    [SerializeField] private Vector2 boxSize = new Vector2(0.8f, 0.6f);
    [Tooltip("Layer yang bisa dipukul. Dropdown ini multi-select.")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Filter tag (opsional)")]
    [Tooltip("Kosongkan = semua target di Target Layers kena. Isi = hanya objek dengan salah satu tag ini yang kena.")]
    [SerializeField] private string[] allowedTags;

    [Header("Penguncian target")]
    [Tooltip("Aktif: yang ada di hitbox saat tombol ditekan akan kena di titik hit, walau Player berbalik atau bergeser. " +
             "Nonaktif: hitbox dicek baru saat titik hit (perilaku lama).")]
    [SerializeField] private bool lockTargetsAtStart = true;
    [Tooltip("Aktif (bersama penguncian): musuh yang masuk hitbox tepat di titik hit juga ikut kena.")]
    [SerializeField] private bool alsoCheckAtHit = false;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    // flipX false = menghadap kanan (sesuai PlayerController)
    private Vector2 Facing(SpriteRenderer renderer) =>
        (renderer != null && renderer.flipX) ? Vector2.left : Vector2.right;

    private Vector2 Center(Vector2 origin, Vector2 dir) => origin + dir * reach;

    /// <summary>
    /// Dipanggil PlayerController saat serangan dimulai.
    /// Arah dan target dikunci sekarang, damage diterapkan setelah 'delay' detik.
    /// </summary>
    public void HitAfter(float delay, int damage)
    {
        Vector2 dir = Facing(sr);
        Vector2 origin = transform.position;

        var locked = new HashSet<IDamageable>();
        if (lockTargetsAtStart) CollectTargets(origin, dir, locked);

        StartCoroutine(HitRoutine(dir, origin, delay, damage, locked));
    }

    private IEnumerator HitRoutine(Vector2 dir, Vector2 startOrigin, float delay, int damage, HashSet<IDamageable> targets)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // mode lama, atau tambahan: cek hitbox di titik hit (dari posisi Player sekarang)
        // cek hitbox di titik hit, memakai arah hadap Player SEKARANG
        if (!lockTargetsAtStart || alsoCheckAtHit)
            CollectTargets(transform.position, Facing(sr), targets);

        foreach (var t in targets)
        {
            // lewati target yang sudah dihancurkan selama jeda
            if ((t as Object) == null) continue;
            t.TakeDamage(damage);
        }
    }

    // kumpulkan target unik di hitbox (satu target hanya sekali, walau punya banyak collider)
    private void CollectTargets(Vector2 origin, Vector2 dir, HashSet<IDamageable> into)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(Center(origin, dir), boxSize, 0f, targetLayers);

        foreach (Collider2D hit in hits)
        {
            if (allowedTags != null && allowedTags.Length > 0 && !HasAllowedTag(hit)) continue;

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                Debug.Log($"Kena {hit.name}, tapi belum ada IDamageable.");
                continue;
            }
            into.Add(target);
        }
    }

    private bool HasAllowedTag(Collider2D hit)
    {
        foreach (var t in allowedTags)
        {
            if (string.IsNullOrEmpty(t)) continue;
            if (hit.CompareTag(t) || hit.transform.root.CompareTag(t)) return true;
        }
        return false;
    }

    // Kotak merah di Scene view saat Player dipilih (menghadap sesuai flipX saat ini).
    private void OnDrawGizmosSelected()
    {
        Vector2 dir = Facing(sr != null ? sr : GetComponent<SpriteRenderer>());
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Center(transform.position, dir), boxSize);
    }
}