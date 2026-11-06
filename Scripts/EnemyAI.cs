using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyAttack
{
    [Header("Animasi")]
    public string triggerName = "Attack";
    [Tooltip("Bagian nama clip (lihat field Motion di Animator).")]
    public string clipName = "Monster_Attack1";
    public float clipLengthFallback = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("Kapan damage kena di dalam clip (0 = awal, 1 = akhir).")]
    public float hitPoint = 0.5f;

    [Header("Hitbox (kotak)")]
    [Tooltip("Geseran pusat kotak dari pusat monster. X otomatis dicerminkan saat monster menghadap kiri.")]
    public Vector2 hitboxOffset = new Vector2(0.5f, 0f);
    [Tooltip("x = lebar, y = tinggi kotak.")]
    public Vector2 hitboxSize = new Vector2(0.9f, 0.8f);
    [Tooltip("Skala kotak saat damage dihitung (1.2 = 20% lebih lega).")]
    public float hitScale = 1.2f;

    [Header("Damage")]
    public int damage = 1;
    [Tooltip("Jeda setelah serangan ini selesai sebelum serangan berikutnya.")]
    public float cooldownAfter = 1f;

    [Header("Knockback ke Player")]
    [Tooltip("0 = tanpa knockback.")]
    public float knockbackSpeed = 0f;
    public float knockbackDuration = 0.2f;

    [HideInInspector] public float length;
}

public class EnemyAI : MonoBehaviour
{
    [Header("Gerak")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private Transform target; // kosong = dicari otomatis

    [Header("Pathfinding")]
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float waypointReach = 0.15f;
    [Tooltip("Hanya cadangan kalau monster tidak punya collider.")]
    [SerializeField] private float sightRadius = 0.25f;
    [Tooltip("Pengali ukuran badan saat cek jalan lurus. Makin besar, makin lega jaraknya dari tembok.")]
    [SerializeField] private float sightClearance = 1.3f;

    [Header("Anti macet")]
    [SerializeField] private float stuckCheckInterval = 0.5f;
    [SerializeField] private float stuckMinMove = 0.4f;
    [SerializeField] private float forcePathTime = 0.4f;

    [Header("Separation (anti saling blok)")]
    [SerializeField] private float separationRadius = 0.8f;
    [SerializeField] private float separationWeight = 1.2f;

    [Header("Serangan (urut: 1 -> 2 -> 1 -> ...)")]
    [SerializeField]
    private EnemyAttack attack1 = new EnemyAttack
    {
        triggerName = "Attack",
        clipName = "Monster_Attack1",
        hitboxOffset = new Vector2(0.5f, 0f),
        hitboxSize = new Vector2(0.9f, 0.8f),
        damage = 1,
        cooldownAfter = 0.8f,
        knockbackSpeed = 0f
    };

    [SerializeField]
    private EnemyAttack attack2 = new EnemyAttack
    {
        triggerName = "Attack2",
        clipName = "Monster_Attack2",
        hitboxOffset = new Vector2(0.6f, 0f),
        hitboxSize = new Vector2(1.4f, 1.1f),
        damage = 2,
        cooldownAfter = 1.5f,
        knockbackSpeed = 5f,
        knockbackDuration = 0.2f
    };

    [Header("Visual")]
    [SerializeField] private bool spriteFacesLeft = false;

    Rigidbody2D rb;
    Collider2D bodyCol;
    Animator anim;
    SpriteRenderer sr;
    EnemyHealth health;
    Vector2 moveDir;

    bool isAttacking;
    bool nextIsAttack2;      // false = giliran Attack 1
    float nextAttackTime;

    float knockbackUntil;
    Vector2 knockbackVelocity;

    List<Vector2> path;
    int pathIndex;
    float nextRepath;
    float nextLookAhead;

    float nextStuckCheck;
    Vector2 lastCheckPos;
    float forcePathUntil;

    readonly Collider2D[] nearby = new Collider2D[12];
    ContactFilter2D sepFilter;
    Vector2 randomPushDir;

    bool KnockedBack => Time.time < knockbackUntil;
    bool FacingLeft => sr != null && (spriteFacesLeft ? !sr.flipX : sr.flipX);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCol = GetComponent<Collider2D>();
        anim = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();

        attack1.length = GetClipLength(attack1.clipName, attack1.clipLengthFallback);
        attack2.length = GetClipLength(attack2.clipName, attack2.clipLengthFallback);

        sepFilter = new ContactFilter2D();
        float a = UnityEngine.Random.value * Mathf.PI * 2f;
        randomPushDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    void Start()
    {
        lastCheckPos = transform.position;
        nextStuckCheck = Time.time + stuckCheckInterval;

        if (target == null)
        {
            var p = FindAnyObjectByType<PlayerController>();
            if (p != null) target = p.transform;
            else Debug.LogWarning($"{name}: Player tidak ditemukan.");
        }
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

    public void ApplyKnockback(Vector2 velocity, float duration)
    {
        if (health != null && health.IsDead) return;
        knockbackVelocity = velocity;
        knockbackUntil = Time.time + duration;
        path = null;
    }

    // ---------- hitbox serangan ----------
    Vector2 HitboxCenter(EnemyAttack a, bool left)
    {
        Vector2 off = a.hitboxOffset;
        if (left) off.x = -off.x;
        return (Vector2)transform.position + off;
    }

    // true kalau collider Player menyentuh kotak serangan 'a' pada skala tertentu
    bool TargetInHitbox(EnemyAttack a, float scale)
    {
        if (target == null) return false;

        Vector2 c = HitboxCenter(a, FacingLeft);
        Vector2 half = a.hitboxSize * 0.5f * scale;

        var col = target.GetComponentInParent<Collider2D>();
        if (col == null)
        {
            Vector2 d = (Vector2)target.position - c;
            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y;
        }

        Bounds b = col.bounds;
        return Mathf.Abs(b.center.x - c.x) <= half.x + b.extents.x
            && Mathf.Abs(b.center.y - c.y) <= half.y + b.extents.y;
    }

    bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        if (PathGrid.Instance == null) return true;
        Vector2 diff = to - from;
        float dist = diff.magnitude;
        if (dist < 0.01f) return true;

        Vector2 size = bodyCol != null ? (Vector2)bodyCol.bounds.size * sightClearance : Vector2.one * sightRadius * 2f;
        Vector2 offset = bodyCol != null ? (Vector2)bodyCol.bounds.center - (Vector2)transform.position : Vector2.zero;

        return Physics2D.BoxCast(from + offset, size, 0f, diff / dist, dist, PathGrid.Instance.obstacleMask).collider == null;
    }

    Vector2 GetSeparation()
    {
        Vector2 pos = transform.position;
        int count = Physics2D.OverlapCircle(pos, separationRadius, sepFilter, nearby);
        Vector2 push = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            var other = nearby[i].GetComponentInParent<EnemyAI>();
            if (other == null || other == this) continue;
            if (other.health != null && other.health.IsDead) continue;

            Vector2 away = pos - (Vector2)other.transform.position;
            float d = away.magnitude;
            if (d < 0.001f) { away = randomPushDir; d = 0.1f; }
            push += away.normalized * (1f - Mathf.Clamp01(d / separationRadius));
        }
        return push;
    }

    Vector2 ChooseDirection(Vector2 toTarget, bool forcePath)
    {
        Vector2 pos = transform.position;

        if (PathGrid.Instance == null || (!forcePath && HasLineOfSight(pos, target.position)))
        {
            path = null;
            return toTarget.normalized;
        }

        if (path == null || Time.time >= nextRepath)
        {
            path = PathGrid.Instance.FindPath(pos, target.position);
            pathIndex = 0;
            nextLookAhead = 0f;
            nextRepath = Time.time + (path.Count > 0 ? repathInterval : 0.2f);
        }

        if (path.Count == 0) return Vector2.zero;

        if (Time.time >= nextLookAhead)
        {
            nextLookAhead = Time.time + 0.1f;
            for (int i = path.Count - 1; i > pathIndex; i--)
            {
                if (HasLineOfSight(pos, path[i])) { pathIndex = i; break; }
            }
        }

        while (pathIndex < path.Count - 1 && Vector2.Distance(pos, path[pathIndex]) < waypointReach)
            pathIndex++;

        Vector2 d = path[pathIndex] - pos;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.zero;
    }

    void Update()
    {
        moveDir = Vector2.zero;

        bool dead = health != null && health.IsDead;
        if (dead || target == null || !TargetAlive())
        {
            if (anim != null) anim.SetBool("isWalking", false);
            return;
        }

        if (KnockedBack)
        {
            if (anim != null) anim.SetBool("isWalking", false);
            lastCheckPos = transform.position;
            return;
        }

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;

        if (!isAttacking && Mathf.Abs(toTarget.x) > 0.05f && sr != null)
            sr.flipX = spriteFacesLeft ? toTarget.x > 0f : toTarget.x < 0f;

        if (!isAttacking)
        {
            bool sight = HasLineOfSight(transform.position, target.position);
            bool forcePath = Time.time < forcePathUntil;

            // hanya serangan yang jadi giliran yang dipakai
            EnemyAttack next = nextIsAttack2 ? attack2 : attack1;
            bool inBox = sight && TargetInHitbox(next, 1f);

            if (!inBox)
            {
                // belum masuk kotak serangan giliran ini: kejar
                Vector2 dir = ChooseDirection(toTarget, forcePath);
                if (sight && !forcePath) dir += GetSeparation() * separationWeight;
                moveDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.zero;
            }
            else
            {
                // sudah di dalam kotak: berhenti, geser pelan kalau menumpuk dengan monster lain
                Vector2 sep = GetSeparation();
                if (sep.sqrMagnitude > 0.04f) moveDir = sep.normalized * 0.5f;

                if (Time.time >= nextAttackTime) StartCoroutine(AttackRoutine(next));
            }
        }

        // deteksi macet: ingin bergerak tapi hampir tidak bergeser
        if (Time.time >= nextStuckCheck)
        {
            nextStuckCheck = Time.time + stuckCheckInterval;
            bool wantsMove = moveDir.sqrMagnitude > 0.01f && !isAttacking;
            if (wantsMove && Vector2.Distance(transform.position, lastCheckPos) < stuckMinMove)
            {
                path = null;
                nextRepath = 0f;
                forcePathUntil = Time.time + forcePathTime;
            }
            lastCheckPos = transform.position;
        }

        if (anim != null) anim.SetBool("isWalking", moveDir.sqrMagnitude > 0.01f);
    }

    IEnumerator AttackRoutine(EnemyAttack a)
    {
        isAttacking = true;
        moveDir = Vector2.zero;
        if (anim != null) anim.SetTrigger(a.triggerName);

        yield return new WaitForSeconds(a.length * a.hitPoint);

        if (!(health != null && health.IsDead) && !KnockedBack && target != null
            && TargetInHitbox(a, a.hitScale) && HasLineOfSight(transform.position, target.position))
        {
            var dmg = target.GetComponentInParent<IDamageable>();
            if (dmg != null && a.damage > 0) dmg.TakeDamage(a.damage);

            if (a.knockbackSpeed > 0f && a.knockbackDuration > 0f)
            {
                var pc = target.GetComponentInParent<PlayerController>();
                if (pc != null)
                {
                    Vector2 dir = (Vector2)target.position - (Vector2)transform.position;
                    dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
                    pc.ApplyKnockback(dir * a.knockbackSpeed, a.knockbackDuration);
                }
            }
        }

        yield return new WaitForSeconds(a.length * (1f - a.hitPoint));

        nextIsAttack2 = (a == attack1);   // setelah 1 -> 2, setelah 2 -> 1
        nextAttackTime = Time.time + a.cooldownAfter;
        isAttacking = false;
    }

    void FixedUpdate()
    {
        bool dead = health != null && health.IsDead;

        if (dead) rb.linearVelocity = Vector2.zero;
        else if (KnockedBack) rb.linearVelocity = knockbackVelocity;
        else if (isAttacking) rb.linearVelocity = Vector2.zero;
        else rb.linearVelocity = moveDir * moveSpeed;
    }

    // oranye = hitbox Attack 1, cyan = hitbox Attack 2 (garis samar = area saat damage dihitung)
    void OnDrawGizmosSelected()
    {
        var s = sr != null ? sr : GetComponentInChildren<SpriteRenderer>();
        bool left = s != null && (spriteFacesLeft ? !s.flipX : s.flipX);
        DrawBox(attack1, left, new Color(1f, 0.5f, 0f));
        DrawBox(attack2, left, Color.cyan);
    }

    void DrawBox(EnemyAttack a, bool left, Color c)
    {
        Gizmos.color = c;
        Gizmos.DrawWireCube(HitboxCenter(a, left), a.hitboxSize);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.4f);
        Gizmos.DrawWireCube(HitboxCenter(a, left), a.hitboxSize * a.hitScale);
    }
}