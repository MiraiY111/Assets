using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class BossAttack
{
    [Header("Animasi")]
    public string triggerName = "Attack";
    [Tooltip("Bagian unik dari nama clip (lihat field Motion di Animator).")]
    public string clipName = "sting";
    public float clipLengthFallback = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("Kapan damage kena di dalam clip (0 = awal, 1 = akhir).")]
    public float hitPoint = 0.5f;

    [Header("Persiapan (windup)")]
    [Tooltip("Boss diam selama ini setelah Player masuk zona, baru animasi serangan dimulai. 0 = langsung.")]
    public float windupTime = 1f;

    [Header("Area serangan (elips)")]
    [Tooltip("Geser pusat area dari pusat boss. X otomatis dicerminkan kalau boss menghadap kiri.")]
    public Vector2 rangeOffset = Vector2.zero;
    [Tooltip("Lebar (X) dan tinggi (Y) elips, dalam satuan Unity.")]
    public Vector2 rangeSize = new Vector2(3f, 3f);

    [Header("Damage")]
    public int damage = 2;
    [Tooltip("Jeda khusus serangan ini sebelum boleh dipakai lagi. 0 = hanya dibatasi jeda antar serangan.")]
    public float cooldown = 0f;

    [Header("Knockback")]
    [Tooltip("0 = tanpa knockback.")]
    public float knockbackSpeed = 6f;
    public float knockbackDuration = 0.2f;

    [Header("Stun")]
    [Tooltip("Total waktu Player tidak bisa bergerak/menyerang (detik), sudah termasuk durasi knockback. 0 = tanpa stun.")]
    public float stunDuration = 0f;

    [HideInInspector] public float nextReadyTime;
    [HideInInspector] public float length;
}

public class BossAttackController : MonoBehaviour
{
    [SerializeField]
    private BossAttack attack1 = new BossAttack
    {
        triggerName = "Attack",
        clipName = "sting",
        rangeSize = new Vector2(2.5f, 2.5f),
        damage = 2,
        knockbackSpeed = 5f,
        knockbackDuration = 0.2f,
        stunDuration = 0f
    };

    [SerializeField]
    private BossAttack attack2 = new BossAttack
    {
        triggerName = "Attack2",
        clipName = "shockwave",
        rangeSize = new Vector2(5f, 4f),
        damage = 1,
        knockbackSpeed = 9f,
        knockbackDuration = 0.25f,
        stunDuration = 1f
    };

    // [DRAGON] serangan ketiga, opsional. Dimatikan (default) = perilaku sama persis seperti sebelumnya (ubur-ubur).
    [Header("Serangan ke-3 (opsional)")]
    [Tooltip("Centang untuk memakai serangan ke-3. Urutan prioritas: Attack 1 (terdekat) > Attack 2 > Attack 3 (terjauh).")]
    [SerializeField] private bool useAttack3 = false;

    [SerializeField]
    private BossAttack attack3 = new BossAttack
    {
        triggerName = "Attack3",
        clipName = "Firebreath",
        rangeOffset = new Vector2(3f, 0f),
        rangeSize = new Vector2(6f, 1.5f),
        damage = 1,
        knockbackSpeed = 4f,
        knockbackDuration = 0.15f,
        stunDuration = 0f
    };

    [Header("Gerak")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private string walkParam = "isWalking";

    [Header("Pemilihan serangan")]
    [Tooltip("Boss diam selama ini setelah satu serangan selesai, sebelum bergerak/menyerang lagi.")]
    [SerializeField] private float delayBetweenAttacks = 1f;
    // [DRAGON]
    [Tooltip("Aktif: kalau Player ada di jangkauan tapi semua serangan masih cooldown, boss tetap mendekat " +
             "(bukan berdiri diam). Matikan untuk perilaku lama.")]
    [SerializeField] private bool approachWhileOnCooldown = false;
    [Tooltip("Skala area saat damage dihitung (1.1 = 10% lebih lega dari elips).")]
    [SerializeField] private float hitScale = 1.1f;
    [Tooltip("Boss baru mulai bersiap kalau Player sudah di dalam area sebesar ini (0.7 = 70% dari ukuran elips).")]
    [SerializeField] private float engageScale = 0.7f;

    [Header("Visual")]
    [SerializeField] private bool faceTarget = true;
    [SerializeField] private bool spriteFacesLeft = false;
    [Tooltip("Warna boss selama windup, sebagai tanda peringatan. Putih = tanpa tanda.")]
    [SerializeField] private Color windupTint = new Color(1f, 0.7f, 0.7f, 1f);

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    Transform target;
    Animator anim;
    SpriteRenderer sr;
    EnemyHealth health;
    Rigidbody2D rb;

    bool hasWalkParam;
    bool isAttacking;
    float nextActionTime;
    Vector2 moveDir;
    Color baseColor = Color.white;

    bool FacingLeft => sr != null && (spriteFacesLeft ? !sr.flipX : sr.flipX);

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        if (sr != null) baseColor = sr.color;

        attack1.length = ClipLength(attack1.clipName, attack1.clipLengthFallback);
        attack2.length = ClipLength(attack2.clipName, attack2.clipLengthFallback);
        if (useAttack3) attack3.length = ClipLength(attack3.clipName, attack3.clipLengthFallback);   // [DRAGON]

        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var p in anim.parameters)
                if (p.name == walkParam && p.type == AnimatorControllerParameterType.Bool) hasWalkParam = true;
    }

    void Start()
    {
        var p = FindAnyObjectByType<PlayerController>();
        if (p != null) target = p.transform;
        else Debug.LogWarning($"{name}: Player tidak ditemukan.");
    }

    float ClipLength(string clipName, float fallback)
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

    // ---------- area serangan (elips) ----------
    Vector2 Center(BossAttack a, bool left)
    {
        Vector2 off = a.rangeOffset;
        if (left) off.x = -off.x;
        return (Vector2)transform.position + off;
    }

    float EllipseValue(BossAttack a, Vector2 p, float scale)
    {
        Vector2 half = a.rangeSize * 0.5f * scale;
        if (half.x <= 0f || half.y <= 0f) return float.MaxValue;
        Vector2 d = p - Center(a, FacingLeft);
        return (d.x * d.x) / (half.x * half.x) + (d.y * d.y) / (half.y * half.y);
    }

    bool InRange(BossAttack a, Vector2 p, float scale = 1f) => EllipseValue(a, p, scale) <= 1f;

    bool Ready(BossAttack a) => Time.time >= a.nextReadyTime;

    // [DRAGON] daftar serangan aktif, urut prioritas
    int AttackCount => useAttack3 ? 3 : 2;
    BossAttack AttackAt(int i) => i == 0 ? attack1 : (i == 1 ? attack2 : attack3);

    // ---------- logika utama ----------
    void Update()
    {
        moveDir = Vector2.zero;

        bool dead = health != null && health.IsDead;
        if (dead || target == null || !TargetAlive()) { SetWalking(false); return; }

        if (isAttacking || Time.time < nextActionTime) { SetWalking(false); return; }

        Vector2 tp = target.position;

        if (faceTarget && sr != null)
        {
            float dx = tp.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) sr.flipX = spriteFacesLeft ? dx > 0f : dx < 0f;
        }

        // [DRAGON] cek serangan berurutan dari yang terdekat (Attack 1) ke terjauh: pakai yang pertama
        // yang Player-nya sudah di dalam jangkauan DAN sudah siap (tidak cooldown)
        BossAttack chosen = null;
        bool anyInRange = false;
        for (int i = 0; i < AttackCount; i++)
        {
            var a = AttackAt(i);
            if (!InRange(a, tp, engageScale)) continue;
            anyInRange = true;
            if (Ready(a)) { chosen = a; break; }
        }

        if (chosen != null)
        {
            StartCoroutine(AttackRoutine(chosen));
        }
        else if (!anyInRange || (approachWhileOnCooldown && !InRange(attack1, tp, engageScale)))
        {
            // belum ada serangan yang kena, atau semua cooldown dan opsi 'mendekat' aktif: kejar Player
            Vector2 d = tp - (Vector2)transform.position;
            moveDir = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.zero;
        }
        // selain itu: Player di jangkauan tapi semua cooldown, boss berdiri diam (perilaku lama)

        SetWalking(moveDir.sqrMagnitude > 0.01f);
    }

    void FixedUpdate()
    {
        bool dead = health != null && health.IsDead;
        rb.linearVelocity = (dead || isAttacking) ? Vector2.zero : moveDir * moveSpeed;
    }

    void SetWalking(bool on)
    {
        if (anim != null && hasWalkParam) anim.SetBool(walkParam, on);
    }

    IEnumerator AttackRoutine(BossAttack a)
    {
        isAttacking = true;
        moveDir = Vector2.zero;
        SetWalking(false);

        // ---- windup: diam dulu, belum menyerang ----
        if (a.windupTime > 0f)
        {
            if (debugLog) Debug.Log($"[BOSS] {a.triggerName} WINDUP {a.windupTime:F1}s");
            if (sr != null) sr.color = windupTint;

            float t = 0f;
            while (t < a.windupTime)
            {
                if (health != null && health.IsDead) break;
                t += Time.deltaTime;
                yield return null;
            }

            if (sr != null && !(health != null && health.IsDead)) sr.color = baseColor;
        }

        if (health != null && health.IsDead) { isAttacking = false; yield break; }

        // ---- serangan sebenarnya ----
        if (anim != null) anim.SetTrigger(a.triggerName);

        if (debugLog && target != null)
            Debug.Log($"[BOSS] {a.triggerName} MULAI | nilaiElips={EllipseValue(a, target.position, 1f):F2}");

        yield return new WaitForSeconds(a.length * a.hitPoint);

        bool alive = !(health != null && health.IsDead);
        bool playerOk = target != null && TargetAlive();
        float v1 = playerOk ? EllipseValue(a, target.position, hitScale) : float.MaxValue;
        bool inRange = playerOk && v1 <= 1f;

        if (debugLog && target != null)
            Debug.Log($"[BOSS] {a.triggerName} CEK HIT | nilaiElips={v1:F2} (harus <=1) inRange={inRange} damage={a.damage}");

        if (alive && inRange) ApplyHit(a);

        yield return new WaitForSeconds(a.length * (1f - a.hitPoint));

        a.nextReadyTime = Time.time + a.cooldown;
        nextActionTime = Time.time + delayBetweenAttacks;
        isAttacking = false;
    }

    void ApplyHit(BossAttack a)
    {
        var dmg = target.GetComponentInParent<IDamageable>();
        if (dmg != null && a.damage > 0) dmg.TakeDamage(a.damage);

        var pc = target.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        Vector2 dir = (Vector2)target.position - (Vector2)transform.position;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        bool hasKnockback = a.knockbackSpeed > 0f && a.knockbackDuration > 0f;
        float kbTime = hasKnockback ? a.knockbackDuration : 0f;
        if (hasKnockback) pc.ApplyKnockback(dir * a.knockbackSpeed, kbTime);

        float stunLeft = a.stunDuration - kbTime;
        if (stunLeft > 0f) StartCoroutine(StunAfter(pc, kbTime, stunLeft));
    }

    IEnumerator StunAfter(PlayerController pc, float delay, float duration)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (pc != null) pc.ApplyKnockback(Vector2.zero, duration);
    }

    // ---------- gizmo ----------
    void OnDrawGizmosSelected()
    {
        var s = sr != null ? sr : GetComponentInChildren<SpriteRenderer>();
        bool left = s != null && (spriteFacesLeft ? !s.flipX : s.flipX);
        DrawEllipse(attack1, left, Color.red);
        DrawEllipse(attack2, left, Color.magenta);
        if (useAttack3) DrawEllipse(attack3, left, new Color(1f, 0.6f, 0f));   // [DRAGON] oranye
    }

    void DrawEllipse(BossAttack a, bool left, Color c)
    {
        Gizmos.color = c;
        Vector2 center = Center(a, left);
        Vector2 half = a.rangeSize * 0.5f;
        const int seg = 48;
        Vector2 prev = center + new Vector2(half.x, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float t = i / (float)seg * Mathf.PI * 2f;
            Vector2 p = center + new Vector2(Mathf.Cos(t) * half.x, Mathf.Sin(t) * half.y);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}