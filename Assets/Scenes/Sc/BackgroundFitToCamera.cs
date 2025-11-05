// Assets/Scripts/Stage/BackgroundFitToCamera.cs
// 카메라 뷰를 '커버'하도록 스프라이트 스케일을 자동 조정
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFitToCamera : MonoBehaviour
{
    [Tooltip("여백 비율(0.1 = 10% 크게)")] public float margin = 0f;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        var sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        float sprH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;
        float sprW = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;

        float scale = Mathf.Max(camW / sprW, camH / sprH) * (1f + margin);
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
