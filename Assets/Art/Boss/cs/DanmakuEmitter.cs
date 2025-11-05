using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 등 발사체 "방출기"에 부착하는 스크립트
/// - 스크린샷처럼 원형으로 여러 번 연사하여 점선 궤적을 만든다
/// - 스파이럴(회전 사격)도 가능
/// - 피해/충돌은 탄알 프리팹의 다른 스크립트가 처리한다고 가정
/// </summary>
public class DanmakuEmitter : MonoBehaviour
{
	// ========================= 기본 설정 =========================
	[Header("필수")]
	[Tooltip("발사할 탄알 프리팹. 예: CircleBullet 프리팹")]
	public GameObject bulletPrefab;                  // 탄알 프리팹

	[Tooltip("시작시 자동으로 패턴 실행")]
	public bool playOnStart = true;                  // 자동 시작

	[Tooltip("시작 지연 시간(초)")]
	[Min(0f)]
	public float startDelay = 0.0f;                  // 시작 딜레이

	[Tooltip("루프 실행 여부")]
	public bool loop = true;                         // 루프 실행

	[Tooltip("한 번의 패턴이 끝난 뒤 다음 패턴까지 대기(초)")]
	[Min(0f)]
	public float loopInterval = 1.0f;                // 루프 간격

	[Header("풀링(선택)")]
	[Tooltip("Instantiate/Destroy 대신 간단한 풀링 사용")]
	public bool usePooling = true;                   // 풀링 사용 여부
	[Tooltip("초기 미리 만들어 둘 탄알 수")]
	[Min(0)]
	public int prewarmCount = 60;                    // 초기 생성 수

	// ========================= 패턴 공통 =========================
	public enum PatternType { RepeatingBurst, Spiral }
	[Header("패턴 선택")]
	public PatternType pattern = PatternType.RepeatingBurst;

	[Header("공통: 발사 각도/반지름")]
	[Tooltip("첫 발사 각도(도). 0=오른쪽, 90=위")]
	public float baseAngleDeg = 90f;                 // 시작 각도
	[Tooltip("총구 오프셋 반지름. 0이면 발사기 중심에서 생성")]
	public float spawnRadius = 0.0f;                 // 생성 반지름

	[Header("공통: 탄알 이동")]
	[Tooltip("탄알 초기 속도(유닛/초)")]
	public float bulletSpeed = 8f;                   // 초기 속도
	[Tooltip("가속도(유닛/초^2). 음수면 감속")]
	public float bulletAcceleration = 0f;            // 가속도
	[Tooltip("탄알 생존 시간(초). 끝나면 자동 반환/파괴")]
	[Min(0.01f)]
	public float bulletLifeTime = 4f;                // 생존 시간

	// ========================= RepeatingBurst(점선 궤적) =========================
	[Header("RepeatingBurst(점선 궤적)")]
	[Tooltip("한 번의 링에서 발사할 탄알 수")]
	[Min(1)]
	public int bulletsPerRing = 36;                  // 링당 탄알 수

	[Tooltip("링을 몇 번 연속으로 쏠지. 값이 클수록 점선 궤적이 촘촘해짐")]
	[Min(1)]
	public int subBurstCount = 12;                   // 연속 링 수

	[Tooltip("각 링 사이의 시간 간격(초). 작을수록 점선 간격이 짧아짐")]
	[Min(0f)]
	public float subBurstInterval = 0.05f;           // 링 간격

	[Tooltip("각 링마다 추가로 회전할 각도(도). 0이면 같은 방향으로 점선 생성")]
	public float offsetPerSubBurstDeg = 4f;          // 링 회전량

	[Tooltip("연속 링을 몇 묶음으로 쏠지")]
	[Min(1)]
	public int burstRepeat = 1;                      // 묶음 반복 수

	[Tooltip("묶음 간 대기 시간(초)")]
	[Min(0f)]
	public float burstInterval = 0.25f;              // 묶음 간격

	[Tooltip("가까운 곳에 작은 장식 링을 추가로 찍어낼지(스크린샷의 안쪽 원 느낌)")]
	public bool spawnInnerRing = true;               // 안쪽 장식 링
	[Tooltip("장식 링 반지름")]
	[Min(0f)]
	public float innerRingRadius = 0.8f;             // 장식 링 반지름
	[Tooltip("장식 링 탄알 수")]
	[Min(1)]
	public int innerRingBullets = 20;                // 장식 링 탄알 수

	// ========================= Spiral =========================
	[Header("Spiral(회전 사격)")]
	[Tooltip("스파이럴 총 지속 시간(초)")]
	[Min(0.01f)]
	public float spiralDuration = 3f;                // 지속 시간
	[Tooltip("샷 간 간격(초)")]
	[Min(0.005f)]
	public float spiralShotInterval = 0.03f;         // 발사 간격
	[Tooltip("초당 도 회전. 양수=시계반대, 음수=시계 방향")]
	public float spiralSpinDegPerSec = 240f;         // 회전 속도
	[Tooltip("샷 1번에 발사할 탄알 수(팔 수). 1=단일, 2=양팔…")]
	[Min(1)]
	public int spiralArms = 1;                       // 팔 수
	[Tooltip("팔 사이 각도 간격(도)")]
	public float spiralArmSpreadDeg = 180f;          // 팔 간격

	// =============== 내부: 간단 풀 ===============
	class Pool
	{
		readonly GameObject _prefab;
		readonly Transform _root;
		readonly Queue<GameObject> _q = new Queue<GameObject>();
		public Pool(GameObject prefab, int prewarm, Transform root)
		{
			_prefab = prefab;
			_root = root;
			for (int i = 0; i < prewarm; i++)
			{
				var go = GameObject.Instantiate(_prefab, _root);
				go.SetActive(false);
				_q.Enqueue(go);
			}
		}
		public GameObject Get()
		{
			if (_q.Count > 0)
			{
				var go = _q.Dequeue();
				go.SetActive(true);
				return go;
			}
			return GameObject.Instantiate(_prefab, _root);
		}
		public void Return(GameObject go)
		{
			go.SetActive(false);
			_q.Enqueue(go);
		}
	}

	Pool _pool; // 내부 풀 인스턴스

	void Awake()
	{
		// 풀 초기화
		if (usePooling && bulletPrefab != null)
			_pool = new Pool(bulletPrefab, prewarmCount, null);
	}

	void Start()
	{
		if (playOnStart) StartCoroutine(Run());
	}

	/// <summary>
	/// 코루틴으로 패턴 실행
	/// </summary>
	IEnumerator Run()
	{
		if (startDelay > 0) yield return new WaitForSeconds(startDelay);

		do
		{
			switch (pattern)
			{
				case PatternType.RepeatingBurst:
					yield return StartCoroutine(Fire_RepeatingBurst());
					break;
				case PatternType.Spiral:
					yield return StartCoroutine(Fire_Spiral());
					break;
			}

			if (loopInterval > 0) yield return new WaitForSeconds(loopInterval);

		} while (loop);
	}

	// ----------------------- 패턴 1: RepeatingBurst -----------------------
	IEnumerator Fire_RepeatingBurst()
	{
		for (int r = 0; r < burstRepeat; r++)
		{
			float angle = baseAngleDeg;

			for (int i = 0; i < subBurstCount; i++)
			{
				// 1) 바깥 링
				SpawnRing(transform.position, spawnRadius, angle, bulletsPerRing, bulletSpeed);

				// 2) 장식용 안쪽 링(선택)
				if (spawnInnerRing && innerRingBullets > 0 && innerRingRadius > 0f)
					SpawnRing(transform.position, innerRingRadius, angle, innerRingBullets, bulletSpeed * 0.6f);

				// 다음 링을 약간 회전. 이게 "점선 궤적"을 만든다.
				angle += offsetPerSubBurstDeg;

				if (subBurstInterval > 0f)
					yield return new WaitForSeconds(subBurstInterval);
				else
					yield return null; // 다음 프레임
			}

			if (burstInterval > 0f) yield return new WaitForSeconds(burstInterval);
		}
	}

	// ----------------------- 패턴 2: Spiral -----------------------
	IEnumerator Fire_Spiral()
	{
		float t = 0f;
		float angle = baseAngleDeg;

		while (t < spiralDuration)
		{
			// 팔 수 만큼 동시에 발사
			for (int arm = 0; arm < spiralArms; arm++)
			{
				float a = angle + arm * spiralArmSpreadDeg;
				SpawnBulletAtAngle(transform.position, spawnRadius, a, bulletSpeed);
			}

			if (spiralShotInterval > 0f) yield return new WaitForSeconds(spiralShotInterval);
			else yield return null;

			t += Mathf.Max(spiralShotInterval, Time.deltaTime);
			angle += spiralSpinDegPerSec * Mathf.Max(spiralShotInterval, Time.deltaTime);
		}
	}

	// ----------------------- 생성 유틸 -----------------------
	void SpawnRing(Vector3 center, float radius, float startAngleDeg, int count, float speed)
	{
		float step = 360f / Mathf.Max(1, count);
		for (int i = 0; i < count; i++)
		{
			float ang = startAngleDeg + step * i;
			SpawnBulletAtAngle(center, radius, ang, speed);
		}
	}

	void SpawnBulletAtAngle(Vector3 center, float radius, float angleDeg, float speed)
	{
		// 각도 → 단위 방향
		float rad = angleDeg * Mathf.Deg2Rad;
		Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

		// 생성 위치: 중심 + 반지름 * 방향
		Vector3 pos = center + (Vector3)(dir * radius);

		// 탄알 인스턴스 생성/가져오기
		GameObject b;
		if (usePooling && _pool != null) b = _pool.Get();
		else b = Instantiate(bulletPrefab);

		b.transform.position = pos;
		b.transform.rotation = Quaternion.AngleAxis(angleDeg - 90f, Vector3.forward); // 스프라이트 방향 보정(필요 시)

		// 이동 스크립트에 파라미터 전달
		var mover = b.GetComponent<SimpleBulletMover>();
		if (mover == null)
		{
			// 없으면 자동 추가하여 이동만 담당하게 함
			mover = b.AddComponent<SimpleBulletMover>();
		}
		mover.Launch(dir, speed, bulletAcceleration, bulletLifeTime, usePooling ? (System.Action<GameObject>)_pool.Return : null);
	}

	// 에디터에서 반지름 확인용
	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.white;
		if (spawnRadius > 0f) Gizmos.DrawWireSphere(transform.position, spawnRadius);
		if (spawnInnerRing && innerRingRadius > 0f)
		{
			Gizmos.color = Color.gray;
			Gizmos.DrawWireSphere(transform.position, innerRingRadius);
		}
	}
}
