// Assets/Scripts/Camera/CameraEffects.cs
// 카메라 흔들림/줌/회전 총괄. App 씬의 Main Camera에 부착.
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraEffects : MonoBehaviour
{
    // 기본 상태 저장(복귀용)
    private Vector3 _basePos;
    private float _baseSize;
    private float _shakeAmp, _shakeFreq;
    private float _shakeTimeLeft;
    private Coroutine _zoomCo, _rotCo;

    private Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _basePos = transform.localPosition;          // 보통 (0,0,-10)
        _baseSize = _cam.orthographicSize;           // 기본 줌
    }

    void LateUpdate()
    {
        // 흔들림 노이즈 적용(프레임마다)
        if (_shakeTimeLeft > 0f)
        {
            _shakeTimeLeft -= Time.unscaledDeltaTime; // 연출은 보통 unscaled
            float t = Time.unscaledTime * _shakeFreq;
            float ox = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * _shakeAmp;
            float oy = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * _shakeAmp;
            transform.localPosition = _basePos + new Vector3(ox, oy, 0f);
            if (_shakeTimeLeft <= 0f) transform.localPosition = _basePos; // 종료 보정
        }
    }

    // ■ 외부 API: 흔들림
    public void Shake(float duration, float amplitude = 0.25f, float frequency = 25f)
    {
        _shakeTimeLeft = duration;
        _shakeAmp = amplitude;
        _shakeFreq = frequency;
    }

    // ■ 외부 API: 줌(OrthographicSize 보간)
    public void ZoomTo(float size, float duration)
    {
        if (_zoomCo != null) StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(ZoomRoutine(size, duration));
    }
    IEnumerator ZoomRoutine(float size, float duration)
    {
        float from = _cam.orthographicSize;
        if (duration <= 0f) { _cam.orthographicSize = size; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _cam.orthographicSize = Mathf.Lerp(from, size, t / duration);
            yield return null;
        }
        _cam.orthographicSize = size;
    }

    // ■ 외부 API: 회전(Z축)
    public void RotateTo(float zAngle, float duration)
    {
        if (_rotCo != null) StopCoroutine(_rotCo);
        _rotCo = StartCoroutine(RotateRoutine(zAngle, duration));
    }
    IEnumerator RotateRoutine(float z, float duration)
    {
        Quaternion from = transform.localRotation;
        Quaternion to = Quaternion.Euler(0, 0, z);
        if (duration <= 0f) { transform.localRotation = to; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            transform.localRotation = Quaternion.Slerp(from, to, t / duration);
            yield return null;
        }
        transform.localRotation = to;
    }

    // ■ 외부 API: 초기값 복귀
    public void ResetAll(float duration = 0.2f)
    {
        _shakeTimeLeft = 0f;
        transform.localPosition = _basePos;
        ZoomTo(_baseSize, duration);
        RotateTo(0f, duration);
    }

    // ■ 초기 기준 갱신(스테이지마다 다른 기본값을 쓰고 싶을 때)
    public void SetBase(float size)
    {
        _baseSize = size;
    }
}
