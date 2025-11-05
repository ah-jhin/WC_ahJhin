// Assets/Scripts/UI/CanvasBindToMainCamera.cs
// 목적: Screen Space–Camera Canvas를 App의 Main Camera에 자동 바인딩
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]                                // 에디터에서도 동작
[RequireComponent(typeof(Canvas))]
public class CanvasBindToMainCamera : MonoBehaviour
{
    [Tooltip("에디터(재생 전)에서도 계속 바인딩 시도")]
    public bool bindInEditor = true;

    Canvas _cv;

    void OnEnable()
    {
        _cv = GetComponent<Canvas>();
        SceneManager.activeSceneChanged += OnSceneChanged;   // 씬 전환 시 재바인딩
        TryBind();                                           // 즉시 1회
    }
    void OnDisable() => SceneManager.activeSceneChanged -= OnSceneChanged;
    void OnSceneChanged(Scene _, Scene __) => TryBind();

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && bindInEditor) TryBind(); // 에디터 미리보기 보정
#endif
    }

    /// <summary>메인 카메라를 찾아 Canvas.worldCamera에 연결</summary>
    public void TryBind()
    {
        if (_cv.renderMode != RenderMode.ScreenSpaceCamera) return;

        // 1) 가장 안전한 탐색(비활성 포함)
        var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include)
                                ?? Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (cam == null) return;

        if (_cv.worldCamera != cam)
        {
            _cv.worldCamera = cam;            // 카메라 지정
            _cv.planeDistance = 1f;
            _cv.overrideSorting = true;
            _cv.sortingOrder = 100;
            // 필요 시: _cv.sortingLayerName = "UI";
        }
    }
}
