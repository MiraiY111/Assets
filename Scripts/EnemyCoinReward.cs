using UnityEngine;

/// <summary>
/// Pasang di prefab monster atau miniboss (satu objek dengan EnemyHealth, di samping EnemyExpReward).
/// Saat mati, monster menjatuhkan koin acak antara Min dan Max (lalu dikalikan Coin Multiplier stage di CoinSystem).
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyCoinReward : MonoBehaviour
{
    [Tooltip("Koin dasar paling sedikit yang dijatuhkan.")]
    [SerializeField] private int minCoins = 1;
    [Tooltip("Koin dasar paling banyak yang dijatuhkan.")]
    [SerializeField] private int maxCoins = 3;

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
        if (CoinSystem.Instance == null) return;

        int lo = Mathf.Max(0, minCoins);
        int hi = Mathf.Max(lo, maxCoins);
        int amount = Random.Range(lo, hi + 1);
        CoinSystem.Instance.AddCoins(amount, transform.position);
    }
}
