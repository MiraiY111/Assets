using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Gerak")]
    public float moveSpeed = 3f;
    public Joystick joystick;
    public Vector2 moveInput;

    [Header("Serangan")]
    public float comboWindow = 0.5f;     // waktu setelah Attack1 selesai untuk menekan Attack2
    public float attack2Cooldown = 0.5f; // jeda setelah Attack2 selesai sebelum bisa menyerang lagi

    // diisi otomatis dari panjang klip; angka ini hanya cadangan kalau klip tidak ditemukan
    public float attack1Length = 0.5f;
    public float attack2Length = 0.5f;

    [Header("Hitbox (Hari 9)")]
    public PlayerAttack attack;          // kosongkan = dicari otomatis di Player
    public int attack1Damage = 1;
    public int attack2Damage = 1;
    [Tooltip("Kapan damage kena, sebagai bagian dari durasi klip (0 = awal, 1 = akhir). Samakan dengan frame saat pedang mengayun.")]
    [Range(0f, 1f)] public float attack1HitPoint = 0.5f;
    [Range(0f, 1f)] public float attack2HitPoint = 0.5f;

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;

    float busyUntil;           // tidak bisa menyerang sebelum waktu ini
    float comboDeadline = -1f; // batas akhir menekan Attack2

    float knockbackUntil;      // selama waktu ini, gerak dikendalikan knockback
    Vector2 knockbackVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        if (attack == null) attack = GetComponent<PlayerAttack>();

        attack1Length = GetClipLength("Orc_Attack1", attack1Length);
        attack2Length = GetClipLength("Orc_Attack2", attack2Length);
    }

    float GetClipLength(string clipName, float fallback)
    {
        if (anim.runtimeAnimatorController == null) return fallback;
        foreach (var c in anim.runtimeAnimatorController.animationClips)
            if (c.name == clipName) return c.length;

        Debug.LogWarning($"Klip '{clipName}' tidak ditemukan, memakai durasi cadangan {fallback}s");
        return fallback;
    }

    void Update()
    {
        // tes di editor
        if (Input.GetKeyDown(KeyCode.Space)) OnAttackButton();

        Vector2 kb = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        Vector2 js = joystick != null
            ? new Vector2(joystick.Horizontal, joystick.Vertical)
            : Vector2.zero;

        // joystick diprioritaskan; keyboard jadi cadangan untuk tes di editor
        moveInput = Vector2.ClampMagnitude(js.sqrMagnitude > 0.01f ? js : kb, 1f);

        bool knockedBack = Time.time < knockbackUntil;

        anim.SetBool("isWalking", !knockedBack && moveInput.sqrMagnitude > 0.01f);

        // saat terpental, arah hadap tidak ikut input
        if (!knockedBack)
        {
            if (moveInput.x > 0.01f) sr.flipX = false;
            else if (moveInput.x < -0.01f) sr.flipX = true;
        }
    }

    void FixedUpdate()
    {
        if (Time.time < knockbackUntil)
            rb.linearVelocity = knockbackVelocity;
        else
            rb.linearVelocity = moveInput * moveSpeed;
    }

    /// <summary>Dipanggil benda berbahaya (cart, dll). Input gerak diabaikan selama 'duration'.</summary>
    public void ApplyKnockback(Vector2 velocity, float duration)
    {
        if (!enabled) return;
        knockbackVelocity = velocity;
        knockbackUntil = Time.time + duration;
    }

    // dipasang di OnClick tombol Attack
    public void OnAttackButton()
    {
        // Player sudah mati (PlayerHealth mematikan script ini), jangan serang
        if (!enabled) return;

        // sedang terpental
        if (Time.time < knockbackUntil) return;

        // sedang menyerang, atau masih dalam jeda setelah Attack2
        if (Time.time < busyUntil) return;

        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Attack2");

        if (Time.time <= comboDeadline)
        {
            // Attack1 sudah selesai, klik masih dalam 0,5 detik -> Attack2
            anim.SetTrigger("Attack2");
            busyUntil = Time.time + attack2Length + attack2Cooldown;
            comboDeadline = -1f;
            if (attack != null) attack.HitAfter(attack2Length * attack2HitPoint, attack2Damage);
        }
        else
        {
            // klik pertama, atau jendela sudah lewat -> Attack1
            anim.SetTrigger("Attack");
            busyUntil = Time.time + attack1Length;
            comboDeadline = busyUntil + comboWindow;
            if (attack != null) attack.HitAfter(attack1Length * attack1HitPoint, attack1Damage);
        }
    }

    // Dibiarkan kosong supaya Animation Event lama tidak menimbulkan error.
    public void EndAttack() { }
}