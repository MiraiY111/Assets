using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeyHeyThere.PixelBarsFree;

/// <summary>
/// HUD bar EXP memakai PixelStatBar (Thin, fill stamina).
/// Dapat EXP : bar bergetar tipis, area EXP baru muncul pucat (trail "ghost"), lalu isi bar menyusul dengan halus.
/// Level up  : bar terisi penuh, lalu kilat putih + getar + teks Level membal, bar kosong lagi dan lanjut ke sisa EXP.
/// Pasang di objek yang sama dengan PixelStatBar. Butuh patch 'ghost' di PixelStatBar.
/// Semua animasi memakai waktu unscaled.
/// </summary>
public class ExpBarUI : MonoBehaviour
{
    [SerializeField] private ExpSystem exp;       // kosong = dicari otomatis
    [SerializeField] private PixelStatBar bar;    // kosong = diambil dari objek ini
    [Tooltip("Opsional. Kosong = diambil dari objek ini kalau ada.")]
    [SerializeField] private UIShake shake;

    [Header("Teks level")]
    [Tooltip("Sebaiknya anak dari objek bar ini, supaya ikut bergetar dan ikut bergeser di UI Customize.")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string levelFormat = "Level {0}";
    [SerializeField] private float textPunchScale = 1.35f;
    [SerializeField] private float textPunchTime = 0.3f;

    [Header("Isi bar")]
    [Tooltip("Jeda area pucat (ghost) tampil sebelum isi bar menyusul (detik).")]
    [SerializeField] private float ghostHold = 0.25f;
    [Tooltip("Kecepatan isi bar menyusul, dalam bagian bar penuh per detik. 1 = penuh dalam 1 detik.")]
    [SerializeField] private float fillSpeed = 1.2f;

    [Header("Level up")]
    [Tooltip("Jeda saat bar penuh dan kilat tampil, sebelum bar dikosongkan (detik).")]
    [SerializeField] private float levelUpPause = 0.35f;
    [SerializeField] private float flashTime = 0.4f;
    [Range(0f, 1f)][SerializeField] private float flashAlpha = 0.85f;
    [Tooltip("Opsional. Kosong = dibuat otomatis (kotak putih menutupi bar).")]
    [SerializeField] private Image flashImage;

    struct Gain { public int oldLevel, oldExp, newLevel, newExp; }
    readonly Queue<Gain> queue = new Queue<Gain>();
    bool running;
    int shownLevel;
    Vector3 textBaseScale = Vector3.one;   // skala awal teks Level, dipakai sebagai acuan membal
    Coroutine punchRoutine;

    void Reset() { bar = GetComponent<PixelStatBar>(); }

    void Start()
    {
        if (exp == null) exp = FindAnyObjectByType<ExpSystem>();
        if (bar == null) bar = GetComponent<PixelStatBar>();
        if (shake == null) shake = GetComponent<UIShake>();

        if (exp == null || bar == null)
        {
            Debug.LogWarning($"{name}: ExpBarUI belum lengkap (ExpSystem / PixelStatBar).");
            enabled = false;
            return;
        }

        if (levelText != null)
        {
            levelText.raycastTarget = false;
            textBaseScale = levelText.transform.localScale;
            if (levelText.transform.parent == bar.transform) levelText.transform.SetAsLastSibling();
        }

        EnsureFlashImage();

        shownLevel = exp.Level;
        bar.ghost = -1f;
        bar.maxValue = exp.ExpToNext(shownLevel);
        bar.value = exp.CurrentExp;
        bar.Settle();
        UpdateLevelText();

        exp.OnExpGained += OnGained;
    }

    void OnDestroy()
    {
        if (exp != null) exp.OnExpGained -= OnGained;
    }

    void OnGained(int oldLevel, int oldExp, int newLevel, int newExp)
    {
        queue.Enqueue(new Gain { oldLevel = oldLevel, oldExp = oldExp, newLevel = newLevel, newExp = newExp });
        if (!running) StartCoroutine(Runner());
    }

    IEnumerator Runner()
    {
        running = true;
        while (queue.Count > 0)
        {
            Gain g = queue.Dequeue();
            if (shake != null) shake.Play();

            int lv = g.oldLevel;
            for (; lv < g.newLevel; lv++)
            {
                int need = exp.ExpToNext(lv);
                bar.maxValue = need;
                bar.ghost = need;
                yield return Wait(ghostHold);
                yield return FillTo(need);
                bar.ghost = -1f;

                LevelUpFx(lv + 1);
                yield return Wait(levelUpPause);

                bar.maxValue = exp.ExpToNext(lv + 1);
                bar.value = 0f;
                bar.Settle();
            }

            bar.maxValue = exp.ExpToNext(lv);
            bar.ghost = g.newExp;
            yield return Wait(ghostHold);
            yield return FillTo(g.newExp);
            bar.ghost = -1f;
        }
        running = false;
    }

    IEnumerator FillTo(float target)
    {
        while (!Mathf.Approximately(bar.value, target))
        {
            float step = bar.maxValue * Mathf.Max(0.05f, fillSpeed) * Time.unscaledDeltaTime;
            bar.value = Mathf.MoveTowards(bar.value, target, step);
            yield return null;
        }
    }

    static IEnumerator Wait(float t)
    {
        if (t > 0f) yield return new WaitForSecondsRealtime(t);
    }

    void LevelUpFx(int newLevel)
    {
        shownLevel = newLevel;
        UpdateLevelText();
        if (shake != null) shake.Play();
        StartCoroutine(FlashRoutine());
        if (levelText != null)
        {
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(PunchText());
        }
    }

    void UpdateLevelText()
    {
        if (levelText != null) levelText.text = string.Format(levelFormat, shownLevel);
    }

    IEnumerator FlashRoutine()
    {
        if (flashImage == null) yield break;

        float t = 0f;
        while (t < flashTime)
        {
            t += Time.unscaledDeltaTime;
            float a = flashAlpha * (1f - Mathf.Clamp01(t / Mathf.Max(0.01f, flashTime)));
            flashImage.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        flashImage.color = new Color(1f, 1f, 1f, 0f);
    }

    IEnumerator PunchText()
    {
        Transform tt = levelText.transform;
        float t = 0f;
        while (t < textPunchTime)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, textPunchTime));
            tt.localScale = textBaseScale * (1f + (textPunchScale - 1f) * k);
            yield return null;
        }
        tt.localScale = textBaseScale;
        punchRoutine = null;
    }

    void EnsureFlashImage()
    {
        if (flashImage == null)
        {
            var go = new GameObject("LevelUpFlash", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(bar.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            flashImage = go.GetComponent<Image>();
        }

        flashImage.raycastTarget = false;
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.transform.SetAsLastSibling();
        if (levelText != null && levelText.transform.parent == bar.transform) levelText.transform.SetAsLastSibling();
    }
}