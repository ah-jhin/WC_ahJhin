using UnityEngine;

/// <summary>
/// 제우스 전용 발사체 이동 스크립트.
/// - damage / 히트 판정은 기존 pain.cs 가 담당하고, 이 스크립트는 "이동과 수명"만 담당한다.
/// - Rigidbody2D 가 있으면 linearVelocity 를 사용하고,
///   없으면 Transform.Translate 로 직접 이동한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ZeusBulletMover : MonoBehaviour
{
	[Header("이동 설정")]
	[Tooltip("초기 속도 (방향 + 크기). 패턴 스크립트에서 설정한다.")]
	public Vector2 initialVelocity = Vector2.zero;

	[Tooltip("중력 사용 여부. true 이면 Rigidbody2D 의 gravityScale 을 그대로 사용한다.")]
	public bool useGravity = false;

	[Header("수명 설정")]
	[Tooltip("수명(초). 0 이하이면 시간으로 자동 파괴하지 않는다.")]
	public float lifeTime = 10f;

	Rigidbody2D _rb;
	bool _initialized = false;

	void Awake()
	{
		_rb = GetComponent<Rigidbody2D>();
	}

	void OnEnable()
	{
		// 수명 설정
		if (lifeTime > 0f)
			Destroy(gameObject, lifeTime);

		// Rigidbody2D 가 있을 경우, 즉시 초기 속도 적용
		if (_rb)
		{
			_rb.linearVelocity = initialVelocity;        // Unity 6 전용 속성
			_rb.gravityScale = useGravity ? _rb.gravityScale : 0f;
		}

		_initialized = true;
	}

	/// <summary>
	/// 패턴 스크립트에서 발사 직후 호출하는 초기화 함수.
	/// </summary>
	public void Init(Vector2 velocity)
	{
		initialVelocity = velocity;

		if (_rb)
			_rb.linearVelocity = initialVelocity;
	}

	void Update()
	{
		// Rigidbody2D 가 없으면 Transform 이동으로 대체
		if (!_rb && _initialized)
		{
			transform.position += (Vector3)(initialVelocity * Time.deltaTime);
		}
	}
}
