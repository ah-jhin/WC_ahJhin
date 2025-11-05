using UnityEngine;

/// <summary>
/// 탄알에 부착되는 아주 단순한 이동 전용 스크립트
/// - 충돌/피해는 다른 스크립트가 처리
/// - DanmakuEmitter가 Launch(...)로 매개변수 전달
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SimpleBulletMover : MonoBehaviour
{
	// 내부 상태
	Vector2 _dir;                 // 이동 방향(정규화)
	float _speed;                 // 현재 속도
	float _accel;                 // 가속도
	float _life;                  // 남은 수명
	System.Action<GameObject> _onReturnToPool; // 풀 반환 콜백(없으면 Destroy)

	/// <summary>
	/// 탄알을 발사할 때 호출
	/// </summary>
	/// <param name="dir">정규화된 방향</param>
	/// <param name="speed">초기 속도</param>
	/// <param name="accel">가속도</param>
	/// <param name="lifeTime">수명(초)</param>
	/// <param name="returnToPool">풀 반환 콜백. null이면 Destroy</param>
	public void Launch(Vector2 dir, float speed, float accel, float lifeTime, System.Action<GameObject> returnToPool)
	{
		_dir = dir.normalized;
		_speed = speed;
		_accel = accel;
		_life = Mathf.Max(0.01f, lifeTime);
		_onReturnToPool = returnToPool;

		// 콜라이더는 Trigger 권장. 충돌 피해 스크립트가 OnTriggerEnter2D에서 처리.
		var col = GetComponent<Collider2D>();
		if (col) col.isTrigger = true;

		enabled = true; // 이동 시작
	}

	void OnEnable()
	{
		// 외부에서 Launch 이전에 Enable될 수도 있으니 안전장치
		if (_life <= 0f) enabled = false;
	}

	void Update()
	{
		// 1) 이동
		_speed += _accel * Time.deltaTime;
		float step = Mathf.Max(0f, _speed) * Time.deltaTime;
		transform.position += (Vector3)(_dir * step);

		// 2) 수명 소모
		_life -= Time.deltaTime;
		if (_life <= 0f)
		{
			Despawn();
		}
	}

	/// <summary>
	/// 수명 종료 혹은 외부에서 호출 시 탄알 제거
	/// </summary>
	public void Despawn()
	{
		if (_onReturnToPool != null) _onReturnToPool.Invoke(gameObject);
		else Destroy(gameObject);
		enabled = false;
	}
}
