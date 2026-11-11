using UnityEngine;

/// <summary>
/// Pasang di objek tombol Dash (HUD). Tombol tidak terlihat dan tidak bisa disentuh sampai kartu Dash dipilih,
/// lalu muncul dengan memudar masuk dan membal sebentar.
/// Memakai CanvasGroup (ditambahkan otomatis), jadi tidak mengganggu Image/DashButton maupun posisi dari UI Customize.
/// Animasi memakai waktu unscaled.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DashButtonGate : MonoBehaviour
{
    [SerializeField] private float fadeTime = 0.5f;
    [Tooltip("Seberapa besar tombol membal saat muncul (1 = tanpa membal).")]
    [SerializeField] private float popScale = 1.25f;

    CanvasGroup group;
    bool shown;
    Coroutine routine;

    void Awake() { group = GetComponent<CanvasGroup>(); }

    void Start()
    {
        // di Start (bukan Awake): ExpSystem sudah memulai stage dan memuat status Dash yang benar
        shown = PlayerUpgrades.DashUnlocked;
        Apply(shown ? 1f : 0f);
    }

    void OnDisable()
    {
        routine = null;
        if (group != null) Apply(PlayerUpgrades.DashUnlocked ? 1f : 0f);
    }

    void Update()
    {
        bool want = PlayerUpgrades.DashUnlocked;
        if (want == shown) return;
        shown = want;

        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (want) routine = StartCoroutine(AppearRoutine());
        else Apply(0f);
    }

    System.Collections.IEnumerator AppearRoutine()
    {
        Vector3 baseScale = transform.localScale;   // diambil dulu, supaya skala dari UI Customize tidak tertimpa
        Apply(0f);
        group.interactable = true;
        group.blocksRaycasts = true;

        float t = 0f;
        float dur = Mathf.Max(0.05f, fadeTime);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            group.alpha = k;
            float pop = 1f + (popScale - 1f) * Mathf.Sin(k * Mathf.PI);   // membesar lalu kembali
            transform.localScale = baseScale * pop;
            yield return null;
        }

        transform.localScale = baseScale;
        Apply(1f);
        routine = null;
    }

    void Apply(float alpha)
    {
        group.alpha = alpha;
        bool on = alpha >= 1f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}
