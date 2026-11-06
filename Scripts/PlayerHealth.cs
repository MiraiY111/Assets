using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHP = 100;
    [SerializeField] private float invulnerableTime = 0.5f;
    [SerializeField] private float flashTime = 0.1f;
    [SerializeField] private string deathClipName = "Orc_Death";
    [SerializeField] private float deathLengthFallback = 0.8f;

    public int CurrentHP { get; private set; }
    public int MaxHP => maxHP;
    public bool IsDead { get; private set; }

    public event Action<int, int> OnHealthChanged; // (current, max) untuk HUD
    public event Action OnDied;                    // dipanggil setelah animasi Death selesai

    SpriteRenderer sr;
    Rigidbody2D rb;
    Animator anim;
    float invulnerableUntil;

    void Awake()
    {
        CurrentHP = maxHP;
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || Time.time < invulnerableUntil) return;

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        invulnerableUntil = Time.time + invulnerableTime;
        Debug.Log($"Player HP: {CurrentHP}/{maxHP}");
        OnHealthChanged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP <= 0) { StartCoroutine(DieRoutine()); return; }

        SetTriggerSafe("Hurt");
        if (sr != null) StartCoroutine(Flash());
    }

    /// <summary>Pulihkan HP, tidak melebihi MaxHP. Diabaikan kalau player sudah mati.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
    }

    /// <summary>
    /// Naikkan MaxHP (dipanggil skill/upgrade). alsoHeal = true: HP saat ini ikut naik
    /// sebesar 'amount', jadi upgrade langsung terasa. Diabaikan kalau player sudah mati.
    /// </summary>
    public void AddMaxHP(int amount, bool alsoHeal = true)
    {
        if (IsDead || amount <= 0) return;
        maxHP += amount;
        if (alsoHeal) CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
    }

    IEnumerator DieRoutine()
    {
        IsDead = true;

        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false; // stop input dan gerak
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetBool("isWalking", false);
            anim.ResetTrigger("Attack");   // buang serangan yang masih tertunda
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("Hurt");
            SetTriggerSafe("Death");
        }

        // diagnosis: lihat state Animator setelah trigger diproses
        yield return null;
        yield return null;
        if (anim != null)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[DIAG] Orc_Death aktif? {st.IsName("Orc_Death")} | sedang transisi? {anim.IsInTransition(0)} | animator enabled? {anim.enabled}");
        }

        float len = ClipLength(deathClipName, deathLengthFallback);
        Debug.Log($"[DIAG] Durasi tunggu Death = {len}s");
        yield return new WaitForSeconds(len);

        Debug.Log("GAME OVER");
        OnDied?.Invoke();
    }

    IEnumerator Flash()
    {
        Color original = sr.color;
        sr.color = Color.red;
        yield return new WaitForSeconds(flashTime);
        if (sr != null && !IsDead) sr.color = original;
    }

    void SetTriggerSafe(string param)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (var p in anim.parameters)
            if (p.name == param && p.type == AnimatorControllerParameterType.Trigger)
            { anim.SetTrigger(param); return; }
        Debug.LogWarning($"Parameter '{param}' tidak ada di Animator Player.");
    }

    float ClipLength(string clipName, float fallback)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return fallback;
        foreach (var c in anim.runtimeAnimatorController.animationClips)
            if (c.name == clipName) return c.length;
        Debug.LogWarning($"Klip '{clipName}' tidak ditemukan, memakai {fallback}s");
        return fallback;
    }
}