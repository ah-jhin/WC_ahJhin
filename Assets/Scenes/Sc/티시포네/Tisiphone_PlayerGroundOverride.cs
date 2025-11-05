using UnityEngine;

/// <summary>
/// Tisiphone 전용: 플레이어 바닥 판정을 안정화.
/// - 발밑 원(또는 캡슐) 오버랩으로 'Ground'만 검사
/// - 검사 결과를 PlayerMovement.isGrounded 에 강제로 반영
/// - 점프 시스템(이중점프 등) 로직은 건드리지 않음
/// </summary>
[DefaultExecutionOrder(-10000)] // PlayerMovement.Update 보다 먼저 실행되도록 매우 이른 순서
public class Tisiphone_PlayerGroundOverride : MonoBehaviour
{
	[Header("참조")]
	public PlayerMovement player;     // 플레이어 스크립트(Inspector에 드래그)

	[Header("Ground 체크 설정")]
	public LayerMask groundMask;      // 오직 바닥 레이어만 포함(타일맵/지면 등)
	public Transform probe;           // 발밑 기준점(없으면 자동 생성)
	public float radius = 0.15f;      // 원 반지름
	public float probeOffsetY = -0.5f; // 플레이어 중심에서 얼마나 아래를 볼지
									   // [추가 1] 헤더에 코요테 타임 파라미터 추가
	[Header("여유 판정")]
	public float coyoteTime = 0.10f; // 바닥을 막 떠났을 때도 잠깐 '지면'으로 처리
	float _coyote;                    // 내부 타이머

	void Reset()
	{
		player = GetComponent<PlayerMovement>();
	}

	void Awake()
	{
		if (!player) player = GetComponent<PlayerMovement>();
		if (!probe)
		{
			// 발밑 기준점이 없으면 임시 생성
			GameObject g = new GameObject("GroundProbe");
			g.transform.SetParent(transform, false);
			g.transform.localPosition = new Vector3(0f, probeOffsetY, 0f);
			probe = g.transform;
		}
	}
	void Update()
	{
		if (!player) return;

		Vector2 p = probe ? (Vector2)probe.position
						  : (Vector2)transform.position + new Vector2(0f, probeOffsetY);

		bool hitGround = Physics2D.OverlapCircle(p, radius, groundMask);

		// [추가 2] 코요테 타이머 갱신
		if (hitGround) _coyote = coyoteTime;
		else _coyote -= Time.deltaTime;

		// 코요테 타임 동안은 여전히 지면으로 간주
		bool grounded = hitGround || _coyote > 0f;

		player.isGrounded = grounded;

		if (grounded && player.GetComponent<Rigidbody2D>().linearVelocity.y < 0f)
		{
			var rb = player.GetComponent<Rigidbody2D>();
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
		}
	}


	// 에디터에서 확인용
	void OnDrawGizmosSelected()
	{
		Vector3 p = probe ? probe.position
						  : transform.position + new Vector3(0f, probeOffsetY, 0f);
		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(p, radius);
	}
}
