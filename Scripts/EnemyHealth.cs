using System;
using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHP = 3;
    [SerializeField] private float flashTime = 0.1f;

    [Header("Animator (samakan dengan controller bawaan)")]
    [SerializeField] private string hurtParam = "Hurt";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string hurtState = "Hurt";   // nama state, dipakai kalau parameter tidak ada
    [SerializeField] private string deathState = "Death";

    public int CurrentHP { get; private set; }
    public int MaxHP => maxHP;
    public bool IsDead { get; private set; }

    public event Action<int, int> OnHealthChanged; // (current, max) untuk HP bar di atas monster
    public event Action<EnemyHealth> OnDied;       // nanti dipakai sistem ronde

    SpriteRenderer sr;
    Animator anim;
    Color baseColor = Color.white;

    void Awake()
    {
        CurrentHP = maxHP;
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        if (sr != null) baseColor = sr.color;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        Debug.Log($"{name} HP: {CurrentHP}/{maxHP}");
        OnHealthChanged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP <= 0) { Die(); return; }

        PlayAnim(hurtParam, hurtState);
        if (sr != null) StartCoroutine(Flash());
    }

    void Die()
    {
        IsDead = true;
        PlayAnim(deathParam, deathState);
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        OnDied?.Invoke(this);
        Destroy(gameObject, ClipLength(deathState, 0.6f) + 0.1f);
    }

    IEnumerator Flash()
    {
        sr.color = Color.red;
        yield return new WaitForSeconds(flashTime);
        if (sr != null && !IsDead) sr.color = baseColor;
    }

    // Coba trigger dulu; kalau parameternya tidak ada, langsung putar state berdasarkan nama.
    void PlayAnim(string param, string stateName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;

        foreach (var p in anim.parameters)
        {
            if (p.name == param && p.type == AnimatorControllerParameterType.Trigger)
            {
                anim.SetTrigger(param);
                return;
            }
        }

        int hash = Animator.StringToHash(stateName);
        if (anim.HasState(0, hash)) anim.Play(hash, 0, 0f);
        else Debug.LogWarning($"{name}: tidak ada parameter '{param}' maupun state '{stateName}'.");
    }

    float ClipLength(string clipName, float fallback)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return fallback;
        foreach (var c in anim.runtimeAnimatorController.animationClips)
            if (c.name == clipName) return c.length;
        return fallback;
    }
}