// Assets/Scripts/Loading/Fader.cs
// 기능: CanvasGroup 알파를 0↔1로 보간. 타임스케일=0에서도 동작.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class Fader : MonoBehaviour
{
    [Tooltip("시작 알파(보통 0)")] public float initialAlpha = 0f;

    CanvasGroup _cg;
    Coroutine _co;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        SetAlpha(initialAlpha); // 시작 상태 고정
    }

    // 알파 즉시 설정 + 입력 차단 동기화
    public void SetAlpha(float a)
    {
        a = Mathf.Clamp01(a);
        _cg.alpha = a;
        _cg.blocksRaycasts = a > 0f;
        _cg.interactable = a > 0f;
    }

    // 목표 알파까지 보간(언스케일드 시간)
    public IEnumerator FadeTo(float target, float duration)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FadeRoutine(Mathf.Clamp01(target), Mathf.Max(0f, duration)));
        yield return _co;
    }

    IEnumerator FadeRoutine(float target, float duration)
    {
        float from = _cg.alpha;
        if (Mathf.Approximately(from, target) || duration <= 0f) { SetAlpha(target); yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;                  // 타임스케일 무시
            SetAlpha(Mathf.Lerp(from, target, t / duration));
            yield return null;
        }
        SetAlpha(target);                                 // 종료 보정
        _co = null;
    }
}
