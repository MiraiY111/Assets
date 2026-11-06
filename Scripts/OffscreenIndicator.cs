using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panah di tepi layar yang menunjuk monster di luar kamera.
/// Pasang di objek UI yang di-stretch penuh (anchor 0,0 - 1,1) di bawah Canvas.
/// </summary>
public class OffscreenIndicator : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Kosong = Camera.main")]
    [SerializeField] private Camera cam;
    [Tooltip("Prefab berisi UI Image panah.")]
    [SerializeField] private Image arrowPrefab;

    [Header("Tampilan")]
    [Tooltip("Ukuran panah (piksel Canvas). Ukuran dan skala dari prefab diabaikan.")]
    [SerializeField] private Vector2 arrowSize = new Vector2(48f, 48f);
    [Tooltip("Jarak panah dari tepi layar (piksel). Naikkan kalau tertutup tombol.")]
    [SerializeField] private float edgePaddingPixels = 60f;
    [Tooltip("Sprite aslinya menunjuk ke ATAS = -90. Menunjuk ke kanan = 0.")]
    [SerializeField] private float rotationOffset = -90f;
    [Tooltip("Monster dianggap sudah terlihat kalau ada di dalam layar + margin ini (0.05 = 5%).")]
    [SerializeField] private float visibleMargin = 0.03f;

    [Header("Batas")]
    [Tooltip("Hanya monster terdekat yang ditunjuk.")]
    [SerializeField] private int maxArrows = 6;
    [SerializeField] private float refreshInterval = 0.25f;

    EnemyHealth[] enemies = new EnemyHealth[0];
    float nextRefresh;

    struct Candidate { public Vector2 viewport; public float dist; }
    readonly List<Candidate> candidates = new List<Candidate>();
    readonly List<Image> pool = new List<Image>();

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (arrowPrefab == null)
        {
            Debug.LogWarning($"{name}: Arrow Prefab belum diisi.");
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshInterval;
            enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        }

        candidates.Clear();
        Vector2 camPos = cam.transform.position;

        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;

            Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
            bool visible = vp.x > -visibleMargin && vp.x < 1f + visibleMargin
                        && vp.y > -visibleMargin && vp.y < 1f + visibleMargin;
            if (visible) continue;

            candidates.Add(new Candidate
            {
                viewport = new Vector2(vp.x, vp.y),
                dist = Vector2.Distance(camPos, e.transform.position)
            });
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
        int count = Mathf.Min(candidates.Count, maxArrows);

        float padX = edgePaddingPixels / Mathf.Max(1, Screen.width);
        float padY = edgePaddingPixels / Mathf.Max(1, Screen.height);
        float halfX = 0.5f - padX;
        float halfY = 0.5f - padY;

        for (int i = 0; i < count; i++)
        {
            Image arrow = GetArrow(i);
            arrow.gameObject.SetActive(true);

            Vector2 d = candidates[i].viewport - new Vector2(0.5f, 0.5f);

            float tx = Mathf.Abs(d.x) > 0.0001f ? halfX / Mathf.Abs(d.x) : float.MaxValue;
            float ty = Mathf.Abs(d.y) > 0.0001f ? halfY / Mathf.Abs(d.y) : float.MaxValue;
            float t = Mathf.Min(tx, ty);
            Vector2 pos = new Vector2(0.5f, 0.5f) + d * t;

            var rt = (RectTransform)arrow.transform;
            rt.anchorMin = rt.anchorMax = pos;
            rt.anchoredPosition = Vector2.zero;

            Vector2 px = new Vector2(d.x * Screen.width, d.y * Screen.height);
            float angle = Mathf.Atan2(px.y, px.x) * Mathf.Rad2Deg + rotationOffset;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        for (int i = count; i < pool.Count; i++)
            if (pool[i] != null) pool[i].gameObject.SetActive(false);
    }

    Image GetArrow(int index)
    {
        while (pool.Count <= index)
        {
            var img = Instantiate(arrowPrefab, transform);
            img.raycastTarget = false;

            // paksa bentuk yang benar, apa pun isi prefab
            var rt = (RectTransform)img.transform;
            rt.localScale = Vector3.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = arrowSize;
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);

            img.gameObject.SetActive(false);
            pool.Add(img);
        }
        return pool[index];
    }
}