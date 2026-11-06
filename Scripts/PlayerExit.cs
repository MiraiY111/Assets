using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player berjalan otomatis keluar arena lewat daftar titik setelah boss kalah (dipanggil WaveSpawner).
/// Selama berjalan, PlayerController dimatikan (gerak dan tombol Attack tidak bekerja).
/// Di titik terakhir Player TETAP terkunci (game dianggap selesai) dan event OnFinished dipanggil.
/// Titik-titik diatur sendiri: buat Empty GameObject, geser di Scene view, seret ke daftar Waypoints.
/// </summary>
public class PlayerExit : MonoBehaviour
{
    [Header("Pemain")]
    [Tooltip("Kosong = dicari otomatis")]
    [SerializeField] private PlayerController player;

    [Header("Jalur keluar")]
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("Titik tujuan, urut dari yang pertama dilewati. Titik terakhir = tempat Player menghilang (boleh di luar layar). Klik + untuk menambah.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [Tooltip("Dianggap sampai kalau sudah sedekat ini dengan titik.")]
    [SerializeField] private float arriveDistance = 0.1f;
    [Tooltip("Batas waktu per titik (detik). Kalau Player macet, dia dipindahkan ke titik itu supaya game tidak macet.")]
    [SerializeField] private float timeoutPerPoint = 6f;

    [Header("Tembus tembok")]
    [Tooltip("Aktif: SEMUA collider Player dimatikan selama berjalan (menembus tembok/pintu). " +
             "Karena game sudah selesai, collider TIDAK dinyalakan lagi di akhir.")]
    [SerializeField] private bool ignoreWallsWhileWalking = true;

    [Header("Setelah sampai")]
    [Tooltip("Sembunyikan sprite Player di titik terakhir (kalau titik terakhir masih terlihat di layar).")]
    [SerializeField] private bool hidePlayerAtEnd = false;

    public bool IsPlaying { get; private set; }
    /// <summary>True kalau jalan keluar berhenti di tengah karena Player mati.</summary>
    public bool Aborted { get; private set; }

    /// <summary>Dipanggil saat Player sampai di titik terakhir.</summary>
    public event Action OnFinished;

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    PlayerHealth health;
    Coroutine routine;

    Collider2D[] bodyColliders;
    bool walkThroughActive;

    bool Resolve()
    {
        if (rb != null) return true;

        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError($"{name}: PlayerController tidak ditemukan.");
            return false;
        }

        rb = player.GetComponent<Rigidbody2D>();
        anim = player.GetComponent<Animator>();
        sr = player.GetComponent<SpriteRenderer>();
        health = player.GetComponent<PlayerHealth>();
        bodyColliders = player.GetComponentsInChildren<Collider2D>();

        if (rb == null)
        {
            Debug.LogError($"{name}: Player tidak punya Rigidbody2D.");
            return false;
        }
        return true;
    }

    void LockPlayer()
    {
        player.enabled = false;
        player.moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        SetWalking(false);
    }

    /// <summary>Mulai berjalan keluar. Tunggu sampai IsPlaying menjadi false.</summary>
    public void Play()
    {
        if (IsPlaying) return;
        if (!Resolve()) return;

        Aborted = false;

        if (health != null && health.IsDead)
        {
            Aborted = true;
            return;
        }

        LockPlayer();

        if (waypoints.Count == 0)
        {
            Debug.LogWarning($"{name}: Waypoints kosong, Player langsung dianggap sudah keluar.");
            OnFinished?.Invoke();
            return;
        }

        IsPlaying = true;
        SetWalkThrough(true);
        routine = StartCoroutine(Run());
    }

    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        IsPlaying = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetWalking(false);
    }

    IEnumerator Run()
    {
        foreach (var wp in waypoints)
        {
            if (wp == null) continue;

            float elapsed = 0f;
            while (true)
            {
                if (health != null && health.IsDead)
                {
                    rb.linearVelocity = Vector2.zero;
                    Aborted = true;
                    IsPlaying = false;
                    routine = null;
                    yield break;
                }

                Vector2 to = (Vector2)wp.position - rb.position;
                float step = walkSpeed * Time.fixedDeltaTime;
                if (to.magnitude <= Mathf.Max(arriveDistance, step)) break;

                if (elapsed > timeoutPerPoint)
                {
                    Debug.LogWarning($"{name}: Player macet menuju '{wp.name}', dipindahkan ke titik itu.");
                    rb.position = wp.position;
                    break;
                }

                rb.linearVelocity = to.normalized * walkSpeed;
                Face(to.x);
                SetWalking(true);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        rb.linearVelocity = Vector2.zero;
        SetWalking(false);
        if (hidePlayerAtEnd && sr != null) sr.enabled = false;

        IsPlaying = false;
        routine = null;
        OnFinished?.Invoke();
    }

    // collider dimatikan dan sengaja tidak dikembalikan: setelah keluar, game sudah selesai
    void SetWalkThrough(bool on)
    {
        if (!ignoreWallsWhileWalking || bodyColliders == null) return;
        if (on == walkThroughActive) return;
        walkThroughActive = on;

        foreach (var c in bodyColliders)
            if (c != null) c.enabled = !on;
    }

    // flipX false = menghadap kanan (sama dengan PlayerController)
    void Face(float dx)
    {
        if (sr == null) return;
        if (dx > 0.01f) sr.flipX = false;
        else if (dx < -0.01f) sr.flipX = true;
    }

    void SetWalking(bool on)
    {
        if (anim != null && anim.runtimeAnimatorController != null) anim.SetBool("isWalking", on);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        bool hasPrev = false;
        Vector3 prev = default;

        var p = player != null ? player.transform : null;
        if (p != null) { prev = p.position; hasPrev = true; }

        foreach (var wp in waypoints)
        {
            if (wp == null) continue;
            Gizmos.DrawWireSphere(wp.position, 0.2f);
            if (hasPrev) Gizmos.DrawLine(prev, wp.position);
            prev = wp.position;
            hasPrev = true;
        }
    }
}