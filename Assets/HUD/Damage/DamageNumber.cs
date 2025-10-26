// DamageNumber.cs  ← 기존 파일 교체
using UnityEngine;
using TMPro;
using System.Collections;

public class DamageNumber : MonoBehaviour
{
    public TMP_Text Label;      // 같은 오브젝트의 TMP(Text)를 가리켜야 함
    public float RiseSpeed = 1.5f;
    public float Life = 0.8f;

    CanvasGroup _cg;
    Coroutine _co;

    void Awake()
    {
        if (!Label) Label = GetComponent<TMP_Text>();          // 자동 연결
        _cg = GetComponent<CanvasGroup>();
        if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>(); // 페이드용
    }

    public void Show(int amount, Color color)
    {
        if (!Label) return;
        Label.text = amount.ToString();
        Label.color = color;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        float t = 0f;
        Vector3 start = transform.position;
        while (t < Life)
        {
            t += Time.deltaTime;
            transform.position = start + Vector3.up * (RiseSpeed * t);
            _cg.alpha = 1f - t / Life;
            yield return null;
        }
        gameObject.SetActive(false); // 풀로 복귀
    }
}
