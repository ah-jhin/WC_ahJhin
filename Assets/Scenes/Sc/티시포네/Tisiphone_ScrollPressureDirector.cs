using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장면 전용 '스크롤 압박' 디렉터.
/// - Q 키(=보스 시작키) 입력 시 시작
/// - 지정한 스폰 지점들 중 랜덤 위치에 '그룹'을 랜덤 생성
/// - 그룹은 아래로 이동(Tisiphone_ScrollingBlock 부착)
/// - n초 뒤 자동 제거
/// - 보스 HP 임계 이하 → 강화 그룹으로 스폰 전환
/// - 보스 사망 → 즉시 모두 정지, 생성/제거 중단
/// </summary>
public class Tisiphone_ScrollPressureDirector : MonoBehaviour
{
	[Header("시작 트리거")]
	public KeyCode startKey = KeyCode.Q;  // 보스 시작과 동일하게 Q 사용
	public bool startOnce = true;         // 한 번만 시작

	[Header("스폰 지점(빈 오브젝트를 여기에 할당)")]
	public Transform[] spawnPoints;       // 위쪽에 여러 개 배치 권장

	[Header("스폰 소스(그룹 템플릿)")]
	[Tooltip("일반 그룹(왼쪽/가운데에 미리 만든 그 그룹 루트를 드래그")]
	public GameObject[] normalGroups;
	[Tooltip("강화 그룹(오른쪽에 만든 강화 패턴 루트들)")]
	public GameObject[] empoweredGroups;

	[Header("초기 배치(이미 씬에 있는 그룹을 바로 '내려가게'만 할 때)")]
	[Tooltip("Q를 누르면 여기의 오브젝트들이 즉시 하강 시작")]
	public Transform[] preplacedGroups;

	[Header("이동/수명 공통 파라미터")]
	public float blockMoveSpeed = 1.0f;   // 내려가는 속도
	public float blockLifeTime = 20f;     // 자동 제거 시간(불필요 오브젝트 정리용)
	public float firstSpawnDelay = 0f;    // 첫 스폰 지연
	public float spawnInterval = 2.0f;    // 스폰 간격(초)

	[Header("강화 전환 조건(보스 HP)")]
	public BossBase boss;                 // 씬의 BossBase를 드래그(없으면 자동 탐색)
	[Range(0.05f, 0.95f)]
	public float empowerAtHpPercent = 0.5f; // 보스 HP가 이 비율 '이하'면 강화 전환
	bool _empowered = false;                 // 현재 강화 모드 여부
											 // ▼▼ HP 단계별 속도(직접 입력) 추가 ▼▼
	[System.Serializable]
	public struct HpSpeedStep
	{
		[Range(0f, 1f)] public float hpPercent; // 보스 HP 비율 임계값(이 값 이하일 때 적용)
		public float speed;                     // 적용할 절대 속도
	}

	[Header("HP 단계 속도(직접 지정)")]
	public bool useSpeedSteps = false;          // 켜면 스텝이 곡선보다 우선
	public List<HpSpeedStep> speedSteps = new(); // 예) 0.75→1.2, 0.50→1.5, 0.30→1.8, 0.15→2.2

	// [Header("이동/수명 공통 파라미터")] 아래에 그대로 둔다.
	[Header("HP 연동 속도")]
	public bool useSpeedCurve = true;   // 체크 시 HP 곡선으로 속도 제어
	[Tooltip("x=보스 HP 비율(0~1), y=블럭 '절대 속도'")]
	public AnimationCurve speedCurve = new AnimationCurve(
		new Keyframe(1.0f, 1.0f),   // HP 100% → 속도 1.0
		new Keyframe(0.50f, 1.3f),  // HP 50%  → 속도 1.3
		new Keyframe(0.25f, 1.7f),  // HP 25%  → 속도 1.7
		new Keyframe(0.00f, 2.1f)   // HP 0%   → 속도 2.1
	);
	float _currentMoveSpeed;           // 현재 적용 중인 실제 속도


	// 내부 상태
	bool _running = false;                  // 스크롤 압박 진행 중
	bool _started = false;                  // 시작키가 이미 눌림
	Coroutine _spawnCo = null;
	readonly List<Tisiphone_ScrollingBlock> _actives = new(); // 현재 움직이는 블럭들

	void Awake()
	{
		// BossBase 미지정이면 자동으로 한 개 찾기
#if UNITY_2023_1_OR_NEWER
		if (!boss) boss = FindFirstObjectByType<BossBase>(FindObjectsInactive.Include);
#else
#pragma warning disable CS0618
        if (!boss) boss = FindObjectOfType<BossBase>();
#pragma warning restore CS0618
#endif
		_currentMoveSpeed = GetSpeedForRatio(1f); // 시작은 HP 100% 가정
	}

	void OnEnable()
	{
		// 보스 이벤트 구독(있을 때만)
		if (boss != null)
		{
			boss.OnHpChanged += HandleHpChanged;
			boss.OnBossDie += HandleBossDie;
		}
	}

	void OnDisable()
	{
		if (boss != null)
		{
			boss.OnHpChanged -= HandleHpChanged;
			boss.OnBossDie -= HandleBossDie;
		}
	}

	void Update()
	{
		// 아직 시작 안했고 Q가 눌리면 시작
		if (!_started && Input.GetKeyDown(startKey))
		{
			StartScroll();
			if (startOnce) _started = true;
		}
	}

	// ──────────────────────────────────────────────────────────
	// 시작
	// ──────────────────────────────────────────────────────────
	void StartScroll()
	{
		if (_running) return;
		_running = true;

		// 1) 씬에 미리 배치된 그룹들 즉시 하강 시작
		foreach (var t in preplacedGroups)
		{
			if (!t) continue;
			var mover = t.gameObject.GetComponent<Tisiphone_ScrollingBlock>();
			if (!mover) mover = t.gameObject.AddComponent<Tisiphone_ScrollingBlock>();
			mover.Apply(_currentMoveSpeed, blockLifeTime);
			_actives.Add(mover);
		}

		// 2) 스폰 루프 시작
		_spawnCo = StartCoroutine(CoSpawnLoop());
	}

	IEnumerator CoSpawnLoop()
	{
		if (firstSpawnDelay > 0f) yield return new WaitForSeconds(firstSpawnDelay);

		while (_running)
		{
			// 스폰 지점이 하나도 없으면 아무 것도 하지 않음
			if (spawnPoints != null && spawnPoints.Length > 0)
			{
				// 강화 여부에 따라 소스 풀 결정
				var source = (!_empowered ? normalGroups : empoweredGroups);

				if (source != null && source.Length > 0)
				{
					// 1) 랜덤 템플릿
					GameObject pick = source[Random.Range(0, source.Length)];
					// 2) 랜덤 스폰 포인트
					Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
					// 3) 생성
					GameObject go = Instantiate(pick, p.position, p.rotation);
					// 4) 이동 컴포넌트 부착/설정
					var mover = go.GetComponent<Tisiphone_ScrollingBlock>();
					if (!mover) mover = go.AddComponent<Tisiphone_ScrollingBlock>();
					mover.Apply(_currentMoveSpeed, blockLifeTime);
					_actives.Add(mover);
				}
			}

			// 다음 스폰까지 대기
			yield return new WaitForSeconds(spawnInterval);
		}
	}

	// ──────────────────────────────────────────────────────────
	// 보스 HP 변화 → 강화 전환 판단
	// ──────────────────────────────────────────────────────────
	void HandleHpChanged(int current, int max)
	{
		if (max <= 0) return;
		float ratio = (float)current / max;

		// 강화 전환(기존 로직 유지)
		if (!_empowered && ratio <= empowerAtHpPercent)
			_empowered = true;

		// 속도 계산: 스텝 우선 → 없으면 곡선 → 없으면 기본
		float newSpeed;
		if (!TryGetSpeedFromSteps(ratio, out newSpeed))
			newSpeed = GetSpeedForRatio(ratio);

		if (!Mathf.Approximately(newSpeed, _currentMoveSpeed))
			ApplySpeedToActives(newSpeed); // 이미 떨어지는 것 포함 전체 갱신
	}

	// ──────────────────────────────────────────────────────────
	// 보스 사망 → 즉시 정지 + 생성/제거 중단
	// ──────────────────────────────────────────────────────────
	void HandleBossDie(BossBase _)
	{
		// 1) 스폰 중지
		_running = false;
		if (_spawnCo != null) StopCoroutine(_spawnCo);
		_spawnCo = null;

		// 2) 활동 중인 모든 블럭 정지 + 자동 제거도 멈춤
		for (int i = 0; i < _actives.Count; i++)
		{
			var m = _actives[i];
			if (!m) continue;
			m.Freeze();           // 이동 정지
			m.StopAutoDestroy();  // 더 이상 파괴하지 않음
		}
	}
	// x: 0(빈사) ~ 1(풀피), 반환: 절대 속도
	float GetSpeedForRatio(float hpRatio)
	{
		if (useSpeedCurve && speedCurve != null && speedCurve.keys.Length > 0)
			return Mathf.Max(0f, speedCurve.Evaluate(Mathf.Clamp01(hpRatio)));
		return blockMoveSpeed; // 곡선 비사용 시 기본값
	}
	// hpRatio: 0(빈사)~1(풀피). true면 stepSpeed에 값이 들어감.
	bool TryGetSpeedFromSteps(float hpRatio, out float stepSpeed)
	{
		stepSpeed = 0f;
		if (!useSpeedSteps || speedSteps == null || speedSteps.Count == 0) return false;

		// hpPercent 오름차순 정렬 가정. 정렬 보장:
		speedSteps.Sort((a, b) => a.hpPercent.CompareTo(b.hpPercent));

		// "ratio ≤ 임계값" 중에서 가장 작은 임계값을 찾음 → 가장 이른 단계가 적용
		for (int i = 0; i < speedSteps.Count; i++)
		{
			if (hpRatio <= speedSteps[i].hpPercent)
			{
				stepSpeed = Mathf.Max(0f, speedSteps[i].speed);
				return true;
			}
		}
		return false; // 아직 어떤 임계도 충족하지 않음 → 기본/곡선 사용
	}

	// 새/기존 모든 블럭에 즉시 반영
	void ApplySpeedToActives(float speed)
	{
		_currentMoveSpeed = speed;
		for (int i = 0; i < _actives.Count; i++)
		{
			var m = _actives[i];
			if (m) m.SetSpeed(speed); // 이미 내려오는 것도 즉시 갱신
		}
	}
}
