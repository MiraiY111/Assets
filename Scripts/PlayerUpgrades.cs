/// <summary>
/// Kemampuan yang dibuka lewat kartu level-up (sekarang: Dash). Static, jadi terbawa antar scene.
/// Polanya sama dengan ExpSystem/CoinSystem: nilai "sekarang" (dashNow) dipakai selama stage, dan baru "dikunci"
/// (dashSaved) saat stage selesai. Mati lalu Restart mengembalikan Dash ke keadaan awal stage itu,
/// sama seperti Level yang ikut kembali, jadi kartunya muncul lagi saat Level 1 tercapai lagi.
///
/// Dipanggil dari ExpSystem (BeginStage, Commit, ResetRun, LoadFromLevel). Tidak perlu dipasang di objek mana pun.
/// </summary>
public static class PlayerUpgrades
{
    static bool dashSaved;
    static bool dashNow;

    /// <summary>True kalau kartu Dash sudah dipilih. Dibaca PlayerController dan DashButtonGate.</summary>
    public static bool DashUnlocked => dashNow;

    /// <summary>Dipanggil UpgradeCardUI saat pemain memilih kartu Dash.</summary>
    public static void UnlockDash() { dashNow = true; }

    /// <summary>Awal stage: pakai keadaan terkunci (membuang Dash yang dibuka di stage yang gagal).</summary>
    public static void BeginStage() { dashNow = dashSaved; }

    /// <summary>Stage selesai: kunci keadaan sekarang sebagai titik awal stage berikutnya.</summary>
    public static void Commit() { dashSaved = dashNow; }

    /// <summary>Run baru: semua kemampuan terkunci lagi.</summary>
    public static void ResetRun()
    {
        dashSaved = false;
        dashNow = false;
    }

    /// <summary>
    /// Memuat save. Save hanya menyimpan Level, dan kartu Dash wajib dipilih saat Level 1 tercapai,
    /// jadi Level 1 ke atas berarti Dash sudah terbuka.
    /// </summary>
    public static void LoadFromLevel(int level)
    {
        dashSaved = level >= 1;
        dashNow = dashSaved;
    }
}
