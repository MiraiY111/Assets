using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// HUD jumlah koin: ikon koin + angka.
/// Angka naik BERTAHAP (bukan loncat dari 0 ke 50), dan ikon + angka membal sebentar tiap ada koin yang sampai.
/// Kalau ada CoinFlyEffect di scene, angka bertambah saat tiap koin terbang tiba di ikon. Kalau tidak ada,
/// angka bertambah langsung saat koin didapat (tetap bertahap dan tetap membal).
///
/// Susunan: objek induk "CoinCounter" (beri CustomizableUI dengan ID "Coin" kalau mau bisa digeser pemain),
/// dengan dua anak: Icon (Image) dan Count (TMP). Isi field Icon Target dan Count Text.
/// Jangan membuat induknya sendiri yang membal: skala induk bisa dipakai UI Customize, jadi yang membal
/// hanya ikon dan angkanya.
/// Animasi memakai waktu unscaled, sama seperti ExpBarUI.
/// </summary>
public class CoinCounterUI : MonoBehaviour
{
    [SerializeField] private CoinSystem coins;   // kosong = dicari otomatis
    [Tooltip("Angka koin.")]
    [SerializeField] private TMP_Text countText;
    [Tooltip("Ikon koin. Ini tujuan koin yang terbang, dan ikut membal.")]
    [SerializeField] private RectTransform iconTarget;
    [SerializeField] private string format = "{0}";

    [Header("Angka naik bertahap")]
    [Tooltip("Kecepatan minimum angka naik (koin per detik).")]
    [SerializeField] private float minCountSpeed = 12f;
    [Tooltip("Kalau selisihnya besar, angka menyusul dalam waktu ini (detik), jadi tidak terlalu lama.")]
    [SerializeField] private float catchUpTime = 0.6f;

    [Header("Membal tiap dapat koin")]
    [SerializeField] private float popScale = 1.3f;
    [SerializeField] private float popTime = 0.18f;

    /// <summary>Tujuan koin terbang (dipakai CoinFlyEffect).</summary>
    public RectTransform IconTarget => iconTarget != null ? iconTarget : (RectTransform)transform;

    float displayed;
    int target;
    int lastShown = int.MinValue;
    bool flyMode;
    float popT = float.MaxValue;
    bool popActive;

    RectTransform[] pops;
    Vector3[] popBase;

    void Start()
    {
        if (coins == null) coins = FindAnyObjectByType<CoinSystem>();
        if (coins == null)
        {
            Debug.LogWarning($"{name}: CoinSystem tidak ditemukan. Tambahkan objek CoinManager di scene.");
            enabled = false;
            return;
        }
        if (iconTarget == null)
            Debug.LogWarning($"{name}: Icon Target belum diisi. Koin terbang akan menuju objek ini sendiri.");

        // elemen yang membal: ikon dan angka (bukan induknya)
        var list = new List<RectTransform>();
        if (iconTarget != null) list.Add(iconTarget);
        if (countText != null && countText.rectTransform != iconTarget) list.Add(countText.rectTransform);
        pops = list.ToArray();
        popBase = new Vector3[pops.Length];
        for (int i = 0; i < pops.Length; i++) popBase[i] = pops[i].localScale;

        target = coins.Coins;
        displayed = target;
        UpdateText();

        coins.OnCoinsChanged += OnCoinsChanged;
    }

    void OnDestroy()
    {
        if (coins != null) coins.OnCoinsChanged -= OnCoinsChanged;
    }

    /// <summary>Dipanggil CoinFlyEffect: ada koin terbang, jadi angka menunggu koin tiba (lewat Receive).</summary>
    public void SetFlyMode(bool on)
    {
        flyMode = on;
    }

    /// <summary>Satu koin terbang tiba di ikon: tambah angka tujuan dan membal.</summary>
    public void Receive(int amount)
    {
        if (amount <= 0) return;
        target += amount;
        StartPop();
    }

    void OnCoinsChanged(int total)
    {
        if (total < target)
        {
            // berkurang (belanja): langsung turun
            target = total;
            if (displayed > target) displayed = target;
            UpdateText();
            return;
        }

        // bertambah tanpa koin terbang: angka naik sendiri. Dengan koin terbang, angka menunggu Receive().
        if (!flyMode)
        {
            target = total;
            StartPop();
        }
    }

    void StartPop()
    {
        popT = 0f;
        popActive = true;
    }

    void Update()
    {
        if (displayed < target)
        {
            float diff = target - displayed;
            float speed = Mathf.Max(minCountSpeed, diff / Mathf.Max(0.05f, catchUpTime));
            displayed = Mathf.MoveTowards(displayed, target, speed * Time.unscaledDeltaTime);
            UpdateText();
        }

        if (popActive)
        {
            popT += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(popT / Mathf.Max(0.01f, popTime));
            float s = 1f + (popScale - 1f) * k * k;   // langsung membesar, lalu mengecil halus
            for (int i = 0; i < pops.Length; i++)
                if (pops[i] != null) pops[i].localScale = popBase[i] * s;

            if (k <= 0f)
            {
                popActive = false;
                for (int i = 0; i < pops.Length; i++)
                    if (pops[i] != null) pops[i].localScale = popBase[i];
            }
        }
    }

    void UpdateText()
    {
        if (countText == null) return;
        int shown = Mathf.RoundToInt(displayed);
        if (shown == lastShown) return;
        lastShown = shown;
        countText.text = string.Format(format, shown);
    }
}
