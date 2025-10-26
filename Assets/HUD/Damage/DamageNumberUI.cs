using UnityEngine;
using TMPro;
using System.Collections;

public class DamageNumberUI : MonoBehaviour
{
    public TMP_Text label;          // 같은 오브젝트의 TextMeshProUGUI
    public float riseSpeed = 60f;   // 픽셀/초
    public float life = 0.8f;       // 표시 시간

    CanvasGroup cg;
    RectTransform rt;
    Coroutine co;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (!label) label = GetComponent<TMP_Text>();           // 자동 참조
        EnsureCanvasGroup();                                     // ★ 필수
    }

    void OnEnable()
    {
        EnsureCanvasGroup();                                     // ★ 재확인
        if (cg) cg.alpha = 1f;
    }

    void EnsureCanvasGroup()
    {
        if (cg == null) cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        // 클릭 막지 않도록
        cg.interactable = false; 
        cg.blocksRaycasts = false;
    }

    public void Show(int amount, Color color)
    {
        if (!label) return;
        label.text = amount.ToString();
        label.color = color;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        EnsureCanvasGroup();                                      // ★ 안전
        float t = 0f;
        Vector2 start = rt.anchoredPosition;

        while (t < life)
        {
            t += Time.deltaTime;
            rt.anchoredPosition = start + Vector2.up * (riseSpeed * t);
            if (cg) cg.alpha = 1f - t / life;
            yield return null;
        }
        gameObject.SetActive(false);                              // 풀 복귀
    }
}
