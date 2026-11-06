using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Memutar frame sprite pada komponen Image (UI) secara berulang.
/// Pasang di objek Title (yang punya Image), isi Frames berurutan dari frame pertama ke terakhir.
/// </summary>
[RequireComponent(typeof(Image))]
public class UISpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 8f;
    [SerializeField] private bool loop = true;
    [Tooltip("Aktif: jalan walau Time.timeScale = 0 (mis. saat pause).")]
    [SerializeField] private bool useUnscaledTime = true;

    Image image;
    float timer;
    int index;

    void Awake() { image = GetComponent<Image>(); }

    void OnEnable()
    {
        timer = 0f;
        index = 0;
        if (frames != null && frames.Length > 0 && frames[0] != null) image.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length < 2 || framesPerSecond <= 0f) return;

        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float frameTime = 1f / framesPerSecond;

        while (timer >= frameTime)
        {
            timer -= frameTime;
            index++;
            if (index >= frames.Length)
            {
                if (!loop) { index = frames.Length - 1; enabled = false; return; }
                index = 0;
            }
            if (frames[index] != null) image.sprite = frames[index];
        }
    }
}