// FlashFX.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class FlashFX : MonoBehaviour
{
    [Header("Flicker")]
    public float flickerSpeed = 10f;     // 깜빡임 속도
    [Range(0f, 1f)] public float minAlpha = 0.3f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    [Header("Fade")]
    public float fadeOutTime = 0.5f;     // 사라지는 시간

    [Header("Extras (optional)")]
    public float rotateSpeed = 0f;       // 살짝 회전 (0이면 끔)
    public float pulseScale = 0f;        // 0.05~0.15 정도 주면 크기가 살짝 펌핑

    SpriteRenderer sr;
    Color baseColor;
    Vector3 baseScale;
    bool playing;
    bool fading;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        baseScale = transform.localScale;
        // 투명 머티리얼(기본 Sprite-Default OK), Color alpha 1.0 이상 확인
    }

    void Update()
    {
        if (!playing || fading) return;

        // 알파 깜빡임
        float a = Mathf.Lerp(minAlpha, maxAlpha, 0.5f * (1f + Mathf.Sin(Time.time * flickerSpeed)));
        var c = sr.color; c.a = a; sr.color = c;

        if (rotateSpeed != 0f)
            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        if (pulseScale > 0f)
        {
            float s = 1f + Mathf.Sin(Time.time * (flickerSpeed * 0.5f)) * pulseScale;
            transform.localScale = baseScale * s;
        }
    }

    public void Play()
    {
        playing = true;
        fading = false;
        var c = sr.color; c.a = maxAlpha; sr.color = c;
    }

    public IEnumerator StopAndFade()
    {
        if (fading) yield break;
        fading = true;
        playing = false;

        float t = 0f;
        Color start = sr.color;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeOutTime);
            var c = start; c.a = Mathf.Lerp(start.a, 0f, k);
            sr.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
