using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player berjalan otomatis melewati daftar titik saat arena dimulai.
/// Selama berjalan, PlayerController dimatikan (gerak dan tombol Attack tidak bekerja),
/// dan dinyalakan lagi begitu titik terakhir tercapai.
/// Posisi awal = posisi Player di scene, jadi letakkan Player di pintu kiri saat edit.
/// </summary>
public class PlayerEntrance : MonoBehaviour
{
    [Header("Pemain")]
    [Tooltip("Kosong = dicari otomatis")]
    [SerializeField] private PlayerController player;

    [Header("Jalur masuk")]
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("Titik tujuan, urut dari atas ke bawah. Buat Empty GameObject, geser di Scene view, lalu seret ke sini. Klik + untuk menambah.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [Tooltip("Dianggap sampai kalau sudah sedekat ini dengan titik.")]
    [SerializeField] private float arriveDistance = 0.1f;
    [Tooltip("Batas waktu per titik (detik). Kalau Player tertahan collider, dia dipindahkan ke titik itu supaya game tidak macet.")]
    [SerializeField] private float timeoutPerPoint = 6f;

    [Header("Tembus tembok")]
    [Tooltip("Aktif: SEMUA collider Player dimatikan selama berjalan, jadi dia menembus tembok (dan tidak bisa kena cart/monster). " +
             "Dinyalakan lagi di titik terakhir. Titik terakhir HARUS di lantai, bukan di dalam tembok.")]
    [SerializeField] private bool ignoreWallsWhileWalking = true;

    public bool IsPlaying { get; private set; }
    /// <summary>True kalau jalan masuk berhenti di tengah karena Player mati.</summary>
    public bool Aborted { get; private set; }

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    PlayerHealth health;
    Coroutine routine;

    Collider2D[] bodyColliders;
    bool[] colliderWasEnabled;
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
        colliderWasEnabled = new bool[bodyColliders.Length];

        if (rb == null)
        {
            Debug.LogError($"{name}: Player tidak punya Rigidbody2D.");
            return false;
        }
        return true;
    }

    /// <summary>Matikan kontrol Player (gerak dan serang) dan hentikan geraknya.</summary>
    public void LockPlayer()
    {
        if (!Resolve()) return;
        player.enabled = false;
        player.moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        SetWalking(false);
    }

    /// <summary>Nyalakan kontrol lagi, kecuali Player sudah mati.</summary>
    public void Release()
    {
        if (!Resolve()) return;
        if (health != null && health.IsDead) return;
        player.moveInput = Vector2.zero;
        player.enabled = true;
    }

    /// <summary>Mulai berjalan. Tunggu sampai IsPlaying menjadi false.</summary>
    public void Play()
    {
        if (IsPlaying) return;
        if (!Resolve()) return;

        Aborted = false;
        LockPlayer();

        if (waypoints.Count == 0)
        {
            Debug.LogWarning($"{name}: Waypoints kosong, Player langsung bisa digerakkan.");
            Release();
            return;
        }

        WarnIfLastPointBlocked();

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
        SetWalkThrough(false);
        Release();
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
                    SetWalkThrough(false);
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
        SetWalkThrough(false);
        Release();
        IsPlaying = false;
        routine = null;
    }

    // matikan/nyalakan collider Player; keadaan awal tiap collider disimpan dan dikembalikan apa adanya
    void SetWalkThrough(bool on)
    {
        if (!ignoreWallsWhileWalking || bodyColliders == null) return;
        if (on == walkThroughActive) return;
        walkThroughActive = on;

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            var c = bodyColliders[i];
            if (c == null) continue;

            if (on) { colliderWasEnabled[i] = c.enabled; c.enabled = false; }
            else c.enabled = colliderWasEnabled[i];
        }
    }

    // kalau titik terakhir ada di tembok, Player akan terjepit begitu collider-nya dinyalakan lagi
    void WarnIfLastPointBlocked()
    {
        if (!ignoreWallsWhileWalking || PathGrid.Instance == null) return;

        for (int i = waypoints.Count - 1; i >= 0; i--)
        {
            if (waypoints[i] == null) continue;
            if (!PathGrid.Instance.IsWalkableAt(waypoints[i].position))
                Debug.LogWarning($"{name}: titik terakhir '{waypoints[i].name}' tidak berada di lantai yang aman " +
                                 "(dekat tembok atau di luar area). Player bisa terjepit saat collider dinyalakan lagi.");
            return;
        }
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
        Gizmos.color = Color.cyan;
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