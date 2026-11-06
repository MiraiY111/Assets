using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MineCart : MonoBehaviour
{
    [Header("Jalur (opsional)")]
    [Tooltip("Kosongkan: start = posisi cart di scene, end = start + fallbackDistance ke bawah.")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private float fallbackDistance = 10f;

    [Header("Waktu")]
    [SerializeField] private float firstDelay = 3f;
    [SerializeField] private float interval = 5f;
    [SerializeField] private float speed = 8f;

    [Header("Tabrakan: Player")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackSpeed = 9f;
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("Tabrakan: Monster")]
    [SerializeField] private bool hitEnemies = true;
    [SerializeField] private int enemyDamage = 1;
    [SerializeField] private float enemyKnockbackSpeed = 9f;
    [SerializeField] private float enemyKnockbackDuration = 0.25f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    Vector2 startPos, endPos;
    bool moving;

    // satu target hanya kena sekali per lewatan
    readonly HashSet<GameObject> hitThisPass = new HashSet<GameObject>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        col.isTrigger = true;

        startPos = startPoint != null ? (Vector2)startPoint.position : (Vector2)transform.position;
        endPos = endPoint != null ? (Vector2)endPoint.position : startPos + Vector2.down * fallbackDistance;
    }

    void Start()
    {
        SetVisible(false);
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(firstDelay);

        while (true)
        {
            transform.position = startPos;
            rb.position = startPos;
            hitThisPass.Clear();
            moving = true;
            SetVisible(true);

            while (Vector2.Distance(rb.position, endPos) > 0.01f)
            {
                rb.MovePosition(Vector2.MoveTowards(rb.position, endPos, speed * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }

            moving = false;
            SetVisible(false);
            yield return new WaitForSeconds(interval);
        }
    }

    void SetVisible(bool on)
    {
        if (sr != null) sr.enabled = on;
        if (col != null) col.enabled = on;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!moving) return;

        // ---- Player ----
        var playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            if (playerHealth.IsDead || !hitThisPass.Add(playerHealth.gameObject)) return;

            playerHealth.TakeDamage(damage);
            var pc = playerHealth.GetComponent<PlayerController>();
            if (pc != null) pc.ApplyKnockback(KnockDir(other) * knockbackSpeed, knockbackDuration);
            return;
        }

        // ---- Monster ----
        if (!hitEnemies) return;
        var enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            if (enemyHealth.IsDead || !hitThisPass.Add(enemyHealth.gameObject)) return;

            enemyHealth.TakeDamage(enemyDamage);
            var ai = enemyHealth.GetComponent<EnemyAI>();
            if (ai != null) ai.ApplyKnockback(KnockDir(other) * enemyKnockbackSpeed, enemyKnockbackDuration);
        }
    }

    // dorong ke samping rel (berdasarkan pusat collider), ditambah sedikit searah laju cart
    Vector2 KnockDir(Collider2D other)
    {
        float diff = other.bounds.center.x - col.bounds.center.x;
        float side = diff >= 0f ? 1f : -1f;
        if (Mathf.Abs(diff) < 0.05f) side = 1f;

        Vector2 moveDir = (endPos - startPos).normalized;
        return (new Vector2(side, 0f) + moveDir * 0.5f).normalized;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 s = startPoint != null ? (Vector2)startPoint.position : (Vector2)transform.position;
        Vector2 e = endPoint != null ? (Vector2)endPoint.position : s + Vector2.down * fallbackDistance;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(s, e);
        Gizmos.DrawWireSphere(e, 0.2f);
    }
}