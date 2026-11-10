using UnityEngine;

/// <summary>
/// Pasang di prefab monster atau miniboss (satu objek dengan EnemyHealth).
/// Memberi EXP ke ExpSystem saat monster mati.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyExpReward : MonoBehaviour
{
    [Tooltip("EXP yang diberikan saat monster ini mati.")]
    [SerializeField] private int exp = 4;

    EnemyHealth health;

    void Awake() { health = GetComponent<EnemyHealth>(); }

    void Start()
    {
        if (health != null) health.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
    }

    void HandleDied(EnemyHealth e)
    {
        if (ExpSystem.Instance != null) ExpSystem.Instance.AddExp(exp);
    }
}
