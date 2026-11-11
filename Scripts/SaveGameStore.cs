using System;
using UnityEngine;

/// <summary>
/// Isi satu slot save. Disimpan sebagai JSON di PlayerPrefs.
/// Yang disimpan adalah keadaan di AWAL stage (titik kunci), bukan keadaan saat tombol Save ditekan.
/// Jadi load = mengulang stage itu dari awal dengan Level/EXP yang dimiliki sebelum masuk stage.
///
/// Mau menyimpan data baru (koin, inventory, dll)? Tambah field di sini, isi di SaveGameStore.Save(),
/// lalu pasang kembali di MainMenu.Play() sebelum scene dimuat. Field baru yang tidak ada di save lama
/// otomatis bernilai 0 / kosong.
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;
    public string sceneName;
    public int level;
    public int exp;
    public int coins;
    // Nanti: public int coins; public string[] inventory; dst. (nilai awal stage, bukan nilai saat menekan Save)
}

public static class SaveGameStore
{
    const string Key = "SaveGame_v1";

    public static bool HasSave => PlayerPrefs.HasKey(Key);

    /// <summary>Simpan titik awal stage 'sceneName' beserta Level/EXP yang dimiliki sebelum masuk stage itu.</summary>
    public static void Save(string sceneName)
    {
        var data = new SaveData
        {
            sceneName = sceneName,
            level = ExpSystem.SnapshotLevel,
            exp = ExpSystem.SnapshotExp,
            coins = CoinSystem.SnapshotCoins
        };

        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>False kalau belum ada save atau datanya rusak.</summary>
    public static bool TryLoad(out SaveData data)
    {
        data = null;
        if (!PlayerPrefs.HasKey(Key)) return false;

        try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key)); }
        catch (Exception) { data = null; }

        if (data == null || string.IsNullOrEmpty(data.sceneName))
        {
            data = null;
            return false;
        }

        data.level = Mathf.Max(0, data.level);
        data.exp = Mathf.Max(0, data.exp);
        data.coins = Mathf.Max(0, data.coins);
        return true;
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}