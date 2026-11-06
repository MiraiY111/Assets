using System.Collections;
using UnityEngine;

public class StationaryBoss : MonoBehaviour
{
    [Header("Serangan")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private int attackDamage = 2;
    [SerializeField] private float attackCooldown = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float attackHitPoint = 0.5f;
    [Tooltip("Nama clip serangan (lihat field Motion di Animator).")]
    [SerializeField] private string attackClipName = "sting";
    [SerializeField] private float attackLengthFallback = 0.8f;

    [Header("Visual")]
    [SerializeField] private bool spriteFacesLeft = false;
    [SerializeField] private bool faceTarget = true;

    Transform target;
    Animator anim;
    SpriteRenderer sr;
    EnemyHealth health;
    Rigidbody2D rb;

    bool isAttacking;
    float nextAttackTime;
    float attackLength;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        attackLength = GetClipLength(attackClipName, attackLengthFallback);
    }

    void Start()
    {
        var p = FindAnyObjectByType<PlayerController>();
        if (p != null) target = p.transform;
        else Debug.LogWarning($"{name}: Player tidak ditemukan.");

        // diam di tempat: tidak terdorong Player/monster
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    float GetClipLength(string clipName, float fallback)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return fallback;
        foreach (var c in anim.runtimeAnimatorController.animationClips)
            if (c.name.Contains(clipName)) return c.length;
        Debug.LogWarning($"{name}: klip '{clipName}' tidak ditemukan, memakai {fallback}s");
        return fallback;
    }

    bool TargetAlive()
    {
        if (target == null) return false;
        var ph = target.GetComponentInParent<PlayerHealth>();
        return ph == null || !ph.IsDead;
    }

    void Update()
    {
        if (health != null && health.IsDead) return;
        if (target == null || !TargetAlive()) return;

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;

        if (faceTarget && !isAttacking && Mathf.Abs(toTarget.x) > 0.05f && sr != null)
            sr.flipX = spriteFacesLeft ? toTarget.x > 0f : toTarget.x < 0f;

        if (!isAttacking && toTarget.magnitude <= attackRange && Time.time >= nextAttackTime)
            StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        if (anim != null) anim.SetTrigger("Attack");

        yield return new WaitForSeconds(attackLength * attackHitPoint);

        if (!(health != null && health.IsDead) && target != null)
        {
            if (Vector2.Distance(transform.position, target.position) <= attackRange + 0.2f)
            {
                var dmg = target.GetComponentInParent<IDamageable>();
                if (dmg != null) dmg.TakeDamage(attackDamage);
            }
        }

        yield return new WaitForSeconds(attackLength * (1f - attackHitPoint));

        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;
    }
}