using UnityEngine;

/// <summary>
/// 단순 직선 탄알. 발사기에서 Init로 파라미터 주입.
/// - Rigidbody2D(있으면 사용) 없으면 Transform 이동
/// - 중력, 트레일, 수명 파괴, 크기 변화, 충돌 on/off
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class bs_CircleBullet : MonoBehaviour
{
	// 런타임 설정 묶음
	public struct Params
	{
		public Vector2 startDir;      // 시작 방향(정규화)
		public float speed;           // 속도
		public float lifeTime;        // 수명
		public bool enableCollision;  // 충돌 사용
		public bool useTrail;         // 잔상 사용
		public float trailTime;       // 잔상 시간
		public float trailWidth;      // 잔상 두께
		public float scaleDeltaPerSec;// 크기 변화
		public float gravityScale;    // 중력 스케일(2D)
	}

	// 내부 상태
	Vector2 _dir = Vector2.right;
	float _speed = 5f;
	float _life;
	float _scaleDeltaPerSec;
	Rigidbody2D _rb;
	Collider2D _col;

	public void Init(Params p)
	{
		// 방향/속도/수명
		_dir = (p.startDir.sqrMagnitude < 0.0001f ? Vector2.right : p.startDir).normalized;
		_speed = Mathf.Max(0f, p.speed);
		_life = Mathf.Max(0.05f, p.lifeTime);
		_scaleDeltaPerSec = p.scaleDeltaPerSec;

		// 중력 및 물리
		_rb = GetComponent<Rigidbody2D>();
		if (!_rb) _rb = gameObject.AddComponent<Rigidbody2D>();
		_rb.gravityScale = p.gravityScale;
		_rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
		_rb.interpolation = RigidbodyInterpolation2D.Interpolate;

		// 충돌
		_col = GetComponent<Collider2D>();
		if (!_col) _col = gameObject.AddComponent<CircleCollider2D>(); // 동그란 탄이라면 원형
		_col.isTrigger = !p.enableCollision; // 기본 요구: 충돌 X → 트리거로 둠(또는 비활성)
		_col.enabled = p.enableCollision;     // 완전 끄고 싶으면 false

		// 잔상(트레일)
		if (p.useTrail)
		{
			var tr = gameObject.GetComponent<TrailRenderer>();
			if (!tr) tr = gameObject.AddComponent<TrailRenderer>();
			tr.time = p.trailTime;
			tr.startWidth = p.trailWidth;
			tr.endWidth = 0f;
			tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			tr.receiveShadows = false;
			tr.autodestruct = false;
			// 머티리얼은 기본 라인 머티리얼 사용(원한다면 프리팹에서 교체)
		}

		// 최초 속도 적용
		_rb.linearVelocity = _dir * _speed;

		// 수명 타이머 시작
		CancelInvoke(nameof(Kill));
		Invoke(nameof(Kill), _life);
	}

	void Update()
	{
		// Rigidbody2D가 이동을 담당하므로 여기서는 크기 변화만
		if (_scaleDeltaPerSec != 0f)
		{
			float d = 1f + _scaleDeltaPerSec * Time.deltaTime;
			transform.localScale *= d;
		}
	}
	void FixedUpdate()
	{
		// Rigidbody2D가 있으면 타입에 따라 이동 방식 결정
		if (_rb)
		{
			if (_rb.bodyType == RigidbodyType2D.Dynamic)
			{
				// 매 물리 프레임에 원하는 속도로 고정
				_rb.linearVelocity = _dir * _speed;
			}
			else // Kinematic 또는 Static
			{
				// velocity가 무시되므로 직접 위치 이동(스크립트 시뮬레이션)
				Vector2 next = _rb.position + _dir * _speed * Time.fixedDeltaTime;
				_rb.MovePosition(next);
			}
		}
		else
		{
			// Rigidbody2D가 전혀 없다면 Transform 이동
			transform.position += (Vector3)(_dir * _speed * Time.fixedDeltaTime);
		}
	}


	void Kill()
	{
		Destroy(gameObject);
	}
}
