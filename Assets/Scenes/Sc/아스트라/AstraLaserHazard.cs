using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아스트라 전용 레이저(바닥/측면/레이저 패턴 공통) 스크립트
/// - 직선 이동 또는 일정 시간 동안 목표를 추적하는 레이저를 구현한다.
/// - 레이저 1, 레이저 2 패턴 모두 이 스크립트를 사용한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AstraLaserHazard : MonoBehaviour
{
	[Header("공격 설정")]
	[Tooltip("최소 피해량. 실제 피해는 최소~최대 범위에서 랜덤으로 결정된다.")]
	[SerializeField] private int minDamage = 5;

	[Tooltip("최대 피해량. 최소와 동일하면 고정 피해가 된다.")]
	[SerializeField] private int maxDamage = 15;

	[Tooltip("같은 대상에게 다시 피해를 줄 수 있기까지의 시간(초). 0이면 제한 없음.")]
	[SerializeField] private float hitInterval = 0.5f;

	[Tooltip("대상을 한 번 맞춘 후 레이저를 즉시 파괴할지 여부.")]
	[SerializeField] private bool destroyOnHit = false;

	[Header("이동 / 수명")]
	[Tooltip("기본 이동 방향. (0,1)=위, (1,0)=오른쪽. 추적 모드에서는 매 프레임 목표를 향해 갱신된다.")]
	[SerializeField] private Vector2 moveDir = Vector2.up;

	[Tooltip("레이저 이동 속도(유닛/초). 0이면 이동하지 않는다.")]
	[SerializeField] private float moveSpeed = 0f;

	[Tooltip("레이저의 전체 수명(초). 0 이하이면 자동 파괴하지 않는다.")]
	[SerializeField] private float lifeTime = 3f;

	[Header("추적 옵션 (레이저 2용)")]
	[Tooltip("이 레이저가 일정 시간 동안 목표를 추적할지 여부.")]
	[SerializeField] private bool useHoming = false;

	[Tooltip("추적이 유지되는 시간(초). 이 시간이 지나면 마지막 방향으로만 계속 이동한다.")]
	[SerializeField] private float homingDuration = 1f;

	// 내부: 추적 대상
	private Transform homingTarget;
	// 내부: 추적 종료 시각
	private float homingEndTime;
	// 내부: 생성 시각
	private float spawnTime;

	[Header("카메라 흔들림 옵션")]
	[Tooltip("레이저가 생성될 때 카메라를 흔들지 여부.")]
	[SerializeField] private bool shakeCameraOnSpawn = false;

	[Tooltip("카메라 흔들림 지속 시간(초).")]
	[SerializeField] private float shakeDuration = 0.2f;

	[Tooltip("카메라 흔들림 강도.")]
	[SerializeField] private float shakeMagnitude = 1f;

	[Tooltip("카메라 흔들림 주기(Hz). 값이 클수록 더 빠르게 떤다.")]
	[SerializeField] private float shakeFrequency = 25f;

	[Header("SFX / 이펙트")]
	[Tooltip("레이저 생성 시 재생할 사운드(선택).")]
	[SerializeField] private AudioClip spawnSFX;

	[Tooltip("레이저가 피해를 줄 때 생성할 히트 이펙트 프리팹(선택).")]
	[SerializeField] private GameObject hitEffectPrefab;

	[Tooltip("오디오 재생용 AudioSource(선택). 비워두면 PlayClipAtPoint 사용.")]
	[SerializeField] private AudioSource audioSource;

	// 내부: 피해 중복 방지용
	private readonly Dictionary<IDamageable, float> _lastHitTime =
		new Dictionary<IDamageable, float>();

	// 내부: 카메라 컨트롤러 캐시
	private CameraController cam;

	private void Start()
	{
		moveDir = moveDir.normalized;
		cam = CameraController.Instance;
		spawnTime = Time.time;

		// 추적 모드라면 종료 시각 계산
		if (useHoming && homingDuration > 0f)
			homingEndTime = spawnTime + homingDuration;

		// 카메라 흔들림
		if (shakeCameraOnSpawn && cam != null && shakeDuration > 0f && shakeMagnitude > 0f)
		{
			cam.Shake(shakeDuration, shakeMagnitude, shakeFrequency);
		}

		// 생성 시 SFX
		if (spawnSFX)
		{
			if (audioSource)
				audioSource.PlayOneShot(spawnSFX);
			else
				AudioSource.PlayClipAtPoint(spawnSFX, transform.position, 1f);
		}

		// 수명 타이머
		if (lifeTime > 0f)
			Destroy(gameObject, lifeTime);
	}

	private void Update()
	{
		if (moveSpeed != 0f)
		{
			// 추적 모드: homingDuration 동안 목표를 계속 따라감
			if (useHoming && homingTarget != null && Time.time < homingEndTime)
			{
				Vector2 dir = (Vector2)(homingTarget.position - transform.position);
				if (dir.sqrMagnitude > 0.0001f)
				{
					moveDir = dir.normalized;

					// 레이저의 회전을 이동 방향에 맞춰서 갱신
					float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
					transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
				}
			}
			// 실제 이동
			transform.position += (Vector3)(moveDir * moveSpeed * Time.deltaTime);
		}
	}

	/// <summary>
	/// 단순 직선 레이저로 설정할 때 사용.
	/// </summary>
	public void SetupStraight(Vector2 direction, float speed)
	{
		moveDir = direction.normalized;
		moveSpeed = speed;
		useHoming = false;
	}

	/// <summary>
	/// 일정 시간 동안 목표를 추적하는 레이저로 설정할 때 사용.
	/// - target: 추적할 대상(주로 플레이어)
	/// - speed : 이동 속도
	/// - duration: 추적 유지 시간(초)
	/// </summary>
	public void SetupHoming(Transform target, float speed, float duration)
	{
		homingTarget = target;
		moveSpeed = speed;
		useHoming = true;
		homingDuration = Mathf.Max(0f, duration);

		spawnTime = Time.time;
		homingEndTime = spawnTime + homingDuration;

		// 시작 시점에서도 방향 한 번 맞춰준다.
		if (homingTarget != null)
		{
			Vector2 dir = (Vector2)(homingTarget.position - transform.position);
			if (dir.sqrMagnitude > 0.0001f)
				moveDir = dir.normalized;
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		IDamageable dmg = other.GetComponent<IDamageable>();
		if (dmg != null)
		{
			float now = Time.time;

			// 같은 대상에게 너무 자주 맞는 것 방지
			if (hitInterval > 0f)
			{
				if (_lastHitTime.TryGetValue(dmg, out float last) &&
					now - last < hitInterval)
				{
					return;
				}

				_lastHitTime[dmg] = now;
			}

			// 피해량 계산
			int dMin = Mathf.Min(minDamage, maxDamage);
			int dMax = Mathf.Max(minDamage, maxDamage);
			int amount = Random.Range(dMin, dMax + 1);
			dmg.TakeDamage(amount, false, 0f);

			if (hitEffectPrefab)
				Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

			if (destroyOnHit)
				Destroy(gameObject);
		}
	}
}
