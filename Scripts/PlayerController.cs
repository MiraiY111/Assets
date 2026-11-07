using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Gerak")]
    public float moveSpeed = 3f;
    public Joystick joystick;
    public Vector2 moveInput;

    [Header("Dash")]
    public float dashSpeed = 9f;          // kecepatan saat dash
    public float dashDuration = 0.15f;    // lama dash (jarak = speed x durasi, +-1,35 unit)
    public float dashCooldown = 0.8f;     // jeda sebelum bisa dash lagi, dihitung dari akhir dash
    [Tooltip("Skala sprite saat dash. X > 1 melebar, Y < 1 memipih (gepeng).")]
    public float dashSquashX = 1.3f;
    public float dashSquashY = 0.7f;
    [Tooltip("Opsional: Particle System asap (child Player). Kosong = dash tanpa asap.")]
    public ParticleSystem dashSmoke;
    [Tooltip("Posisi asap relatif ke Player untuk dash KE KANAN. X negatif = di belakang, Y negatif = ke bawah. Dash ke kiri dicerminkan otomatis; dash atas/bawah memakai jarak |X|. Posisi Transform DashSmoke diabaikan.")]
    public Vector2 dashSmokeOffset = new Vector2(-0.3f, -0.4f);

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

    bool isDashing;
    float dashUntil;
    float dashReadyAt;
    Vector2 dashDir;
    Vector3 baseScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        if (attack == null) attack = GetComponent<PlayerAttack>();
        baseScale = transform.localScale;

        attack1Length = GetClipLength("Orc_Attack1", attack1Length);
        attack2Length = GetClipLength("Orc_Attack2", attack2Length);
    }

    // Player mati (PlayerHealth mematikan script ini) atau dimatikan PlayerEntrance/Exit:
    // pastikan sprite tidak tertinggal gepeng dan asap berhenti.
    void OnDisable()
    {
        if (isDashing) EndDash();
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

        // tes dash di editor (dibaca setelah moveInput supaya arahnya selalu terbaru)
        if (Input.GetKeyDown(KeyCode.E)) OnDashButton();

        if (isDashing && Time.time >= dashUntil) EndDash();

        bool knockedBack = Time.time < knockbackUntil;

        anim.SetBool("isWalking", isDashing || (!knockedBack && moveInput.sqrMagnitude > 0.01f));

        // saat terpental, arah hadap tidak ikut input
        if (!knockedBack)
        {
            Vector2 faceSource = isDashing ? dashDir : moveInput;
            if (faceSource.x > 0.01f) sr.flipX = false;
            else if (faceSource.x < -0.01f) sr.flipX = true;
        }
    }

    void FixedUpdate()
    {
        if (Time.time < knockbackUntil)
            rb.linearVelocity = knockbackVelocity;
        else if (isDashing)
            rb.linearVelocity = dashDir * dashSpeed;
        else
            rb.linearVelocity = moveInput * moveSpeed;
    }

    /// <summary>Dipanggil benda berbahaya (cart, dll). Input gerak diabaikan selama 'duration'.</summary>
    public void ApplyKnockback(Vector2 velocity, float duration)
    {
        if (!enabled) return;
        if (isDashing) EndDash(); // terpental membatalkan dash
        knockbackVelocity = velocity;
        knockbackUntil = Time.time + duration;
    }

    // dipasang di OnClick tombol Dash (mobile); di editor dipanggil tombol E
    public void OnDashButton()
    {
        // Player sudah mati / dimatikan
        if (!enabled) return;

        // sedang dash, terpental, atau masih cooldown
        if (isDashing) return;
        if (Time.time < knockbackUntil) return;
        if (Time.time < dashReadyAt) return;

        // sedang menyerang (termasuk jeda setelah Attack2). Hapus baris ini kalau dash boleh memotong serangan.
        if (Time.time < busyUntil) return;

        // arah = input saat ini; kalau tidak ada input, ke arah hadap terakhir
        dashDir = moveInput.sqrMagnitude > 0.01f
            ? moveInput.normalized
            : (sr.flipX ? Vector2.left : Vector2.right);

        isDashing = true;
        dashUntil = Time.time + dashDuration;

        // batalkan combo yang menggantung
        comboDeadline = -1f;

        // dash lebih ke atas/bawah -> bentuk ikut sumbu vertikal (sempit di X, memanjang di Y)
        bool vertical = Mathf.Abs(dashDir.y) > Mathf.Abs(dashDir.x);

        transform.localScale = vertical
            ? new Vector3(baseScale.x * dashSquashY, baseScale.y * dashSquashX, baseScale.z)
            : new Vector3(baseScale.x * dashSquashX, baseScale.y * dashSquashY, baseScale.z);

        if (dashSmoke != null)
        {
            // asap digambar untuk dash ke kanan (ekor di kiri):
            // atas = putar +90, bawah = putar -90, kiri = cermin (Y 180)
            float rotZ = vertical ? (dashDir.y > 0f ? 90f : -90f) : 0f;
            float rotY = (!vertical && dashDir.x < 0f) ? 180f : 0f;
            dashSmoke.transform.localRotation = Quaternion.Euler(0f, rotY, rotZ);

            // posisi: selalu di belakang arah dash. Child tidak ikut flipX, jadi dihitung manual.
            // samping = cerminkan X; atas/bawah = geser sejauh jarak "belakang" di sumbu Y
            float behind = Mathf.Abs(dashSmokeOffset.x);
            Vector3 pos = vertical
                ? new Vector3(0f, dashDir.y > 0f ? -behind : behind, 0f)
                : new Vector3(dashDir.x < 0f ? -dashSmokeOffset.x : dashSmokeOffset.x, dashSmokeOffset.y, 0f);
            dashSmoke.transform.localPosition = pos;
            dashSmoke.Play();
        }
    }

    void EndDash()
    {
        isDashing = false;
        dashReadyAt = Time.time + dashCooldown;
        transform.localScale = baseScale;
        if (dashSmoke != null) dashSmoke.Stop(); // partikel yang sudah keluar tetap memudar sendiri
    }

    // ---------- [DASHBTN] dibaca DashButton (tombol UI dash) ----------

    public float DashCooldown => dashCooldown;

    // cooldown dihitung dari AKHIR dash, jadi selama dash tombol tetap tampil penuh abu-abu,
    // lalu mulai berputar begitu dash selesai
    public float DashRemaining => isDashing ? dashCooldown : Mathf.Max(0f, dashReadyAt - Time.time);

    // true hanya kalau dash benar-benar mulai (bukan ditolak cooldown, terpental, atau sedang menyerang)
    public bool TryDash()
    {
        bool wasDashing = isDashing;
        OnDashButton();
        return !wasDashing && isDashing;
    }

    // dipasang di OnClick tombol Attack
    public void OnAttackButton()
    {
        // Player sudah mati (PlayerHealth mematikan script ini), jangan serang
        if (!enabled) return;

        // sedang dash
        if (isDashing) return;

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