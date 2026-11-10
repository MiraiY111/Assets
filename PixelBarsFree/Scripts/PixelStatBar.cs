using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PixelBarsFree
{
    /// <summary>
    /// A status bar dressed from the kit: set shape, style and fill, then value of maxValue. It does what
    /// a game's bar does: the fill runs over the track only, so a boss bar's plate never eats its low
    /// values; when the value drops the fill flashes and a pale trail holds where it was, then drains to
    /// it; at or below low the fill pulses; segments marks the track in equal parts; an orb's liquid
    /// shows its surface.
    /// <code>
    /// var hp = new GameObject("HP", typeof(RectTransform)).AddComponent&lt;PixelStatBar&gt;();
    /// hp.transform.SetParent(canvas.transform, false);
    /// ((RectTransform)hp.transform).sizeDelta = new Vector2(96, 12);
    /// hp.fill = "health";
    /// hp.value -= 25;  // flashes, and the trail drains
    /// </code>
    /// Its frame, trail and fill are hidden children it makes itself, in the editor too, and doesn't save.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public class PixelStatBar : MonoBehaviour
    {
        public enum Shape { Bar, Thin, Vertical, Boss, Orb, Ring }

        /// <summary>The kit's folder under Resources/.</summary>
        public const string Kit = "Pixel Bars Free/";
        static readonly string[] Files = { "bar", "thin", "vbar", "boss", "orb", "ring" };
        /// <summary>Where the fill runs inside the frame, left, top, right, bottom; the orb's is its liquid's height.</summary>
        static readonly int[][] Track = { new[] { 3, 3, 3, 3 }, new[] { 1, 1, 1, 1 }, new[] { 3, 3, 3, 3 }, new[] { 16, 5, 3, 5 }, new[] { 0, 4, 0, 4 }, null };
        const float Liquid = 11.9f, FlashTime = 0.25f, Orb = 32f;

        [Tooltip("Bar, Thin, Vertical and Boss stretch (12, 7, 12 and 16 high); Orb (32x32) and Ring (20x20) scale by whole numbers")]
        public Shape shape;
        [Tooltip("A folder of Frames/: wood, stone, sky, forest, royal, ember, gilded or cyan")]
        public string style = "wood";
        [Tooltip("A folder of Fills/: health, mana, stamina, xp, shield, rage, magic or poison")]
        public string fill = "health";
        public float maxValue = 100f;
        public float value = 100f;
        [Tooltip("Marks the track in this many equal parts (0 or 1: none). Not on orbs and rings."), Range(0, 64)]
        public int segments;
        [Header("Feel")]
        [Tooltip("On a drop, a pale trail holds where the value was, then drains to it")]
        public bool trail = true;
        [Tooltip("Seconds the trail holds before it drains")]
        public float trailHold = 0.4f;
        [Tooltip("The share of the bar the trail drains per second")]
        public float trailSpeed = 0.8f;
        public Color trailColor = Color.white;
        [Tooltip("On a drop, the fill flashes white")]
        public bool flash = true;
        [Tooltip("The fill pulses at or below this share of the bar (0: never)"), Range(0, 1)]
        public float low;
        [Tooltip("[EXP] Nilai tujuan yang digambar pucat di depan isi bar (dipakai ExpBarUI). -1 = mati.")]
        public float ghost = -1f;

        static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();
        readonly List<GameObject> parts = new List<GameObject>();
        readonly List<Image> marks = new List<Image>();
        RectTransform clipTrail, clipFill;
        Image frame, trailImage, fillImage, flashImage, surface, glint;
        string dressed, marked;
        float last, trailValue, hold, flashLeft, clock;

        public float Ratio => maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;
        /// <summary>Where the trail is, as a share of the bar.</summary>
        public float TrailRatio => trail && maxValue > 0f ? Mathf.Clamp01(trailValue / maxValue) : 0f;
        /// <summary>The fill's reach in art pixels from the bar's start (an orb's in its 32), or a ring's share.</summary>
        public float Shown { get; private set; }
        public bool Flashing => flashLeft > 0f;

        /// <summary>A sprite of the kit by its path under Resources/<see cref="Kit"/>, or null if this kit has none.</summary>
        public static Sprite Load(string path)
        {
            if (!loaded.TryGetValue(path, out var s) || s == null)
                loaded[path] = s = Names.Load<Sprite>(Kit + path.Insert(path.LastIndexOf('/') + 1, "T_"));
            return s;
        }

        void OnEnable()
        {
            if (frame == null)
                Build();
            dressed = null;
            Settle();
        }

        /// <summary>Hides the parts: destroying them here fails while the bar's object is being deactivated.</summary>
        void OnDisable()
        {
            foreach (var p in parts)
                if (p != null)
                    foreach (var image in p.GetComponentsInChildren<Image>(true))
                        image.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var p in parts)
                Drop(p);
            parts.Clear();
        }

        /// <summary>Destroys a part, in the editor once the current call is over, as an object being
        /// activated or deactivated can't lose its children.</summary>
        internal static void Drop(GameObject part)
        {
            if (part == null)
                return;
            if (Application.isPlaying)
                Destroy(part);
#if UNITY_EDITOR
            else
                UnityEditor.EditorApplication.delayCall += () => { if (part != null) DestroyImmediate(part); };
#endif
        }

        void Update() => Tick(Application.isPlaying ? Time.unscaledDeltaTime : 0f);

        /// <summary>Puts the trail where the value is and stops a flash: for a bar shown afresh.</summary>
        public void Settle()
        {
            last = trailValue = value;
            hold = flashLeft = 0f;
            Tick(0f);
        }

        /// <summary>Advances the bar by dt seconds; Update calls it.</summary>
        public void Tick(float dt)
        {
            if (frame == null)
                Build();
            if (dressed != $"{shape}/{style}/{fill}")
                Dress();
            value = Mathf.Clamp(value, 0f, maxValue);
            clock += dt;
            if (!Application.isPlaying)
                last = trailValue = value;
            if (value < last)
            {
                trailValue = Mathf.Max(trailValue, last);
                hold = trailHold;
                if (flash)
                    flashLeft = 1f;
            }
            last = value;
            if (hold > 0f)
                hold -= dt;
            else
                trailValue = Mathf.Max(value, trailValue - trailSpeed * maxValue * dt);
            trailValue = Mathf.Max(trailValue, value);
            flashLeft = Mathf.Max(0f, flashLeft - dt / FlashTime);
            float pulse = low > 0f && Ratio <= low && Ratio > 0f ? 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 3f) : 0f;

            // [EXP] area pucat di depan isi bar: EXP yang baru didapat, sebelum isi bar menyusul
            float trailShare = TrailRatio;
            if (ghost > value && maxValue > 0f)
                trailShare = Mathf.Max(trailShare, Mathf.Clamp01(ghost / maxValue));
            Lay(Ratio, trailShare);

            flashImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.7f * flashLeft + 0.3f * pulse));
            trailImage.color = trailColor;
        }

        RectTransform Part(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            if (parent == transform)
                parts.Add(go);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        Image Picture(string name, Transform parent)
        {
            var image = Part(name, parent).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void Build()
        {
            OnDestroy();
            foreach (Transform child in transform)  // an earlier one's, left by a script reload
                if (child.gameObject.hideFlags == HideFlags.HideAndDontSave)
                    Drop(child.gameObject);
            marks.Clear();
            frame = Picture("Frame", transform);
            Full(frame.rectTransform);
            clipTrail = Part("Trail", transform);
            clipTrail.gameObject.AddComponent<RectMask2D>();
            trailImage = Picture("Trail", clipTrail);
            clipFill = Part("Fill", transform);
            clipFill.gameObject.AddComponent<RectMask2D>();
            fillImage = Picture("Fill", clipFill);
            flashImage = Picture("Flash", clipFill);
            surface = Picture("Surface", transform);
            surface.rectTransform.anchorMin = surface.rectTransform.anchorMax = surface.rectTransform.pivot = Vector2.up;
            glint = Picture("Glint", transform);
            Full(glint.rectTransform);
        }

        static void Full(RectTransform rt)
        {
            rt.anchorMin = rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.anchorMax = Vector2.one;
        }

        void Dress()
        {
            var file = Files[(int)shape];
            bool stretch = shape <= Shape.Boss, round = !stretch;
            frame.sprite = Load($"Frames/{style}/{file}");
            trailImage.sprite = Load($"Fills/trail/{file}");
            fillImage.sprite = Load($"Fills/{fill}/{file}");
            flashImage.sprite = Load($"Fills/flash/{file}");
            glint.sprite = shape == Shape.Orb ? Load("Fills/glint") : null;
            frame.type = stretch ? Image.Type.Sliced : Image.Type.Simple;
            foreach (var image in new[] { trailImage, fillImage, flashImage })
            {
                image.type = stretch ? Image.Type.Sliced : Image.Type.Filled;
                if (round)
                {
                    image.fillMethod = shape == Shape.Orb ? Image.FillMethod.Vertical : Image.FillMethod.Radial360;
                    image.fillOrigin = shape == Shape.Orb ? (int)Image.OriginVertical.Bottom : (int)Image.Origin360.Top;
                    image.fillClockwise = true;
                }
            }
            foreach (var image in new[] { frame, trailImage, fillImage, flashImage, glint })
                image.enabled = image.sprite != null;
            surface.enabled = false;
            dressed = $"{shape}/{style}/{fill}";
            marked = null;
        }

        /// <summary>The fill's reach in art pixels along `whole`, over the track only, in whole pixels.</summary>
        float OnTrack(float share, float whole)
        {
            var t = Track[(int)shape];
            bool vertical = shape == Shape.Vertical || shape == Shape.Orb;
            float start = vertical ? t[3] : t[0], end = vertical ? t[1] : t[2];
            if (share <= 0f || whole <= start + end)
                return 0f;
            return Mathf.Round(start + share * (whole - start - end));
        }

        void Lay(float ratio, float trailRatio)
        {
            var size = ((RectTransform)transform).rect.size;
            bool vertical = shape == Shape.Vertical || shape == Shape.Orb;
            foreach (var image in new[] { trailImage, fillImage, flashImage })
                Size(image.rectTransform, size);
            if (shape <= Shape.Boss)
            {
                float whole = vertical ? size.y : size.x;
                Shown = OnTrack(ratio, whole);
                Clip(clipFill, Shown, size, vertical);
                Clip(clipTrail, OnTrack(trailRatio, whole), size, vertical);
            }
            else
            {
                Size(clipFill, size);
                Size(clipTrail, size);
                Shown = shape == Shape.Ring ? ratio : OnTrack(ratio, Orb);
                fillImage.fillAmount = flashImage.fillAmount = shape == Shape.Ring ? ratio : Shown / Orb;
                trailImage.fillAmount = shape == Shape.Ring ? trailRatio : OnTrack(trailRatio, Orb) / Orb;
            }
            Surface(ratio, size);
            Mark(size);
        }

        static void Size(RectTransform rt, Vector2 size)
        {
            if (rt.sizeDelta != size)
                rt.sizeDelta = size;
        }

        static void Clip(RectTransform clip, float shown, Vector2 size, bool vertical) =>
            Size(clip, vertical ? new Vector2(size.x, shown) : new Vector2(shown, size.y));

        /// <summary>An orb's liquid: its top row lit, across the circle at that height.</summary>
        void Surface(float ratio, Vector2 size)
        {
            bool on = shape == Shape.Orb && fillImage.enabled && ratio > 0f && ratio < 1f;
            float y = Orb - Shown, dy = y + 0.5f - Orb / 2f;
            on &= Mathf.Abs(dy) < Liquid;
            surface.enabled = on;
            if (!on)
                return;
            float half = Mathf.Sqrt(Liquid * Liquid - dy * dy), x0 = Mathf.Ceil(Orb / 2f - half), s = size.y / Orb;
            surface.rectTransform.anchoredPosition = new Vector2(x0 * s, -y * s);
            Size(surface.rectTransform, new Vector2((Mathf.Floor(Orb / 2f + half) - x0) * s, s));
            surface.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.4f + 0.5f * flashLeft));
        }

        /// <summary>The segment marks, a dark pixel line across the track, laid again when the bar changes.</summary>
        void Mark(Vector2 size)
        {
            int n = segments > 1 && shape <= Shape.Boss ? segments : 0;
            var key = $"{n}/{size}/{shape}";
            if (key == marked)
                return;
            marked = key;
            foreach (var m in marks)
                m.enabled = false;
            var t = Track[(int)shape];
            var inner = t == null ? Rect.zero : new Rect(t[0], t[3], size.x - t[0] - t[2], size.y - t[1] - t[3]);
            for (int i = 1; i < n; i++)
            {
                if (marks.Count < i)
                {
                    var made = Picture("Mark", transform);
                    made.color = new Color(0f, 0f, 0f, 0.55f);
                    made.rectTransform.SetSiblingIndex(glint.rectTransform.GetSiblingIndex());
                    marks.Add(made);
                }
                var mark = marks[i - 1];
                mark.enabled = true;
                if (shape == Shape.Vertical)
                {
                    mark.rectTransform.anchoredPosition = new Vector2(inner.x, inner.y + Mathf.Round(inner.height * i / n));
                    mark.rectTransform.sizeDelta = new Vector2(inner.width, 1f);
                }
                else
                {
                    mark.rectTransform.anchoredPosition = new Vector2(inner.x + Mathf.Round(inner.width * i / n), inner.y);
                    mark.rectTransform.sizeDelta = new Vector2(1f, inner.height);
                }
            }
        }
    }
}