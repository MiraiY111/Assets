using UnityEngine;
using TMPro;
using HeyHeyThere.PixelBarsFree;

/// <summary>
/// Pasang di objek yang sama dengan PixelStatBar (BossBar).
/// Tersembunyi sampai Bind() dipanggil. Setelah Bind, bar tampil KOSONG dan menunggu FillUp()
/// (dipanggil WaveSpawner setelah boss selesai berjalan masuk), lalu terisi penuh sampai HP boss sekarang.
/// Max Value bar selalu mengikuti Max HP boss, jadi segmen dan isi bar ikut menyesuaikan.
/// Teks angka HP (opsional) ikut berjalan dari 0 sampai penuh saat bar terisi, lalu mengikuti HP boss.
/// Bar bergetar sebentar (UIShake, opsional) setiap boss kena damage.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private PixelStatBar bar;
    [Tooltip("Lama bar terisi dari kosong sampai penuh (detik).")]
    [SerializeField] private float fillDuration = 1f;
    [Tooltip("Jeda setelah boss mati sebelum bar disembunyikan, supaya jejak putihnya sempat turun.")]
    [SerializeField] private float hideDelay = 1f;

    [Header("Teks HP (opsional)")]
    [Tooltip("Sebaiknya anak dari objek BossBar ini, supaya ikut tampil, hilang, dan bergetar bersama bar.")]
    [SerializeField] private TMP_Text hpText;
    [Tooltip("{0} = HP sekarang, {1} = HP maksimum. Contoh: \"{0}\" atau \"{0}/{1}\"")]
    [SerializeField] private string format = "{0}";

    [Header("Getar saat kena damage (opsional)")]
    [Tooltip("Kosong = diambil dari objek ini kalau ada komponen UIShake.")]
    [SerializeField] private UIShake shake;

    EnemyHealth boss;
    bool waitingFill;   // bar kosong, menunggu FillUp()
    bool filling;       // bar sedang terisi
    int lastHP;

    void Reset() { bar = GetComponent<PixelStatBar>(); }

    void Awake()
    {
        if (bar == null) bar = GetComponent<PixelStatBar>();
        if (shake == null) shake = GetComponent<UIShake>();
        if (boss == null) gameObject.SetActive(false);   // sembunyi sampai ada boss
    }

    /// <summary>Tampilkan bar dalam keadaan kosong untuk boss ini. Isinya menunggu FillUp().</summary>
    public void Bind(EnemyHealth newBoss)
    {
        if (bar == null) bar = GetComponent<PixelStatBar>();
        if (newBoss == null || bar == null)
        {
            Debug.LogWarning($"{name}: BossHealthBarUI tidak bisa dihubungkan (boss atau PixelStatBar kosong).");
            return;
        }

        Unbind();
        CancelInvoke();
        boss = newBoss;
        lastHP = boss.CurrentHP;
        boss.OnHealthChanged += Refresh;
        boss.OnDied += HandleDied;

        gameObject.SetActive(true);

        // PixelStatBar membuat bagian-bagiannya (bingkai, isi) sebagai anak saat diaktifkan,
        // jadi teks harus dipindah ke urutan paling akhir supaya tampil di depan semuanya.
        if (hpText != null)
        {
            hpText.raycastTarget = false;
            if (hpText.transform.parent == transform) hpText.transform.SetAsLastSibling();
        }

        bar.maxValue = boss.MaxHP;
        bar.value = 0f;
        bar.Settle();           // kosong, tanpa efek kedip
        UpdateText(0f);
        waitingFill = true;
        filling = false;
    }

    /// <summary>Mulai mengisi bar dari kosong sampai HP boss sekarang.</summary>
    public void FillUp()
    {
        if (boss == null || !waitingFill) return;
        waitingFill = false;
        filling = true;
    }

    void Unbind()
    {
        if (boss == null) return;
        boss.OnHealthChanged -= Refresh;
        boss.OnDied -= HandleDied;
        boss = null;
    }

    void OnDestroy() { Unbind(); }

    void Update()
    {
        if (!filling || boss == null) return;

        float speed = bar.maxValue / Mathf.Max(0.05f, fillDuration);
        bar.value = Mathf.MoveTowards(bar.value, boss.CurrentHP, speed * Time.unscaledDeltaTime);
        UpdateText(bar.value);
        if (Mathf.Approximately(bar.value, boss.CurrentHP)) filling = false;
    }

    void Refresh(int current, int max)
    {
        bool damaged = current < lastHP;
        lastHP = current;

        bar.maxValue = max;
        // selama kosong atau sedang terisi, Update() yang mengatur nilainya
        if (!waitingFill && !filling)
        {
            bar.value = current;
            UpdateText(current);
        }

        if (damaged && shake != null) shake.Play();
    }

    void HandleDied(EnemyHealth e)
    {
        waitingFill = filling = false;
        Invoke(nameof(Hide), hideDelay);
    }

    void Hide() { gameObject.SetActive(false); }

    void UpdateText(float shown)
    {
        if (hpText == null || boss == null) return;
        hpText.text = string.Format(format, Mathf.CeilToInt(shown), boss.MaxHP);
    }
}