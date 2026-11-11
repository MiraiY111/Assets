using System;
using UnityEngine;

/// <summary>
/// Jumlah koin Player. Pasang di objek kosong "CoinManager" di tiap stage (boleh digabung dengan ExpManager).
/// Pola sama dengan ExpSystem: koin dibawa antar scene lewat field static, dan baru "dikunci" saat stage selesai
/// (OnArenaCompleted). Mati lalu Restart mengembalikan koin ke jumlah saat masuk stage itu, jadi tidak ada
/// celah "mati sambil membawa koin dari stage yang gagal".
/// Panggil CoinSystem.ResetRun() saat run baru (MainMenu), dan CoinSystem.LoadSnapshot() saat memuat save.
/// Jumlah koin dasar ada di prefab monster (EnemyCoinReward); tiap stage mengalikannya dengan Coin Multiplier.
///
/// Angka di HUD naik bertahap SAAT koin terbang sampai (diatur CoinCounterUI), bukan saat monster mati,
/// tapi nilai koin di sini langsung bertambah saat monster mati, jadi Save/Commit selalu benar.
/// </summary>
public class CoinSystem : MonoBehaviour
{
    public static CoinSystem Instance { get; private set; }

    [Header("Per stage")]
    [Tooltip("Pengali koin dari monster di stage ini. Stage 1 = 1, Stage 2 = 1.5 (sama polanya dengan EXP).")]
    [SerializeField] private float coinMultiplier = 1f;

    [Header("Terkait")]
    [Tooltip("Kosong = dicari otomatis. Koin dikunci saat OnArenaCompleted.")]
    [SerializeField] private WaveSpawner spawner;

    static int savedCoins = 0;

    /// <summary>Jumlah koin sekarang (langsung bertambah saat monster mati, tidak menunggu animasi HUD).</summary>
    public int Coins { get; private set; }

    /// <summary>Koin yang dikunci di awal stage ini (yang disimpan oleh Save).</summary>
    public static int SnapshotCoins => savedCoins;

    /// <summary>(jumlah koin, posisi dunia tempat koin muncul). Dipakai CoinFlyEffect untuk koin terbang.</summary>
    public event Action<int, Vector3> OnCoinsDropped;
    /// <summary>Total koin baru, setelah bertambah atau berkurang.</summary>
    public event Action<int> OnCoinsChanged;

    void Awake()
    {
        Instance = this;
        Coins = savedCoins;
    }

    void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();
        if (spawner != null) spawner.OnArenaCompleted += Commit;
    }

    void OnDestroy()
    {
        if (spawner != null) spawner.OnArenaCompleted -= Commit;
        if (Instance == this) Instance = null;
    }

    /// <summary>Run baru: koin mulai lagi dari 0.</summary>
    public static void ResetRun()
    {
        savedCoins = 0;
    }

    /// <summary>Pasang koin dari save, dipanggil sebelum scene stage dimuat.</summary>
    public static void LoadSnapshot(int coins)
    {
        savedCoins = Mathf.Max(0, coins);
    }

    /// <summary>Kunci koin sekarang sebagai titik awal stage berikutnya.</summary>
    public void Commit()
    {
        savedCoins = Coins;
    }

    /// <summary>Tambah koin dasar (dari prefab monster), dikalikan Coin Multiplier stage ini.</summary>
    public void AddCoins(int baseAmount, Vector3 worldPos)
    {
        int amount = Mathf.RoundToInt(baseAmount * coinMultiplier);
        if (amount <= 0) return;

        Coins += amount;
        OnCoinsChanged?.Invoke(Coins);
        OnCoinsDropped?.Invoke(amount, worldPos);
    }

    /// <summary>Belanja (untuk toko nanti). False kalau koin tidak cukup.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        OnCoinsChanged?.Invoke(Coins);
        return true;
    }
}
