// Assets/Scripts/UI/UICover.cs
// UI Image를 화면비에 맞춰 '여백 없이' 덮기
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class UICover : MonoBehaviour
{
    void LateUpdate()
    {
        var rt = (RectTransform)transform;
        var img = GetComponent<Image>();
        if (img.sprite == null) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float spriteAspect = img.sprite.rect.width / img.sprite.rect.height;

        // 부족한 축을 키워 화면 커버
        if (screenAspect > spriteAspect) rt.localScale = new Vector3(screenAspect / spriteAspect, 1f, 1f);
        else rt.localScale = new Vector3(1f, spriteAspect / screenAspect, 1f);
    }
}
