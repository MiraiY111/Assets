using System.Collections.Generic;
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
    [Tooltip("[DASH CANCEL] Nama state Animator yang langsung diputar saat dash memotong serangan (animasi jalan). Kosong = animasi serangan hanya berhenti lewat transisi Animator.")]
    public string dashAnimState = "Orc_Walk";

    [Header("Dash: bayangan")]
    [Tooltip("Jarak antar bayangan (unit dunia). Kecil = rapat, besar = renggang. Sekitar selebar badan Player kalau mau seperti contoh.")]
    public float ghostSpacing = 0.5f;
    [Tooltip("Lama tiap bayangan memudar sampai hilang (detik).")]
    public float ghostLifetime = 0.3f;
    [Tooltip("Warna dan transparansi awal bayangan. Warna dikalikan dengan warna sprite, jadi pilih yang gelap/pudar. A = transparansi awal.")]
    public Color ghostColor = new Color(0.25f, 0.6f, 0.6f, 0.6f);

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

    // bayangan dash
    struct Ghost { public SpriteRenderer sr; public float spawnTime; }
    readonly List<Ghost> activeGhosts = new List<Ghost>();
    readonly Stack<SpriteRenderer> ghostPool = new Stack<SpriteRenderer>();
    Vector2 lastGhostPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        if (attack == null) attack = GetComponent<PlayerAttack>();

        attack1Length = GetClipLength("Orc_Attack1", attack1Length);
        attack2Length = GetClipLength("Orc_Attack2", attack2Length);
    }

    // Player mati (PlayerHealth mematikan script ini) atau dimatikan PlayerEntrance/Exit:
    // hentikan dash dan buang bayangan (Update berhenti, jadi bayangan tidak akan memudar sendiri).
    void OnDisable()
    {
        if (isDashing) EndDash();
        ClearGhosts();
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
        // [PAUSE] Time.timeScale = 0 menghentikan fisika dan Time.time, tetapi Update tetap berjalan.
        // Tanpa pengecekan ini, input A/D tetap membalik sprite dan tombol Attack/Dash tetap "terpasang"
        // lalu baru jalan setelah Resume.
        if (GamePause.IsPaused)
        {
            moveInput = Vector2.zero;
            return;
        }

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

        // tinggalkan bayangan tiap ghostSpacing unit selama dash
        if (isDashing && Vector2.Distance(transform.position, lastGhostPos) >= ghostSpacing)
            SpawnGhost();
        UpdateGhosts();

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

        // [PAUSE] tombol Dash di HUD tidak boleh dianggap saat game di-pause
        if (GamePause.IsPaused) return;

        // sedang dash, terpental, atau masih cooldown
        if (isDashing) return;
        if (Time.time < knockbackUntil) return;
        if (Time.time < dashReadyAt) return;

        // [DASH CANCEL] dash boleh memotong serangan (termasuk jeda setelah Attack2): animasi serangan diputus
        // dan damage yang belum jatuh dibatalkan. Sengaja SETELAH semua pengecekan di atas, jadi dash yang
        // ditolak (cooldown/terpental) tidak ikut membatalkan serangan.
        if (Time.time < busyUntil) CancelAttack();

        // arah = input saat ini; kalau tidak ada input, ke arah hadap terakhir
        dashDir = moveInput.sqrMagnitude > 0.01f
            ? moveInput.normalized
            : (sr.flipX ? Vector2.left : Vector2.right);

        isDashing = true;
        dashUntil = Time.time + dashDuration;

        // batalkan combo yang menggantung
        comboDeadline = -1f;

        SpawnGhost(); // bayangan pertama di titik awal
    }

    void EndDash()
    {
        isDashing = false;
        dashReadyAt = Time.time + dashCooldown;
        // bayangan yang sudah ada tetap memudar sendiri di UpdateGhosts()
    }

    // [DASH CANCEL] putus serangan yang sedang berjalan sekarang juga
    void CancelAttack()
    {
        busyUntil = 0f;
        comboDeadline = -1f;
        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Attack2");

        // damage yang dijadwalkan tapi belum jatuh dibatalkan (serangan dipotong sebelum titik hit = tidak kena)
        if (attack != null) attack.CancelPending();

        // putus animasi serangan sekarang juga, jangan menunggu transisi/exit time Animator
        anim.SetBool("isWalking", true);
        if (!string.IsNullOrEmpty(dashAnimState))
        {
            int hash = Animator.StringToHash(dashAnimState);
            if (anim.HasState(0, hash)) anim.Play(hash, 0, 0f);
            else Debug.LogWarning($"State '{dashAnimState}' tidak ada di Animator Player. Isi Dash Anim State dengan nama state jalan.");
        }
    }

    // ---------- bayangan dash ----------

    void SpawnGhost()
    {
        SpriteRenderer g = ghostPool.Count > 0 ? ghostPool.Pop() : null;
        if (g == null)
        {
            var go = new GameObject("DashGhost");
            g = go.AddComponent<SpriteRenderer>();
        }

        g.sprite = sr.sprite;
        g.flipX = sr.flipX;
        g.sharedMaterial = sr.sharedMaterial;
        g.sortingLayerID = sr.sortingLayerID;
        g.sortingOrder = sr.sortingOrder - 1;   // tepat di belakang Player
        g.color = ghostColor;

        g.transform.SetPositionAndRotation(transform.position, transform.rotation);
        g.transform.localScale = transform.lossyScale;
        g.gameObject.SetActive(true);

        activeGhosts.Add(new Ghost { sr = g, spawnTime = Time.time });
        lastGhostPos = transform.position;
    }

    void UpdateGhosts()
    {
        float life = Mathf.Max(0.01f, ghostLifetime);
        for (int i = activeGhosts.Count - 1; i >= 0; i--)
        {
            var gh = activeGhosts[i];
            if (gh.sr == null) { activeGhosts.RemoveAt(i); continue; }

            float t = (Time.time - gh.spawnTime) / life;
            if (t >= 1f)
            {
                gh.sr.gameObject.SetActive(false);
                ghostPool.Push(gh.sr);
                activeGhosts.RemoveAt(i);
                continue;
            }

            Color c = ghostColor;
            c.a = Mathf.Lerp(ghostColor.a, 0f, t);
            gh.sr.color = c;
        }
    }

    void ClearGhosts()
    {
        for (int i = activeGhosts.Count - 1; i >= 0; i--)
        {
            var gh = activeGhosts[i];
            if (gh.sr == null) continue;
            gh.sr.gameObject.SetActive(false);
            ghostPool.Push(gh.sr);
        }
        activeGhosts.Clear();
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

        // [PAUSE] serangan tidak boleh dianggap saat game di-pause
        if (GamePause.IsPaused) return;

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