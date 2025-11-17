using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아스트라 전용 구체(탄막) 공통 스크립트
/// - 기본적으로 지정된 방향으로 이동하는 투사체.
/// - 1회성 유도(HomingOnce), 위로만 이동, 경로 이동(Path) 등을 지원한다.
/// - 피해량/무적시간/크기 증가/수명 등을 인스펙터에서 수정할 수 있다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AstraOrbProjectile : MonoBehaviour
{
	public enum MoveMode
	{
		Straight,   // 지정된 방향으로 직선 이동
		HomingOnce, // 일정 시간 후 목표를 향해 방향을 한 번만 잡고 직선 이동
		Path        // 미리 지정된 경로(웨이포인트)를 따라 순서대로 이동
	}

	[Header("공격 설정")]
	[Tooltip("최소 피해량. 실제 피해는 최소~최대 범위에서 랜덤으로 결정된다.")]
	[SerializeField] private int minDamage = 5;

	[Tooltip("최대 피해량. 최소와 동일하면 고정 피해가 된다.")]
	[SerializeField] private int maxDamage = 15;

	[Tooltip("한 번 피격한 대상에게 다시 피해를 줄 수 있기까지의 최소 시간(초). 0이면 제한 없음.")]
	[SerializeField] private float hitInterval = 0.5f;

	[Tooltip("플레이어(또는 다른 IDamageable)와 충돌 시 투사체를 즉시 파괴할지 여부.")]
	[SerializeField] private bool destroyOnHit = false;

	[Header("이동 설정")]
	[Tooltip("기본 이동 모드. Straight / HomingOnce / Path 세 가지 중 하나를 선택한다.")]
	[SerializeField] private MoveMode moveMode = MoveMode.Straight;

	[Tooltip("Straight, HomingOnce 모드에서 사용할 이동 속도(유닛/초).")]
	[SerializeField] private float moveSpeed = 6f;

	[Tooltip("HomingOnce 모드에서, 탄이 생성된 후 목표를 향해 움직이기까지의 대기 시간(초).")]
	[SerializeField] private float homingDelay = 1f;

	[Tooltip("Path 모드에서 각 경로 지점 사이를 이동하는 속도(유닛/초).")]
	[SerializeField] private float pathMoveSpeed = 4f;

	[Tooltip("투사체 생성 후 실제 이동을 시작하기까지의 지연(초). 0이면 즉시 이동.")]
	[SerializeField] private float startMoveDelay = 0f;

	[Header("수명 / 크기")]
	[Tooltip("투사체가 자동으로 파괴되기까지의 수명(초). 0 이하이면 자동 파괴를 하지 않는다.")]
	[SerializeField] private float lifeTime = 10f;

	[Tooltip("시간이 지남에 따라 크기를 점점 키울지 여부.")]
	[SerializeField] private bool growOverTime = false;

	[Tooltip("초당 크기 증가량. 1이면 1초에 scale이 1만큼 증가한다.")]
	[SerializeField] private float growSpeed = 0.5f;

	[Tooltip("투사체의 시작 크기(Scale). 0이면 현재 Transform scale 유지.")]
	[SerializeField] private float startScale = 1f;

	[Header("SFX / 이펙트")]
	[Tooltip("투사체가 생성될 때 재생할 사운드 (선택).")]
	[SerializeField] private AudioClip spawnSFX;

	[Tooltip("투사체가 생성될 때 SFX 를 재생할지 여부. 패턴에서 여러 개를 동시에 생성할 때는 끄고, 상위 컨트롤러에서 한 번만 재생하는 용도로 사용한다.")]
	[SerializeField] private bool playSpawnSfxOnStart = true;

	[Tooltip("투사체가 플레이어나 벽에 맞고 파괴될 때 재생할 이펙트 (선택).")]
	[SerializeField] private GameObject hitEffectPrefab;

	[Tooltip("오디오 재생에 사용할 AudioSource (선택). 비워두면 PlayClipAtPoint를 사용한다.")]
	[SerializeField] private AudioSource audioSource;

	// 내부 이동용 상태
	private Vector2 _moveDir = Vector2.right;
	private Transform _homingTarget;
	private bool _homingApplied = false;
	private float _spawnTime;
	private float _startMoveTime;

	// Path 모드용 경로
	private Vector3[] _pathPoints;
	private int _pathIndex = 0;

	// 피해 중복 방지용: 대상별 마지막 타격 시간
	private Dictionary<IDamageable, float> _lastHitTime = new Dictionary<IDamageable, float>();

	void Awake()
	{
		// 시작 스케일 설정
		if (startScale > 0f)
			transform.localScale = Vector3.one * startScale;
	}

	void Start()
	{
		_spawnTime = Time.time;
		_startMoveTime = Time.time + Mathf.Max(0f, startMoveDelay);

		// 생성 시 SFX (옵션)
		if (spawnSFX && playSpawnSfxOnStart)
		{
			if (audioSource)
				audioSource.PlayOneShot(spawnSFX);
			else
				AudioSource.PlayClipAtPoint(spawnSFX, transform.position, 1f);
		}

		// HomingOnce 모드: 일정 시간 후 방향 1회 결정
		if (moveMode == MoveMode.HomingOnce && _homingTarget != null)
		{
			StartCoroutine(HomingOnceRoutine());
		}

		// Path 모드: 경로 이동 코루틴 시작
		if (moveMode == MoveMode.Path && _pathPoints != null && _pathPoints.Length > 0)
		{
			StartCoroutine(PathMoveRoutine());
		}
	}

	void Update()
	{
		// 일정 시간이 지나면 자동으로 파괴
		if (lifeTime > 0f && Time.time - _spawnTime >= lifeTime)
		{
			Destroy(gameObject);
			return;
		}

		// Straight / HomingOnce(방향 확정 후) 모드 직선 이동
		if (Time.time >= _startMoveTime &&
			(moveMode == MoveMode.Straight || (moveMode == MoveMode.HomingOnce && _homingApplied)))
		{
			transform.position += (Vector3)_moveDir * moveSpeed * Time.deltaTime;
		}

		// 크기 증가 옵션
		if (growOverTime)
		{
			float s = transform.localScale.x + growSpeed * Time.deltaTime;
			transform.localScale = Vector3.one * Mathf.Max(0.01f, s);
		}
	}

	/// <summary>
	/// Straight 모드에서 사용할 방향을 설정한다.
	/// </summary>
	public void SetupStraight(Vector2 dir)
	{
		moveMode = MoveMode.Straight;
		_moveDir = dir.normalized;
	}

	/// <summary>
	/// HomingOnce 모드에서 사용할 목표와 지연 시간을 설정한다.
	/// </summary>
	public void SetupHomingOnce(Transform target, float delay)
	{
		moveMode = MoveMode.HomingOnce;
		_homingTarget = target;
		homingDelay = Mathf.Max(0f, delay);
	}

	/// <summary>
	/// Path 모드에서 사용할 경로(웨이포인트) 배열을 설정한다.
	/// </summary>
	public void SetupPath(Vector3[] worldPoints)
	{
		if (worldPoints == null || worldPoints.Length == 0) return;
		moveMode = MoveMode.Path;
		_pathPoints = worldPoints;
	}

	/// <summary>
	/// 이동 시작까지의 지연을 설정한다.
	/// </summary>
	public void SetStartMoveDelay(float delay)
	{
		startMoveDelay = Mathf.Max(0f, delay);
		_startMoveTime = Time.time + startMoveDelay;
	}

	/// <summary>
	/// 외부(패턴 컨트롤러)에서 이 구체의 스폰 SFX 재생 여부를 on/off 할 때 사용.
	/// 동시에 여러 개를 생성할 때는 false 로 꺼두고, 상위에서 한 번만 SFX 재생하도록 만들면 된다.
	/// </summary>
	public void SetSpawnSfxEnabled(bool enabled)
	{
		playSpawnSfxOnStart = enabled;
	}

	/// <summary>
	/// 외부에서 구체 이동 속도를 변경할 때 사용하는 함수.
	/// Straight/HomingOnce 모드의 moveSpeed 와 Path 모드의 pathMoveSpeed 를 동시에 변경한다.
	/// </summary>
	public void SetSpeed(float speed)
	{
		moveSpeed = speed;
		pathMoveSpeed = speed;
	}

	/// <summary>
	/// HomingOnce 모드에서 지정된 지연 시간 이후에 한 번 목표 방향을 잡는 코루틴.
	/// </summary>
	private IEnumerator HomingOnceRoutine()
	{
		if (homingDelay > 0f)
			yield return new WaitForSeconds(homingDelay);

		if (_homingTarget)
		{
			Vector2 dir = (_homingTarget.position - transform.position);
			if (dir.sqrMagnitude > 0.0001f)
				_moveDir = dir.normalized;
		}

		_homingApplied = true;
	}

	/// <summary>
	/// Path 모드에서 _pathPoints 를 순서대로 따라가는 코루틴.
	/// 각 포인트를 향해 이동하면서 도착하면 다음 포인트로 넘어간다.
	/// </summary>
	private IEnumerator PathMoveRoutine()
	{
		if (startMoveDelay > 0f)
			yield return new WaitForSeconds(startMoveDelay);

		while (_pathPoints != null && _pathPoints.Length > 0)
		{
			Vector3 target = _pathPoints[_pathIndex];
			while (Vector3.Distance(transform.position, target) > 0.05f)
			{
				Vector3 dir = (target - transform.position).normalized;
				transform.position += dir * pathMoveSpeed * Time.deltaTime;
				yield return null;
			}

			transform.position = target;
			_pathIndex = (_pathIndex + 1) % _pathPoints.Length;
			yield return null;
		}
	}

	void OnTriggerEnter2D(Collider2D other)
	{
		IDamageable target = other.GetComponent<IDamageable>();
		if (target != null)
		{
			float now = Time.time;
			if (hitInterval > 0f)
			{
				if (_lastHitTime.TryGetValue(target, out float last) &&
					now - last < hitInterval)
				{
					return;
				}
				_lastHitTime[target] = now;
			}

			int dMin = Mathf.Min(minDamage, maxDamage);
			int dMax = Mathf.Max(minDamage, maxDamage);
			int dmg = Random.Range(dMin, dMax + 1);

			target.TakeDamage(dmg, false, 0f);

			if (hitEffectPrefab)
				Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

			if (destroyOnHit)
				Destroy(gameObject);
		}
	}
}
