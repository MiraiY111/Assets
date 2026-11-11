using System;
using UnityEngine;

/// <summary>
/// Level dan EXP Player. Pasang di objek kosong "ExpManager" di tiap stage.
/// Level mulai dari 0. Level/EXP dibawa antar scene lewat field static, dan baru "dikunci" saat stage selesai
/// (OnArenaCompleted). Jadi kalau Player mati lalu Restart, level/EXP kembali ke keadaan awal stage itu.
/// Panggil ExpSystem.ResetRun() saat memulai run baru (MainMenu), dan ExpSystem.LoadSnapshot() saat memuat save.
/// EXP dasar ada di prefab monster (EnemyExpReward); tiap stage mengalikannya dengan Exp Multiplier.
/// </summary>
public class ExpSystem : MonoBehaviour
{
    public static ExpSystem Instance { get; private set; }

    [Header("Kurva EXP")]
    [Tooltip("EXP untuk naik dari level 0, 1, 2, ... (urut). Elemen 0 = level 0 -> 1. " +
             "Nilai ini tersimpan di scene: kalau ExpManager sudah ada, ubah di sini juga, bukan hanya di script.")]
    [SerializeField] private int[] expToNext = { 12, 18, 28, 42, 60, 88 };
    [Tooltip("Kalau level melewati isi tabel, kebutuhan EXP = nilai terakhir tabel x angka ini per level.")]
    [SerializeField] private float growthAfterTable = 1.4f;

    [Header("Per stage")]
    [Tooltip("Pengali EXP yang didapat dari monster di stage ini. Stage 1 = 1, Stage 2 = 1.5.")]
    [SerializeField] private float expMultiplier = 1f;

    [Header("Terkait")]
    [Tooltip("Kosong = dicari otomatis. EXP dikunci saat OnArenaCompleted.")]
    [SerializeField] private WaveSpawner spawner;

    static int savedLevel = 0;
    static int savedExp = 0;

    public int Level { get; private set; }
    public int CurrentExp { get; private set; }

    /// <summary>Level yang dikunci di awal stage ini (yang disimpan oleh Save).</summary>
    public static int SnapshotLevel => savedLevel;
    /// <summary>EXP yang dikunci di awal stage ini (yang disimpan oleh Save).</summary>
    public static int SnapshotExp => savedExp;

    /// <summary>(levelLama, expLama, levelBaru, expBaru), dipanggil setelah EXP bertambah.</summary>
    public event Action<int, int, int, int> OnExpGained;
    /// <summary>Level baru, dipanggil sekali per level yang dilewati.</summary>
    public event Action<int> OnLevelUp;

    void Awake()
    {
        Instance = this;
        Level = savedLevel;
        CurrentExp = savedExp;
        PlayerUpgrades.BeginStage();   // kemampuan (Dash) kembali ke keadaan awal stage ini
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

    /// <summary>Run baru: Level dan EXP mulai lagi dari 0.</summary>
    public static void ResetRun()
    {
        savedLevel = 0;
        savedExp = 0;
        PlayerUpgrades.ResetRun();
    }

    /// <summary>Pasang Level/EXP dari save, dipanggil sebelum scene stage dimuat.</summary>
    public static void LoadSnapshot(int level, int exp)
    {
        savedLevel = Mathf.Max(0, level);
        savedExp = Mathf.Max(0, exp);
        PlayerUpgrades.LoadFromLevel(savedLevel);
    }

    /// <summary>Kunci level/EXP sekarang sebagai titik awal stage berikutnya.</summary>
    public void Commit()
    {
        savedLevel = Level;
        savedExp = CurrentExp;
        PlayerUpgrades.Commit();
    }

    /// <summary>EXP yang dibutuhkan untuk naik dari 'level' ke level berikutnya.</summary>
    public int ExpToNext(int level)
    {
        int idx = Mathf.Max(0, level);
        if (expToNext == null || expToNext.Length == 0) return 100;
        if (idx < expToNext.Length) return Mathf.Max(1, expToNext[idx]);

        float v = expToNext[expToNext.Length - 1] * Mathf.Pow(growthAfterTable, idx - expToNext.Length + 1);
        return Mathf.Max(1, Mathf.RoundToInt(v));
    }

    /// <summary>Tambah EXP dasar (dari prefab). Dikalikan Exp Multiplier stage ini.</summary>
    public void AddExp(int baseAmount)
    {
        int amount = Mathf.RoundToInt(baseAmount * expMultiplier);
        if (amount <= 0) return;

        int oldLevel = Level, oldExp = CurrentExp;
        CurrentExp += amount;

        while (CurrentExp >= ExpToNext(Level))
        {
            CurrentExp -= ExpToNext(Level);
            Level++;
            OnLevelUp?.Invoke(Level);
        }

        OnExpGained?.Invoke(oldLevel, oldExp, Level, CurrentExp);
    }
}