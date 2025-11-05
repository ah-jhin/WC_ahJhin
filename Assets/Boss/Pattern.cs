using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 패턴 실행기(단일 스크립트).
/// - 체력% 조건으로 패턴 후보 필터 → 무작위 1개 실행 → 반복
/// - 각 패턴: 경고(텔레그래프) → 추가 지연 → 공격 생성 → 보스 이동/텔레포트 → 다음 패턴 딜레이
/// - 경고 위치 스냅샷/스폰포인트 스냅샷 지원(낚시 경고 포함)
/// - BossBase가 없어도 테스트 HP로 동작
/// </summary>
[DisallowMultipleComponent]
public class Pattern : MonoBehaviour
{
	// ─────────────────────────────────────────────────────────────
	// 외부 연결
	// ─────────────────────────────────────────────────────────────
	[Header("보스 체력 연결(선택)")]
	[Tooltip("같은 오브젝트에 BossBase가 있으면 자동으로 찾는다. 없으면 테스트 HP 사용")]
	public BossBase bossBase;

	[Header("테스트 모드 HP(보스 체력 미연결 시 사용)")]
	public bool useTestHp = true;      // true면 아래 값으로만 계산
	public float testMaxHp = 100f;     // 테스트 최대 체력
	public float testCurrentHp = 100f; // 테스트 현재 체력

	[Header("실행 제어")]
	public bool autoStart = true;             // 씬 시작 시 자동 실행
	public float emptyFallbackWait = 0.5f;    // 선택 가능한 패턴이 없을 때 대기

	private Coroutine _loop;
	private bool _stop;

	// ─────────────────────────────────────────────────────────────
	// 패턴 데이터
	// ─────────────────────────────────────────────────────────────
	[System.Serializable]
	public class BossPattern
	{
		// 조건
		[Header("조건(체력 %)")]
		[Range(0, 100)] public float minHpPercent = 0;   // 이 값 이상일 때 허용
		[Range(0, 100)] public float maxHpPercent = 100; // 이 값 이하일 때 허용

		// 공격 프리팹
		[Header("공격 프리팹 소환")]
		[Tooltip("실제 공격 프리팹(탄/레이저 등). 비우면 공격 생성 생략")]
		public GameObject prefab;               // 공격 프리팹
		[Tooltip("공격 생성 기준 Transform(플레이어 등). 비우면 보스 위치")]
		public Transform spawnPoint;            // 씬 오브젝트만 유효

		// 경고(텔레그래프)
		[Header("경고(텔레그래프)")]
		[Tooltip("공격 전에 잠깐 보여줄 경고 프리팹(화살표/타겟 등). 비우면 경고 생략")]
		public GameObject warningPrefab;        // 경고 프리팹
		[Tooltip("경고를 찍을 위치. 비우면 spawnPoint → 보스 순으로 사용")]
		public Transform warningPoint;          // 씬 오브젝트만 유효
		[Tooltip("경고 유지 시간(초). 0 이하면 프리팹 기본값 사용")]
		public float warningDuration = 0f;      // 경고 수명
		[Tooltip("경고음(선택). 2D로 1회 재생")]
		public AudioClip warningSfx;            // 경고 SFX
		[Range(0f, 1f)] public float warningSfxVolume = 0.8f; // SFX 볼륨
		[Tooltip("경고 후 실제 공격까지 추가 지연(초)")]
		public float attackDelayAfterWarning = 0f; // 경고→공격 지연
		[Tooltip("낚시 경고 확률(0~1). 확률 이하면 '공격을 스킵'")]
		[Range(0f, 1f)] public float fakeWarningChance = 0f;  // 낚시 확률

		// 스폰 동기화
		[Header("스폰 동기화")]
		[Tooltip("경고가 표시된 정확한 위치에 공격을 소환")]
		public bool spawnAtWarning = false;     // 경고 좌표에 고정 소환
		[Tooltip("경고가 뜬 '그 순간'의 spawnPoint 위치/회전을 스냅샷하여 소환")]
		public bool snapshotSpawnPointOnWarning = false; // 순간 위치 고정
		[Tooltip("소환 위치 보정(월드 좌표 기준)")]
		public Vector3 spawnOffset = Vector3.zero;       // 소환 오프셋
		[Tooltip("경고/스냅샷 회전을 그대로 사용할지 여부")]
		public bool inheritRotation = true;     // 회전 유지 여부

		// 보스 이동/텔레포트
		[Header("n초 후 천천히 이동(보스)")]
		public bool doSlowMove = false;         // 천천히 이동 사용
		public float slowMoveDelay = 0f;        // 이동 시작 대기
		public Transform slowMoveTarget;        // 목표 위치
		public float slowMoveDuration = 0.5f;   // 이동 시간(0이면 즉시)

		[Header("n초 후 순간이동(보스)")]
		public bool doTeleport = false;         // 텔레포트 사용
		public float teleportDelay = 0f;        // 텔레포트 대기
		public Transform teleportTarget;        // 목표 위치

		// 다음 패턴 딜레이
		[Header("다음 패턴까지 딜레이(초)")]
		public float delayMin = 0.5f;           // 최소
		public float delayMax = 1.0f;           // 최대

		// 내부 캐시(런타임 전용)
		[System.NonSerialized] public bool hasCachedSpawn;      // 스냅샷 유무
		[System.NonSerialized] public Vector3 cachedSpawnPos;   // 스냅샷 위치
		[System.NonSerialized] public Quaternion cachedSpawnRot; // 스냅샷 회전
	}

	[Header("기본 패턴 목록(무작위 선택)")]
	public List<BossPattern> randomPatterns = new List<BossPattern>();

	// ─────────────────────────────────────────────────────────────
	// 유니티 수명주기
	// ─────────────────────────────────────────────────────────────
	private void Reset()
	{
		// 같은 오브젝트에서 BossBase 자동 탐색
		bossBase = GetComponent<BossBase>();
	}

	private void Awake()
	{
		if (bossBase == null) bossBase = GetComponent<BossBase>();
	}

	private void Start()
	{
		if (autoStart) StartPatterns();
	}

	// ─────────────────────────────────────────────────────────────
	// 공개 API
	// ─────────────────────────────────────────────────────────────
	/// <summary>
	/// 외부 보스 체력 스크립트에서 호출(선택).
	/// 예) BossHealth.TakeDamage() 마지막 줄:
	/// FindFirstObjectByType<Pattern>()?.UpdateHpExternally(cur, max);
	/// </summary>
	public void UpdateHpExternally(float current, float max)
	{
		useTestHp = false;
		testCurrentHp = Mathf.Max(0f, current);
		testMaxHp = Mathf.Max(1f, max);
	}

	public void StartPatterns()
	{
		StopPatterns();
		_stop = false;
		_loop = StartCoroutine(MainLoop());
	}

	public void StopPatterns()
	{
		if (_loop != null) StopCoroutine(_loop);
		_loop = null;
	}

	// ─────────────────────────────────────────────────────────────
	// 메인 루프
	// ─────────────────────────────────────────────────────────────
	private IEnumerator MainLoop()
	{
		while (!_stop)
		{
			// 사망 처리: BossBase가 없으면 테스트 HP 사용
			if (GetHpPercent() <= 0f) yield break;

			// 체력% 조건으로 패턴 풀 구성
			var pool = BuildPool(GetHpPercent());
			if (pool.Count == 0)
			{
				yield return new WaitForSeconds(emptyFallbackWait);
				continue;
			}

			// 무작위 선택 후 실행
			var p = pool[Random.Range(0, pool.Count)];
			yield return ExecutePattern(p);
		}
	}

	// 체력% 조건 필터
	private List<BossPattern> BuildPool(float hpPercent)
	{
		var list = new List<BossPattern>();
		foreach (var p in randomPatterns)
		{
			if (p == null) continue;
			if (hpPercent >= p.minHpPercent && hpPercent <= p.maxHpPercent)
				list.Add(p);
		}
		return list;
	}

	// ─────────────────────────────────────────────────────────────
	// 패턴 1회 실행
	// ─────────────────────────────────────────────────────────────
	private IEnumerator ExecutePattern(BossPattern pattern)
	{
		if (pattern == null) yield break;

		// 0) 스냅샷 초기화
		pattern.hasCachedSpawn = false;

		// 1) 경고 위치 계산
		Vector3 warnPos;
		Quaternion warnRot;
		if (pattern.warningPoint != null && pattern.warningPoint.gameObject.scene.IsValid())
		{
			warnPos = pattern.warningPoint.position;
			warnRot = pattern.warningPoint.rotation;
		}
		else if (pattern.spawnPoint != null && pattern.spawnPoint.gameObject.scene.IsValid())
		{
			warnPos = pattern.spawnPoint.position;
			warnRot = pattern.spawnPoint.rotation;
		}
		else
		{
			warnPos = transform.position;
			warnRot = transform.rotation;
		}

		// 2) 경고 프리팹 소환
		if (pattern.warningPrefab != null)
		{
			var warn = Instantiate(pattern.warningPrefab, warnPos, warnRot);
			if (pattern.warningDuration > 0f) Destroy(warn, pattern.warningDuration);
		}

		// 2-1) 경고 SFX 재생(2D, 감쇠 없음)
		if (pattern.warningSfx != null)
		{
			var go = new GameObject("SFX_Telegraph_2D");
			var src = go.AddComponent<AudioSource>();
			src.clip = pattern.warningSfx;
			src.spatialBlend = 0f; // 2D
			src.volume = Mathf.Clamp01(pattern.warningSfxVolume);
			src.loop = false;
			src.playOnAwake = false;
			src.Play();
			Destroy(go, pattern.warningSfx.length + 0.05f);
		}

		// 3) 스폰 좌표 스냅샷
		if (pattern.spawnAtWarning)
		{
			// 경고가 뜬 그 자리로 고정
			pattern.cachedSpawnPos = warnPos + pattern.spawnOffset;
			pattern.cachedSpawnRot = warnRot;
			pattern.hasCachedSpawn = true;
		}
		else if (pattern.snapshotSpawnPointOnWarning)
		{
			// 경고 순간의 spawnPoint 좌표로 고정
			Vector3 basePos;
			Quaternion baseRot;
			if (pattern.spawnPoint != null && pattern.spawnPoint.gameObject.scene.IsValid())
			{
				basePos = pattern.spawnPoint.position;
				baseRot = pattern.spawnPoint.rotation;
			}
			else
			{
				basePos = transform.position;
				baseRot = transform.rotation;
			}
			pattern.cachedSpawnPos = basePos + pattern.spawnOffset;
			pattern.cachedSpawnRot = baseRot;
			pattern.hasCachedSpawn = true;
		}
		else
		{
			// 스냅샷 사용 안 함 → 생성 시점의 위치를 사용
			pattern.hasCachedSpawn = false;
		}

		// 4) 경고 후 추가 지연
		if (pattern.attackDelayAfterWarning > 0f)
			yield return new WaitForSeconds(pattern.attackDelayAfterWarning);

		// 5) 낚시 경고 처리(공격 스킵)
		if (pattern.fakeWarningChance > 0f && Random.value < pattern.fakeWarningChance)
			yield break;

		// 6) 공격 프리팹 생성
		if (pattern.prefab != null)
		{
			Vector3 pos;
			Quaternion rot;

			if (pattern.hasCachedSpawn)
			{
				pos = pattern.cachedSpawnPos;
				rot = pattern.cachedSpawnRot;
			}
			else
			{
				if (pattern.spawnPoint != null && pattern.spawnPoint.gameObject.scene.IsValid())
				{
					pos = pattern.spawnPoint.position + pattern.spawnOffset;
					rot = pattern.spawnPoint.rotation;
				}
				else
				{
					pos = transform.position + pattern.spawnOffset;
					rot = transform.rotation;
				}
			}

			if (!pattern.inheritRotation) rot = Quaternion.identity;
			Instantiate(pattern.prefab, pos, rot);
		}

		// 7) n초 후 천천히 이동
		if (pattern.doSlowMove && pattern.slowMoveTarget != null)
		{
			if (pattern.slowMoveDelay > 0f)
				yield return new WaitForSeconds(pattern.slowMoveDelay);

			if (pattern.slowMoveDuration <= 0f)
			{
				// 즉시 이동
				transform.position = new Vector3(
					pattern.slowMoveTarget.position.x,
					pattern.slowMoveTarget.position.y,
					transform.position.z
				);
			}
			else
			{
				// 보간 이동
				Vector3 start = transform.position;
				Vector3 end = new Vector3(
					pattern.slowMoveTarget.position.x,
					pattern.slowMoveTarget.position.y,
					start.z
				);
				float t = 0f;
				while (t < pattern.slowMoveDuration)
				{
					t += Time.deltaTime;
					float k = Mathf.Clamp01(t / pattern.slowMoveDuration);
					transform.position = Vector3.Lerp(start, end, k);
					yield return null;
				}
				transform.position = end;
			}
		}

		// 8) n초 후 순간이동
		if (pattern.doTeleport && pattern.teleportTarget != null)
		{
			if (pattern.teleportDelay > 0f)
				yield return new WaitForSeconds(pattern.teleportDelay);

			transform.position = new Vector3(
				pattern.teleportTarget.position.x,
				pattern.teleportTarget.position.y,
				transform.position.z
			);
		}

		// 9) 다음 패턴까지 딜레이
		float min = Mathf.Max(0f, Mathf.Min(pattern.delayMin, pattern.delayMax));
		float max = Mathf.Max(min, Mathf.Max(pattern.delayMin, pattern.delayMax));
		float wait = Random.Range(min, max);
		if (wait > 0f) yield return new WaitForSeconds(wait);

		// 10) 캐시 정리(다음 패턴 보호)
		pattern.hasCachedSpawn = false;
	}

	// ─────────────────────────────────────────────────────────────
	// 유틸
	// ─────────────────────────────────────────────────────────────
	private float GetHpPercent()
	{
		// BossBase 연동이 없다면 테스트 HP 사용
		if (useTestHp || bossBase == null)
		{
			if (testMaxHp <= 0f) return 0f;
			return Mathf.Clamp01(testCurrentHp / testMaxHp) * 100f;
		}

		// BossBase가 있다면 거기서 퍼센트를 가져오도록 확장해도 된다.
		// 현재 프로젝트 구조를 알 수 없으므로 테스트 모드만 확정 제공.
		if (testMaxHp <= 0f) return 0f;
		return Mathf.Clamp01(testCurrentHp / testMaxHp) * 100f;
	}

	private void OnValidate()
	{
		testMaxHp = Mathf.Max(1f, testMaxHp);
		testCurrentHp = Mathf.Clamp(testCurrentHp, 0f, testMaxHp);
	}
}
