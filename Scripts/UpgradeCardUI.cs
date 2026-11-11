using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Kartu level-up. Saat Level mencapai Required Level (1), game BERHENTI (Time.timeScale = 0) dan satu kartu
/// "DASH" muncul di tengah layar. Pemain wajib mengkliknya; sebelum itu tidak ada hal lain yang bisa diklik
/// (pause, tombol serang, dll tertutup peredup layar). Setelah dipilih Dash terbuka dan game lanjut.
///
/// Seluruh tampilan (peredup, teks LEVEL UP, kartu, petunjuk) dibuat lewat kode di Canvas sendiri, jadi cukup
/// pasang skrip ini di objek kosong "UpgradeManager" di tiap stage dan isi sprite + font di Inspector.
///
/// Bila Level sudah >= Required Level tetapi Dash belum terbuka (mis. level naik tepat saat stage selesai),
/// kartu muncul di awal stage berikutnya.
/// Semua animasi memakai waktu unscaled karena game sedang berhenti.
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("Sprite kartu (card_normal, card_hover, card_pressed)")]
    [SerializeField] private Sprite cardNormal;
    [SerializeField] private Sprite cardHover;
    [SerializeField] private Sprite cardPressed;
    [Tooltip("Logo Dash di tengah atas kartu (card_icon_dash, atau ikon Dash milikmu sendiri).")]
    [SerializeField] private Sprite iconSprite;

    [Header("Teks")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private string header = "LEVEL UP!";
    [SerializeField] private string title = "DASH";
    [TextArea(2, 4)]
    [SerializeField] private string description = "Burst forward in the direction you move. Cancels your attack. Cooldown: {cd}s";
    [SerializeField] private string hint = "TAP THE CARD TO CHOOSE";

    [Header("Kapan muncul")]
    [Tooltip("Kartu muncul saat Level mencapai angka ini.")]
    [SerializeField] private int requiredLevel = 1;
    [Tooltip("Jeda (detik, waktu nyata) setelah naik level sebelum game berhenti, supaya efek naik level di bar EXP sempat terlihat.")]
    [SerializeField] private float showDelay = 0.6f;

    [Header("Tampilan")]
    [Tooltip("Lebar kartu di Canvas (referensi 1920x1080). Pakai kelipatan 80 (240, 320, 400) supaya piksel rata. Tinggi mengikuti.")]
    [SerializeField] private float cardWidth = 320f;
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0.04f, 0.78f);
    [SerializeField] private Color headerColor = new Color(0.45f, 0.82f, 1f, 1f);
    [SerializeField] private int canvasSortingOrder = 500;

    [Header("Terkait (kosong = dicari otomatis)")]
    [SerializeField] private ExpSystem exp;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerHealth health;

    const float CardW = 80f, CardH = 112f;   // ukuran asli sprite kartu (piksel)

    bool pending;
    bool showing;
    bool acceptInput;
    bool picked;
    bool pressedNow;
    float prevTimeScale = 1f;

    GameObject canvasGO;
    Image dimImage;
    CanvasGroup contentGroup;
    RectTransform cardRT;
    Image cardImg;
    TMP_Text hintText;

    void Start()
    {
        if (exp == null) exp = FindAnyObjectByType<ExpSystem>();
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (health == null) health = FindAnyObjectByType<PlayerHealth>();

        if (exp == null)
        {
            Debug.LogWarning($"{name}: ExpSystem tidak ditemukan. Kartu level-up dimatikan.");
            enabled = false;
            return;
        }
        if (cardNormal == null)
            Debug.LogWarning($"{name}: Card Normal belum diisi. Kartu akan tampil sebagai kotak polos.");

        exp.OnLevelUp += HandleLevelUp;

        // level sudah cukup tetapi Dash belum terbuka: tampilkan sekarang
        if (exp.Level >= requiredLevel && !PlayerUpgrades.DashUnlocked)
            StartCoroutine(ShowAfter(0.3f));
    }

    void OnDestroy()
    {
        if (exp != null) exp.OnLevelUp -= HandleLevelUp;
        if (showing) ReleaseGame();
        if (canvasGO != null) Destroy(canvasGO);
    }

    void HandleLevelUp(int newLevel)
    {
        if (newLevel < requiredLevel) return;
        if (PlayerUpgrades.DashUnlocked || showing || pending) return;
        StartCoroutine(ShowAfter(showDelay));
    }

    IEnumerator ShowAfter(float delay)
    {
        pending = true;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // pemain membuka pause sendiri di sela jeda: tunggu sampai pause ditutup
        while (GamePause.IsPaused) yield return null;

        // pemain sudah mati: tidak perlu kartu (Restart mengembalikan level)
        if (health != null && health.IsDead) { pending = false; yield break; }

        pending = false;
        yield return ShowRoutine();
    }

    // ---------- alur kartu ----------
    IEnumerator ShowRoutine()
    {
        showing = true;
        acceptInput = false;
        picked = false;
        pressedNow = false;

        prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        GamePause.IsPaused = true;

        BuildUI();
        Vector2 basePos = cardRT.anchoredPosition;

        // masuk: peredup memudar, kartu naik dari bawah sambil membesar dengan sedikit membal
        const float intro = 0.45f;
        float t = 0f;
        while (t < intro)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / intro);
            dimImage.color = WithAlpha(dimColor, dimColor.a * k);
            contentGroup.alpha = Mathf.Clamp01(k * 2f);
            float s = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(k));
            cardRT.localScale = new Vector3(s, s, 1f);
            cardRT.anchoredPosition = basePos + new Vector2(0f, Mathf.Lerp(-90f, 0f, 1f - (1f - k) * (1f - k)));
            yield return null;
        }
        cardRT.localScale = Vector3.one;
        cardRT.anchoredPosition = basePos;
        contentGroup.alpha = 1f;
        acceptInput = true;

        // menunggu pemain memilih
        while (!picked)
        {
            // jaga game tetap berhenti (mis. aplikasi dibuka lagi dari background dan memicu pause)
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            GamePause.IsPaused = true;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Pick();

            float now = Time.unscaledTime;
            cardRT.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(now * 2.2f) * 6f);

            if (hintText != null)
            {
                Color c = hintText.color;
                c.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(now * 4f));
                hintText.color = c;
            }
            yield return null;
        }

        // keluar: kartu membesar dan memudar, peredup hilang
        PlayerUpgrades.UnlockDash();
        SetCardSprite(cardPressed != null ? cardPressed : cardHover);
        const float outro = 0.35f;
        t = 0f;
        while (t < outro)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / outro);
            float s = Mathf.Lerp(1f, 1.2f, 1f - (1f - k) * (1f - k));
            cardRT.localScale = new Vector3(s, s, 1f);
            contentGroup.alpha = 1f - k * k;
            dimImage.color = WithAlpha(dimColor, dimColor.a * (1f - k));
            yield return null;
        }

        if (canvasGO != null) Destroy(canvasGO);
        canvasGO = null;
        ReleaseGame();
        showing = false;
    }

    void ReleaseGame()
    {
        Time.timeScale = prevTimeScale;
        GamePause.IsPaused = false;
    }

    void Pick()
    {
        if (!acceptInput || picked) return;
        picked = true;
        acceptInput = false;
    }

    // ---------- membangun tampilan ----------
    void BuildUI()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        canvasGO = new GameObject("UpgradeCardCanvas", typeof(RectTransform), typeof(Canvas),
                                  typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // peredup layar penuh: menahan semua klik ke HUD di belakangnya
        dimImage = NewImage("Dim", canvasGO.transform, null, WithAlpha(dimColor, 0f));
        Stretch(dimImage.rectTransform);
        dimImage.raycastTarget = true;

        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(canvasGO.transform, false);
        Stretch((RectTransform)content.transform);
        contentGroup = content.GetComponent<CanvasGroup>();
        contentGroup.alpha = 0f;

        float s = cardWidth / CardW;
        float cardH = CardH * s;

        // teks LEVEL UP! di atas kartu
        TMP_Text headerText = NewText("Header", content.transform, header, headerColor, 64f, 36f, 80f);
        PlaceCentered(headerText.rectTransform, new Vector2(0f, cardH * 0.5f + 80f), new Vector2(1200f, 110f));

        // kartu
        cardImg = NewImage("Card", content.transform, cardNormal, Color.white);
        cardImg.raycastTarget = true;
        cardImg.preserveAspect = false;
        cardRT = cardImg.rectTransform;
        PlaceCentered(cardRT, new Vector2(0f, 10f), new Vector2(cardWidth, cardH));

        // logo (tengah atas), judul (plat), deskripsi (panel): koordinat dalam piksel sprite kartu
        Image icon = NewImage("Icon", cardRT, iconSprite, Color.white);
        icon.preserveAspect = true;
        PlaceInCard(icon.rectTransform, s, 40f, 28f, 22f, 22f);

        TMP_Text titleText = NewText("Title", cardRT, title, Color.white, 40f, 14f, 44f);
        PlaceInCard(titleText.rectTransform, s, 40f, 56f, 54f, 10f);

        string cd = player != null ? player.DashCooldown.ToString("0.0#", CultureInfo.InvariantCulture) : "0.8";
        TMP_Text descText = NewText("Description", cardRT, description.Replace("{cd}", cd),
                                    new Color(0.86f, 0.91f, 1f, 1f), 24f, 11f, 28f);
        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.alignment = TextAlignmentOptions.Center;
        PlaceInCard(descText.rectTransform, s, 40f, 86f, 58f, 34f);

        // petunjuk di bawah kartu
        hintText = NewText("Hint", content.transform, hint, new Color(1f, 1f, 1f, 1f), 30f, 20f, 36f);
        PlaceCentered(hintText.rectTransform, new Vector2(0f, -cardH * 0.5f - 60f), new Vector2(1200f, 60f));

        // interaksi kartu: hover / tekan / klik
        var trig = cardImg.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trig, EventTriggerType.PointerEnter, _ => { if (acceptInput && !pressedNow) SetCardSprite(cardHover); });
        AddTrigger(trig, EventTriggerType.PointerExit, _ => { pressedNow = false; if (acceptInput) SetCardSprite(cardNormal); });
        AddTrigger(trig, EventTriggerType.PointerDown, _ => { if (acceptInput) { pressedNow = true; SetCardSprite(cardPressed); } });
        AddTrigger(trig, EventTriggerType.PointerUp, _ => { pressedNow = false; if (acceptInput) SetCardSprite(cardHover); });
        AddTrigger(trig, EventTriggerType.PointerClick, _ => Pick());
    }

    void SetCardSprite(Sprite sp)
    {
        if (cardImg != null && sp != null) cardImg.sprite = sp;
    }

    // ---------- pembantu UI ----------
    static void AddTrigger(EventTrigger t, EventTriggerType type, UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        t.triggers.Add(entry);
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text NewText(string name, Transform parent, string text, Color color, float size, float minSize, float maxSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.color = color;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = minSize;
        t.fontSizeMax = maxSize;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void PlaceCentered(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // koordinat dari pojok kiri-atas sprite kartu (piksel sprite), pusat elemen di (cx, cy)
    static void PlaceInCard(RectTransform rt, float s, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx * s, -cy * s);
        rt.sizeDelta = new Vector2(w * s, h * s);
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
