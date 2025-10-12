// FlashFX.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class FlashFX : MonoBehaviour
{
    [Header("Flicker")]
    public float flickerSpeed = 10f;     // ������ �ӵ�
    [Range(0f, 1f)] public float minAlpha = 0.3f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    [Header("Fade")]
    public float fadeOutTime = 0.5f;     // ������� �ð�

    [Header("Extras (optional)")]
    public float rotateSpeed = 0f;       // ��¦ ȸ�� (0�̸� ��)
    public float pulseScale = 0f;        // 0.05~0.15 ���� �ָ� ũ�Ⱑ ��¦ ����

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
        // ���� ��Ƽ����(�⺻ Sprite-Default OK), Color alpha 1.0 �̻� Ȯ��
    }

    void Update()
    {
        if (!playing || fading) return;

        // ���� ������
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
