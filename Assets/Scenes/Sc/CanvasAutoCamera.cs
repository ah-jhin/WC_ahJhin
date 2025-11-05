// Assets/Scripts/UI/CanvasAutoCamera.cs
// 역할: Screen Space - Camera 캔버스의 Render Camera를 자동으로 Main Camera에 연결
using UnityEngine;
[RequireComponent(typeof(Canvas))]
public class CanvasAutoCamera : MonoBehaviour
{
    void OnEnable()
    {
        var cv = GetComponent<Canvas>();
        if (cv.renderMode != RenderMode.ScreenSpaceCamera) return;

        // 메인 카메라 연결
        if (cv.worldCamera == null && Camera.main != null)
            cv.worldCamera = Camera.main;

        // UI 정렬 우선권 명시
        cv.overrideSorting = true;       // 수동 정렬 사용
        cv.sortingOrder = 100;           // UI가 스프라이트 위에 오도록
        // 필요 시: cv.sortingLayerName = "UI";  // 정렬 레이어를 쓰면 설정
    }
}