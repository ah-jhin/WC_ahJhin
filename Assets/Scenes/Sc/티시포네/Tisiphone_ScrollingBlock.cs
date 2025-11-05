using System.Collections;
using UnityEngine;

/// <summary>
/// 내려오는 블럭(또는 블럭 '그룹'의 루트)에 붙여서 사용.
/// - 매 프레임 아래로 이동
/// - n초 뒤 자동 제거(옵션)
/// - 보스 사망 시 즉시 정지 + 자동 제거도 멈춤
/// </summary>
public class Tisiphone_ScrollingBlock : MonoBehaviour
{
	[Header("이동/수명")]
	public float moveSpeed = 1.0f;   // 1초에 내려갈 유닛. 양수=아래로
	public float lifeTime = 20f;     // n초 뒤 자동 파괴. 0 이하면 자동 제거 안함
	public void SetSpeed(float speed) { moveSpeed = speed; }    // 런타임에 속도만 바꿀 때 사용

	// 내부 상태
	bool _frozen = false;            // true면 이동 정지
	bool _allowAutoDestroy = true;   // false면 수명 코루틴이 더이상 파괴하지 않음

	void OnEnable()
	{
		// 수명 타이머 시작
		if (lifeTime > 0f) StartCoroutine(CoLife());
	}

	void Update()
	{
		// 정지 상태가 아니면 아래로 이동
		if (!_frozen)
		{
			// 월드 좌표 기준으로 '아래'로 이동
			transform.position += Vector3.down * moveSpeed * Time.deltaTime;
		}
	}

	IEnumerator CoLife()
	{
		float t = 0f;
		while (t < lifeTime)
		{
			t += Time.deltaTime;
			yield return null;
		}
		// 생성/제거 중단 요구가 들어오면 파괴하지 않음
		if (_allowAutoDestroy) Destroy(gameObject);
	}

	/// <summary>외부에서 호출: 즉시 정지</summary>
	public void Freeze()
	{
		_frozen = true;
	}

	/// <summary>외부에서 호출: 자동 제거도 금지</summary>
	public void StopAutoDestroy()
	{
		_allowAutoDestroy = false;
	}

	/// <summary>디렉터가 런타임에 값 주입할 때 사용(선택)</summary>
	public void Apply(float speed, float life)
	{
		moveSpeed = speed;
		lifeTime = life;
	}
}
