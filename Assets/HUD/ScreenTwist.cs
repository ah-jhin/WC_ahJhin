using UnityEngine;
using System.Collections;

/// 화면 회전 컨트롤러(교정본)
/// - 카메라 Z회전
/// - HUDRoot도 동일 각도로 회전(옵션)
public class ScreenTwist : MonoBehaviour
{
    [Header("참조")]
    public Camera cam;                 // 메인 카메라
    public RectTransform hudRoot;      // Canvas 아래 HUDRoot

    [Header("설정")]
    public float angle = 90f;          // 회전 각도
    public float duration = 0.7f;      // 회전 시간
    public AnimationCurve ease = null; // 가감속 곡선
    public bool autoReturn = true;     // 원위치 복귀
    public float holdTime = 0.6f;      // 유지 시간

    [Header("옵션")]
    public bool rotateHUD = true;      // HUD도 회전
    public bool freezePlayer = true;   // 연출 동안 조작 잠금

    Quaternion camStartRot, hudStartRot;
    bool busy;

    void Awake()
    {
        // 기본 곡선
        if (ease == null) ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 카메라 자동
        if (!cam) cam = Camera.main;

        // HUDRoot 자동 탐색(오브젝트 이름 "HUDRoot")
        if (!hudRoot)
        {
            var canvas = FindFirstObjectByType<Canvas>();            // 경고 제거된 API
            if (canvas)
            {
                var t = canvas.transform.Find("HUDRoot");            // 동일 이름 자식 탐색
                if (t) hudRoot = t as RectTransform;
            }
        }
    }

    /// 외부에서 호출
    public void TriggerTwist()
    {
        if (!busy) StartCoroutine(TwistRoutine());
    }

    IEnumerator TwistRoutine()
    {
        busy = true;
        camStartRot = cam ? cam.transform.rotation : Quaternion.identity;
        if (hudRoot) hudStartRot = hudRoot.rotation;

        // 플레이어 조작 잠금(선택)
        var pm = FindFirstObjectByType<PlayerMovement>();            // 경고 제거된 API
        bool prevEnabled = pm ? pm.enabled : false;
        if (freezePlayer && pm) pm.enabled = false;

        // 1) 회전
        yield return RotateTo(angle);

        // 2) 유지
        yield return new WaitForSeconds(holdTime);

        // 3) 복귀
        if (autoReturn) yield return RotateTo(0f);

        if (freezePlayer && pm) pm.enabled = prevEnabled;
        busy = false;
    }

    IEnumerator RotateTo(float targetAngle)
    {
        if (!cam) yield break;

        float startZ = cam.transform.eulerAngles.z;
        float endZ = targetAngle;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float k = ease.Evaluate(Mathf.Clamp01(t));
            float z = Mathf.LerpAngle(startZ, endZ, k);

            // 카메라 회전
            cam.transform.rotation = Quaternion.Euler(0, 0, z);

            // HUD 회전
            if (rotateHUD && hudRoot)
                hudRoot.rotation = Quaternion.Euler(0, 0, z);

            yield return null;
        }
    }
}
