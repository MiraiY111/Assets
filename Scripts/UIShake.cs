using UnityEngine;

/// <summary>
/// Getar kecil untuk objek UI (RectTransform). Pasang di objek bar, panggil Play() saat kena damage.
/// Posisi digeser dalam kelipatan satu satuan Canvas (cocok untuk pixel art) lalu dikembalikan persis.
/// Jalan walau Time.timeScale = 0. Jangan dipakai di objek yang posisinya juga digerakkan script lain.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIShake : MonoBehaviour
{
    [Tooltip("Lama getar (detik).")]
    [SerializeField] private float duration = 0.2f;
    [Tooltip("Jarak getar maksimum (satuan Canvas). Mengecil sampai 0 selama durasi.")]
    [SerializeField] private float magnitude = 3f;
    [Tooltip("Jeda antar loncatan posisi (detik). Makin kecil makin cepat bergetar.")]
    [SerializeField] private float interval = 0.03f;

    RectTransform rt;
    Vector2 basePos;
    Vector2 offset;
    float timeLeft;
    float nextJitter;
    bool shaking;

    void Awake() { rt = (RectTransform)transform; }

    /// <summary>Mulai getar. Dipanggil lagi saat masih bergetar = durasi diulang dari awal.</summary>
    public void Play()
    {
        if (!isActiveAndEnabled) return;
        if (!shaking) basePos = rt.anchoredPosition;
        shaking = true;
        timeLeft = duration;
        nextJitter = 0f;
    }

    void LateUpdate()
    {
        if (!shaking) return;

        timeLeft -= Time.unscaledDeltaTime;
        if (timeLeft <= 0f)
        {
            Stop();
            return;
        }

        nextJitter -= Time.unscaledDeltaTime;
        if (nextJitter <= 0f)
        {
            nextJitter = Mathf.Max(0.005f, interval);
            float m = magnitude * (timeLeft / Mathf.Max(0.01f, duration));
            offset = new Vector2(Mathf.Round(Random.Range(-m, m)), Mathf.Round(Random.Range(-m, m)));
        }

        rt.anchoredPosition = basePos + offset;
    }

    void OnDisable()
    {
        if (shaking) Stop();
    }

    void Stop()
    {
        shaking = false;
        offset = Vector2.zero;
        if (rt != null) rt.anchoredPosition = basePos;
    }
}