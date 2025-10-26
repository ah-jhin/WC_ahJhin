// Assets/Scripts/UI/Fader.cs
// 역할: CanvasGroup 알파를 서서히 변경하여 화면 페이드 처리
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class Fader : MonoBehaviour
{
	// 페이드 대상 (자동 할당)
	private CanvasGroup _cg;

	void Awake()
	{
		_cg = GetComponent<CanvasGroup>(); // CanvasGroup 캐시
		_cg.alpha = 0f;                    // 메뉴 진입 시 투명(필요 시 1로 바꿔 인트로 페이드인)
		gameObject.SetActive(true);        // 안전장치: 반드시 활성화
	}

	/// <summary>
	/// 화면을 일정 시간 동안 0->1로 어둡게 혹은 1->0으로 밝게 페이드한다.
	/// </summary>
	/// <param name="to">목표 알파(0=밝음, 1=어두움)</param>
	/// <param name="duration">지속시간(초)</param>
	public IEnumerator FadeTo(float to, float duration)
	{
		// 시작 알파 기록
		float from = _cg.alpha;
		float t = 0f;

		// duration이 0이거나 아주 작으면 즉시 전환
		if (duration <= 0.0001f)
		{
			_cg.alpha = to;
			yield break;
		}

		// 선형 보간으로 서서히 변경
		while (t < duration)
		{
			t += Time.unscaledDeltaTime;           // 메뉴는 일시정지 영향 없음: unscaled 사용
			_cg.alpha = Mathf.Lerp(from, to, t / duration);
			yield return null;
		}

		_cg.alpha = to;                             // 마지막 값 보정
	}
}
