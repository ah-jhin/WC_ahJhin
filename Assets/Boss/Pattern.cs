using System.Collections;               // 코루틴
using System.Collections.Generic;       // List
using UnityEngine;                      // Unity API
using System;
using UObject = UnityEngine.Object;       // UnityEngine.Object 별칭
using URandom = UnityEngine.Random;       // UnityEngine.Random 별칭
using SRandom = System.Random;            // System.Random 별칭

/// <summary>
/// 보스 패턴 실행기(단일 스크립트).
/// - 랜덤 패턴: 체력% 조건 풀 → 무작위 1개 실행 → 반복
/// - 고정 패턴: HP% 이하 트리거 시 지정 리스트를 n회 사용 → 종료 후 랜덤으로 복귀
/// - 각 패턴: 경고 → 추가지연 → 공격 생성(수명/사운드) → 이동/텔레포트 → 다음 딜레이
/// - BossSequenceController에서 보스 소환 후 n초 뒤 시작 신호를 받을 수 있음.
/// - [추가] 경고/공격 시 배경/카메라 연출 훅 제공
/// </summary>
[DisallowMultipleComponent]
public class Pattern : MonoBehaviour
{
	// ─────────────────────────────────────────────────────────────
	// 외부 연결 및 시작 제어
	// ─────────────────────────────────────────────────────────────
	[Header("보스 체력 연결(선택)")]
	[Tooltip("같은 오브젝트에 BossBase가 있으면 자동으로 찾음. 없으면 테스트 HP 사용")]
	public BossBase bossBase;

	[Header("테스트 HP(연결 없을 때만 사용)")]
	public bool useTestHp = true;
	public float testMaxHp = 100f;
	public float testCurrentHp = 100f;

	[Header("실행 제어")]
	[Tooltip("씬 시작 시 자동 실행")]
	public bool autoStart = true;
	[Tooltip("선택 가능한 패턴이 없을 때 대기(초)")]
	public float emptyFallbackWait = 0.5f;

	[Header("보스 소환 신호 대기")]
	[Tooltip("외부 신호를 받을 때까지 시작하지 않음(BossSequenceController에서 SignalBossSpawned 호출)")]
	public bool waitForSpawnSignal = false;
	[Tooltip("보스 소환 신호를 받은 뒤 시작까지 추가 지연(초)")]
	public float startDelayAfterSpawn = 0f;

	Coroutine _loop;
	bool _stop;

	// ─────────────────────────────────────────────────────────────
	// 타이밍별 연출 세트(인스펙터에서 세팅)
	// ─────────────────────────────────────────────────────────────
	[System.Serializable]
	public struct Tisiphone_BgTween
	{
		[Tooltip("연출 대상 배경(BackgroundUVScroller)")]
		public BackgroundUVScroller target;

		[Header("목표값")]
		[Tooltip("UV 스크롤 속도(초당). X=가로, Y=세로")]
		public Vector2 uvSpeed;
		[Tooltip("회전 속도(도/초)")]
		public float rotationSpeed;

		[Header("보간/복귀")]
		[Tooltip("현재→목표 보간 시간(초). 0이면 즉시")]
		public float lerpTime;
		[Tooltip("목표 상태 유지 시간(초)")]
		public float holdTime;
		[Tooltip("유지 후 원래 값으로 복귀")]
		public bool revert;
		[Tooltip("이징 커브(비워두면 선형)")]
		public AnimationCurve ease;
	}

	/// <summary>
	/// 경고/공격 타이밍에 카메라 FX를 적용하기 위한 구조체
	/// </summary>
	[System.Serializable]
	public struct Tisiphone_CamFx
	{
		[Tooltip("연출 대상 카메라(CameraEffects). 비우면 자동 탐색")]
		public CameraEffects cam;

		[Header("쉐이크")]
		public bool shake;
		public float shakeDuration, shakeAmplitude, shakeFrequency;

		[Header("줌")]
		public bool zoom;
		public float zoomSize, zoomTime;

		[Header("회전")]
		public bool rotate;
		public float rotateZ, rotateTime;

		[Header("자동 리셋")]
		public bool autoReset;
		public float autoResetDelay, resetEaseTime;
	}

	[Header("연출: 경고(텔레그래프) 발생 시")]
	public Tisiphone_BgTween[] onWarningBackgrounds;   // 경고 직후 배경 보간
	public Tisiphone_CamFx[] onWarningCameras;       // 경고 직후 카메라 FX

	[Header("연출: 실제 공격 프리팹 생성 시")]
	public Tisiphone_BgTween[] onAttackBackgrounds;    // 공격 생성 직후 배경 보간
	public Tisiphone_CamFx[] onAttackCameras;        // 공격 생성 직후 카메라 FX

	// ─────────────────────────────────────────────────────────────
	// 패턴 데이터
	// ─────────────────────────────────────────────────────────────
	[System.Serializable]
	public class BossPattern
	{
		// 조건
		[Header("조건(체력 %)")]
		[Range(0, 100)] public float minHpPercent = 0;
		[Range(0, 100)] public float maxHpPercent = 100;

		// 공격 프리팹
		[Header("공격 프리팹 소환")]
		[Tooltip("실제 공격 프리팹(탄/레이저 등). 비우면 공격 생략")]
		public GameObject prefab;
		[Tooltip("공격 생성 기준 Transform(플레이어 등). 비우면 보스 위치")]
		public Transform spawnPoint;

		// 경고(텔레그래프)
		[Header("경고(텔레그래프)")]
		[Tooltip("공격 전에 잠깐 보여줄 경고 프리팹(화살표/타겟 등). 비우면 경고 생략")]
		public GameObject warningPrefab;
		[Tooltip("경고를 찍을 위치. 비우면 spawnPoint → 보스 순으로 사용")]
		public Transform warningPoint;
		[Tooltip("경고 유지 시간(초). 0 이하면 프리팹 기본값 사용")]
		public float warningDuration = 0f;
		[Tooltip("경고음(선택). 2D로 1회 재생")]
		public AudioClip warningSfx;
		[Range(0f, 1f)] public float warningSfxVolume = 0.8f;
		[Tooltip("경고 후 실제 공격까지 추가 지연(초)")]
		public float attackDelayAfterWarning = 0f;
		[Tooltip("낚시 경고 확률(0~1). 확률 이하면 공격을 스킵")]
		[Range(0f, 1f)] public float fakeWarningChance = 0f;
		// ───────── 회전 설정(이 패턴 전용) ─────────
		[Header("회전 설정(이 패턴 전용)")]
		[Tooltip("true면 이 패턴에서 최종 Z회전을 강제 적용한다")]
		public bool applyRotation = true;

		public enum RotationSource { PrefabDefault, SpawnPoint, WarningPoint, LookTarget, CustomAngle }

		[Tooltip("회전 기준 선택")]
		public RotationSource rotationSource = RotationSource.SpawnPoint;

		[Tooltip("LookTarget 기준일 때 바라볼 대상. 비어있으면 Tag==Player를 자동 탐색")]
		public Transform lookTarget;

		[Tooltip("CustomAngle 기준일 때 사용할 Z각도(도 단위)")]
		public float customAngleZ = 0f;

		[Tooltip("최종 각도에 더할 오프셋(도)")]
		public float angleOffsetZ = 0f;

		[Tooltip("Rigidbody2D가 있으면 SetRotation까지 적용한다")]
		public bool alsoSetRigidbody2D = true;
		// ───────── 경고(텔레그래프) 회전 설정 ─────────
		[Header("경고 회전(자동)")]
		[Tooltip("true면 경고 프리팹에 각도를 자동 적용한다")]
		public bool warnApplyRotation = true;

		// 아래 enum은 이미 BossPattern에 있다면 재선언 금지. 없다면 같은 위치에 선언해둔 enum을 사용.
		// public enum RotationSource { PrefabDefault, SpawnPoint, WarningPoint, LookTarget, CustomAngle }

		[Tooltip("경고 각도의 기준")]
		public RotationSource warnRotationSource = RotationSource.LookTarget;

		[Tooltip("LookTarget일 때 바라볼 대상. 비워두면 Player 태그를 자동 탐색")]
		public Transform warnLookTarget;

		[Tooltip("CustomAngle일 때 사용할 Z각도(도)")]
		public float warnCustomAngleZ = 0f;

		[Tooltip("최종 각도에 더할 오프셋(도). 스프라이트가 '오른쪽'이 정면이라면 0, '위쪽'이 정면이면 90 권장")]
		public float warnAngleOffsetZ = 0f;

		[Tooltip("경고가 살아있는 동안 매 프레임 대상 방향을 향하도록 회전 갱신")]
		public bool warnTrackTarget = true;

		[Tooltip("Rigidbody2D가 있으면 SetRotation도 함께 적용")]
		public bool warnAlsoSetRigidbody2D = true;


		// 스폰 동기화
		[Header("스폰 동기화")]
		[Tooltip("경고가 표시된 정확한 위치에 공격을 소환")]
		public bool spawnAtWarning = false;
		[Tooltip("경고가 뜬 순간의 spawnPoint 위치/회전을 스냅샷하여 소환")]
		public bool snapshotSpawnPointOnWarning = false;
		[Tooltip("소환 위치 보정(월드 좌표 기준)")]
		public Vector3 spawnOffset = Vector3.zero;
		[Tooltip("경고/스냅샷 회전을 그대로 사용할지 여부")]
		public bool inheritRotation = true;

		// 공격 수명/사운드
		[Header("공격 프리팹 수명/사운드")]
		[Tooltip("공격 프리팹을 생성 후 이 시간(초) 뒤 자동 삭제. 0 이하면 삭제하지 않음")]
		public float attackPrefabLifetime = 0f;
		[Tooltip("공격이 발동되는 순간 재생할 SFX(2D)")]
		public AudioClip attackSfx;
		[Range(0f, 1f)] public float attackSfxVolume = 0.85f;

		// 연출
		[Header("연출(이 패턴 전용)")]
		[Tooltip("경고(텔레그래프) 발생 시 배경 트윈. 비우면 상단 전역 On Warning Backgrounds 사용")]
		public Tisiphone_BgTween[] warnBackgrounds;

		[Tooltip("경고(텔레그래프) 발생 시 카메라 FX. 비우면 상단 전역 On Warning Cameras 사용")]
		public Tisiphone_CamFx[] warnCameras;

		[Tooltip("실제 공격 프리팹 생성 시 배경 트윈. 비우면 상단 전역 On Attack Backgrounds 사용")]
		public Tisiphone_BgTween[] attackBackgrounds;

		[Tooltip("실제 공격 프리팹 생성 시 카메라 FX. 비우면 상단 전역 On Attack Cameras 사용")]
		public Tisiphone_CamFx[] attackCameras;


		// 보스 이동/텔레포트
		[Header("n초 후 천천히 이동(보스)")]
		public bool doSlowMove = false;
		public float slowMoveDelay = 0f;
		public Transform slowMoveTarget;
		public float slowMoveDuration = 0.5f;

		[Header("n초 후 순간이동(보스)")]
		public bool doTeleport = false;
		public float teleportDelay = 0f;
		public Transform teleportTarget;

		// 다음 패턴 딜레이
		[Header("다음 패턴까지 딜레이(초)")]
		public float delayMin = 0.5f;
		public float delayMax = 1.0f;

		// 내부 캐시(런타임 전용)
		[System.NonSerialized] public bool hasCachedSpawn;
		[System.NonSerialized] public Vector3 cachedSpawnPos;
		[System.NonSerialized] public Quaternion cachedSpawnRot;
	}

	[Header("기본 패턴 목록(무작위 선택)")]
	public List<BossPattern> randomPatterns = new List<BossPattern>();

	// ─────────────────────────────────────────────────────────────
	// 고정 패턴(HP% 이하에서 n회 사용 후 종료)
	// ─────────────────────────────────────────────────────────────
	[System.Serializable]
	public class FixedPatternSet
	{
		[Header("발동 조건")]
		[Range(0, 100)] public float triggerHpPercent = 50f; // 현재 HP%가 이 값 이하가 되면 발동

		[Header("패턴 목록(랜덤 선택)")]
		public List<BossPattern> patterns = new List<BossPattern>();

		[Header("반복 횟수")]
		[Tooltip("이 세트를 몇 번 사용할지")]
		public int useCount = 1;

		[Header("한 번만 발동")]
		[Tooltip("true면 한 번 완료 후 재발동하지 않음")]
		public bool triggerOnce = true;

		// 내부 상태
		[System.NonSerialized] public int used;       // 사용된 횟수
		[System.NonSerialized] public bool consumed;  // 더 이상 발동하지 않음
	}

	[Header("고정 패턴 세트")]
	public List<FixedPatternSet> fixedPatternSets = new List<FixedPatternSet>();
	FixedPatternSet _activeFixed; // 현재 진행 중인 고정 세트
	private System.Random _rng;
	// ▼ 같은 패턴만 반복 선택되는 현상 방지용 상태
	private int _lastRandomIdx = -1;                                   // 랜덤 패턴 마지막 인덱스
	private System.Collections.Generic.Dictionary<FixedPatternSet, int>
		_lastFixedIdx = new System.Collections.Generic.Dictionary<FixedPatternSet, int>(); // 고정 세트별 마지막 인덱스

	// 에디터에서만 컴파일되는 간단 로그 헬퍼
	[System.Diagnostics.Conditional("UNITY_EDITOR")]
	void DBG(string msg) { Debug.Log(msg); }

	// ─────────────────────────────────────────────────────────────
	// 수명주기
	// ─────────────────────────────────────────────────────────────
	void Reset()
	{
		bossBase = GetComponent<BossBase>(); // 같은 오브젝트에서 자동 탐색
	}

	void Awake()
	{
		if (bossBase == null) bossBase = GetComponent<BossBase>();
		_rng = new SRandom(unchecked(Environment.TickCount ^ GetInstanceID())); // 실행마다 다른 시드
		if (bossBase != null && useTestHp) useTestHp = false;
	}

	void Start()
	{
		// 보스 소환 신호를 기다리는 경우 Start에서 자동 시작하지 않음
		if (autoStart && !waitForSpawnSignal)
			StartPatterns();
	}

	// 외부에서 “보스가 소환되었다” 신호를 보낼 때 호출
	public void SignalBossSpawned()
	{
		StopPatterns(); // 중복 방지
		StartCoroutine(CoStartAfterSpawnDelay());
	}

	IEnumerator CoStartAfterSpawnDelay()
	{
		if (startDelayAfterSpawn > 0f)
			yield return new WaitForSeconds(startDelayAfterSpawn);

		StartPatterns();
	}

	// 공개 제어
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
	IEnumerator MainLoop()
	{
		while (!_stop)
		{
			if (GetHpPercent() <= 0f) yield break;

			// 1) 현재 HP%로 발동 가능한 고정 세트 탐색/진행
			if (_activeFixed == null || _activeFixed.consumed || _activeFixed.used >= _activeFixed.useCount)
			{
				_activeFixed = null; // 현재 세트 종료

				// 아직 소비되지 않았고, 체력 조건을 만족하는 첫 세트 선택
				float hp = GetHpPercent();
				foreach (var s in fixedPatternSets)
				{
					if (s.consumed) continue;
					if (hp <= s.triggerHpPercent)
					{
						// 진행 시작
						_activeFixed = s;
						_activeFixed.used = 0;
						break;
					}
				}
			}

			if (_activeFixed != null)
			{
				// 고정 세트 1회 실행
				if (_activeFixed.patterns != null && _activeFixed.patterns.Count > 0)
				{
					DBG($"[Pattern] FIXED_POOL hp%={GetHpPercent():F1}, candidates={_activeFixed.patterns.Count}");

					int pick = _rng.Next(_activeFixed.patterns.Count);                 // 0 <= pick < count

					// ★ 세트별로 '직전과 다른 것'을 강제
					if (_activeFixed.patterns.Count > 1 &&
						_lastFixedIdx.TryGetValue(_activeFixed, out int last) &&
						pick == last)
					{
						pick = (pick + 1) % _activeFixed.patterns.Count;
					}
					_lastFixedIdx[_activeFixed] = pick;                                 // 상태 갱신
					DBG($"[Pattern] PICK_FIXED idx={pick} / count={_activeFixed.patterns.Count}");

					var p = _activeFixed.patterns[pick];
					yield return ExecutePattern(p);
				}
				else
				{
					// 패턴이 비었다면 즉시 종료하고 랜덤으로 전환
					_activeFixed.consumed = true;
				}

				// 다음 루프
				continue;
			}

			// 2) 랜덤 패턴 풀 구성 → 실행
			var pool = BuildPool(GetHpPercent());
			if (pool.Count == 0)
			{
				yield return new WaitForSeconds(emptyFallbackWait);
				continue;
			}

			DBG($"[Pattern] RANDOM_POOL hp%={GetHpPercent():F1}, candidates={pool.Count}");

			// 독립 난수로 인덱스 뽑기
			int rIdx = _rng.Next(pool.Count);                                  // 0 <= rIdx < pool.Count

			// ★ 직전과 동일하면 다음 것으로 강제 변경(풀 크기 2 이상일 때만)
			if (pool.Count > 1 && rIdx == _lastRandomIdx)
				rIdx = (rIdx + 1) % pool.Count;

			_lastRandomIdx = rIdx;                                             // 상태 갱신
			DBG($"[Pattern] PICK_RANDOM idx={rIdx} / count={pool.Count}");

			var rp = pool[rIdx];
			yield return ExecutePattern(rp);

		}
	}

	List<BossPattern> BuildPool(float hpPercent)
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
	IEnumerator ExecutePattern(BossPattern pattern)
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

		// 2) 경고 프리팹 소환 + SFX
		if (pattern.warningPrefab != null)
		{
			// ★ 경고 각도 계산
			float warnZ = warnRot.eulerAngles.z; // 기본: 계산된 warnRot 사용
			switch (pattern.warnRotationSource)
			{
				case BossPattern.RotationSource.PrefabDefault:
					// 프리팹 기본 회전 유지
					warnZ = 0f; // Instantiate 회전으로만 결정되게 둘 수도 있으나, 아래에서 보정하므로 그대로 둠
					break;
				case BossPattern.RotationSource.SpawnPoint:
					warnZ = (pattern.spawnPoint ? pattern.spawnPoint.rotation.eulerAngles.z : transform.rotation.eulerAngles.z);
					break;
				case BossPattern.RotationSource.WarningPoint:
					warnZ = warnRot.eulerAngles.z; // 위에서 구한 경고 지점 회전
					break;
				case BossPattern.RotationSource.LookTarget:
					{
						Transform t = pattern.warnLookTarget ? pattern.warnLookTarget : FindPlayerTransform(); // 대상 자동 탐색
						if (t)
						{
							Vector2 dir = (Vector2)(t.position - warnPos);
							if (dir.sqrMagnitude > 1e-6f) warnZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // 오른쪽 기준
						}
						break;
					}
				case BossPattern.RotationSource.CustomAngle:
					warnZ = pattern.warnCustomAngleZ;
					break;
			}
			warnZ += pattern.warnAngleOffsetZ; // 오프셋 보정

			// 경고 프리팹 생성(초기 회전 적용)
			var warn = Instantiate(pattern.warningPrefab, warnPos, Quaternion.Euler(0, 0, warnZ));

			// 경고 수명 지정
			if (pattern.warningDuration > 0f) Destroy(warn, pattern.warningDuration);

			// ★ 생성 직후 Transform + Rigidbody2D 회전 강제(내부 스크립트가 Start에서 덮어쓸 대비)
			if (pattern.warnApplyRotation) ApplyZRotation(warn, warnZ, pattern.warnAlsoSetRigidbody2D);

			// ★ 추적 회전(옵션): 경고가 살아있는 동안 LookTarget을 따라간다
			if (pattern.warnApplyRotation && pattern.warnTrackTarget && pattern.warnRotationSource == BossPattern.RotationSource.LookTarget)
			{
				Transform t = pattern.warnLookTarget ? pattern.warnLookTarget : FindPlayerTransform();
				if (t) StartCoroutine(CoTrackWarnRotation(warn.transform, t, pattern.warnAngleOffsetZ, pattern.warnAlsoSetRigidbody2D));
			}

			DBG($"[Pattern] WARN at {warnPos} rotZ={warnZ:F1}");
			FireWarningFx(pattern); // 경고 연출
		}


		// 3) 스폰 좌표 스냅샷
		if (pattern.spawnAtWarning)
		{
			pattern.cachedSpawnPos = warnPos + pattern.spawnOffset;
			pattern.cachedSpawnRot = warnRot;
			pattern.hasCachedSpawn = true;
		}
		else if (pattern.snapshotSpawnPointOnWarning)
		{
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

		// 4) 경고 후 추가 지연
		if (pattern.attackDelayAfterWarning > 0f)
			yield return new WaitForSeconds(pattern.attackDelayAfterWarning);

		// 5) 낚시 경고 처리
		if (pattern.fakeWarningChance > 0f && URandom.value < pattern.fakeWarningChance)
			yield break;

		// 6) 공격 프리팹 생성 + 수명 + 공격 SFX
		if (pattern.prefab != null)
		{
			Vector3 pos; Quaternion rot;
			if (pattern.hasCachedSpawn)
			{ pos = pattern.cachedSpawnPos; rot = pattern.cachedSpawnRot; }
			else if (pattern.spawnPoint != null && pattern.spawnPoint.gameObject.scene.IsValid())
			{ pos = pattern.spawnPoint.position + pattern.spawnOffset; rot = pattern.spawnPoint.rotation; }
			else
			{ pos = transform.position + pattern.spawnOffset; rot = transform.rotation; }

			if (!pattern.inheritRotation) rot = Quaternion.identity;
			// ★ 최종 Z각도 계산
			float finalZ = rot.eulerAngles.z; // 기본은 스폰/경고의 회전
			switch (pattern.rotationSource)
			{
				case BossPattern.RotationSource.PrefabDefault:
					// 프리팹 기본 회전 유지 → finalZ 그대로
					break;

				case BossPattern.RotationSource.SpawnPoint:
					// 이미 rot가 spawnPoint(또는 보스)의 회전을 담고 있으니 그대로
					break;

				case BossPattern.RotationSource.WarningPoint:
					// 경고 좌표를 바라보는 느낌이 필요하면 warnRot 사용
					finalZ = warnRot.eulerAngles.z;
					break;

				case BossPattern.RotationSource.LookTarget:
					{
						// 대상 찾기: 지정 없으면 Tag==Player 1개 자동 탐색
						Transform t = pattern.lookTarget ? pattern.lookTarget : FindPlayerTransform();
						if (t)
						{
							// 2D 기준: +X 방향이 우측이라면, 우측을 기준으로 각도 계산
							Vector2 dir = (Vector2)(t.position - pos);
							if (dir.sqrMagnitude > 0.0001f)
								finalZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
						}
					}
					break;

				case BossPattern.RotationSource.CustomAngle:
					finalZ = pattern.customAngleZ;
					break;
			}

			// 오프셋 추가
			finalZ += pattern.angleOffsetZ;

			var go = Instantiate(pattern.prefab, pos, rot); // 공격 생성
			// 회전 강제 적용(Transform + Rigidbody2D)
			if (pattern.applyRotation)
				ApplyZRotation(go, finalZ, pattern.alsoSetRigidbody2D);

			if (pattern.attackPrefabLifetime > 0f)
				Destroy(go, pattern.attackPrefabLifetime);

			if (pattern.attackSfx != null)
			{
				var s = new GameObject("SFX_Attack_2D");
				var a = s.AddComponent<AudioSource>();
				a.clip = pattern.attackSfx;
				a.spatialBlend = 0f; // 2D
				a.volume = Mathf.Clamp01(pattern.attackSfxVolume);
				a.Play();
				Destroy(s, pattern.attackSfx.length + 0.05f);
			}

			// ★ 공격 직후 연출 발동
			FireAttackFx(pattern);
		}

		// 7) n초 후 천천히 이동
		if (pattern.doSlowMove && pattern.slowMoveTarget != null)
		{
			if (pattern.slowMoveDelay > 0f)
				yield return new WaitForSeconds(pattern.slowMoveDelay);

			if (pattern.slowMoveDuration <= 0f)
			{
				transform.position = new Vector3(
					pattern.slowMoveTarget.position.x,
					pattern.slowMoveTarget.position.y,
					transform.position.z
				);
			}
			else
			{
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
		// 선형보간으로 대기시간 결정
		float wait = Mathf.Lerp(min, max, (float)_rng.NextDouble());
		if (wait > 0f) yield return new WaitForSeconds(wait);

		// 10) 캐시 정리
		pattern.hasCachedSpawn = false;
	}

	// ─────────────────────────────────────────────────────────────
	// [추가] 연출 유틸
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// 배경 트윈 1개를 보간 적용.
	/// BackgroundUVScroller의 런타임 안전 필드(uvSpeed/rotationSpeed)만 변경한다.
	/// </summary>
	IEnumerator CoApplyBg(Tisiphone_BgTween t)
	{
		if (!t.target) yield break;

		// 원래 값 백업
		Vector2 fromSpeed = t.target.uvSpeed;
		float fromRot = t.target.rotationSpeed;

		// 전진 보간
		float dur = Mathf.Max(0f, t.lerpTime);
		if (dur <= 0f)
		{
			t.target.uvSpeed = t.uvSpeed;
			t.target.rotationSpeed = t.rotationSpeed;
		}
		else
		{
			float e = 0f;
			while (e < dur)
			{
				e += Time.unscaledDeltaTime; // 타임스케일 무시
				float k = Mathf.Clamp01(e / dur);
				if (t.ease != null && t.ease.length > 0) k = t.ease.Evaluate(k);

				t.target.uvSpeed = Vector2.LerpUnclamped(fromSpeed, t.uvSpeed, k);
				t.target.rotationSpeed = Mathf.LerpUnclamped(fromRot, t.rotationSpeed, k);
				yield return null;
			}
			t.target.uvSpeed = t.uvSpeed;
			t.target.rotationSpeed = t.rotationSpeed;
		}

		// 유지
		if (t.holdTime > 0f) yield return new WaitForSecondsRealtime(t.holdTime);

		// 복귀
		if (t.revert)
		{
			float rdur = Mathf.Max(0f, t.lerpTime);
			if (rdur <= 0f)
			{
				t.target.uvSpeed = fromSpeed;
				t.target.rotationSpeed = fromRot;
			}
			else
			{
				float e = 0f;
				while (e < rdur)
				{
					e += Time.unscaledDeltaTime;
					float k = Mathf.Clamp01(e / rdur);
					if (t.ease != null && t.ease.length > 0) k = t.ease.Evaluate(k);

					t.target.uvSpeed = Vector2.LerpUnclamped(t.uvSpeed, fromSpeed, k);
					t.target.rotationSpeed = Mathf.LerpUnclamped(t.rotationSpeed, fromRot, k);
					yield return null;
				}
				t.target.uvSpeed = fromSpeed;
				t.target.rotationSpeed = fromRot;
			}
		}
	}

	/// <summary>카메라 자동 탐색. 지정값이 비어있다면 첫 번째 CameraEffects를 찾는다.</summary>
	CameraEffects GetCam(CameraEffects prefer)
	{
		if (prefer) return prefer;
#if UNITY_2023_1_OR_NEWER
		return UObject.FindFirstObjectByType<CameraEffects>(FindObjectsInactive.Include);
#else
#pragma warning disable CS0618
    return UObject.FindObjectOfType<CameraEffects>();
#pragma warning restore CS0618
#endif
	}

	/// <summary>카메라 FX 실행 및 자동 리셋 예약</summary>
	void PlayCamFx(Tisiphone_CamFx fx)
	{
		var cam = GetCam(fx.cam);
		if (!cam) return;

		if (fx.shake) cam.Shake(fx.shakeDuration, fx.shakeAmplitude, fx.shakeFrequency);
		if (fx.zoom) cam.ZoomTo(fx.zoomSize, fx.zoomTime);
		if (fx.rotate) cam.RotateTo(fx.rotateZ, fx.rotateTime);

		if (fx.autoReset)
			StartCoroutine(CoCamAutoReset(cam, Mathf.Max(0f, fx.autoResetDelay), Mathf.Max(0f, fx.resetEaseTime)));
	}

	/// <summary>CameraEffects.ResetAll 예약</summary>
	IEnumerator CoCamAutoReset(CameraEffects cam, float delay, float ease)
	{
		if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
		cam.ResetAll(ease);
	}
	void FireFx(Tisiphone_BgTween[] bg, Tisiphone_CamFx[] cam)
	{
		// 배경
		if (bg != null)
			foreach (var t in bg)
				StartCoroutine(CoApplyBg(t)); // // 배경 uvSpeed/rotationSpeed만 보간

		// 카메라
		if (cam != null)
			foreach (var c in cam)
				PlayCamFx(c); // // CameraEffects.Shake/ZoomTo/RotateTo 호출
	}
	// 경고 타이밍: 패턴 전용 > 전역 순서로 선택
	void FireWarningFx(BossPattern p)
	{
		var bg = (p.warnBackgrounds != null && p.warnBackgrounds.Length > 0)
					? p.warnBackgrounds : onWarningBackgrounds;
		var cam = (p.warnCameras != null && p.warnCameras.Length > 0)
					? p.warnCameras : onWarningCameras;
		FireFx(bg, cam);
	}

	// 공격 타이밍: 패턴 전용 > 전역 순서로 선택
	void FireAttackFx(BossPattern p)
	{
		var bg = (p.attackBackgrounds != null && p.attackBackgrounds.Length > 0)
					? p.attackBackgrounds : onAttackBackgrounds;
		var cam = (p.attackCameras != null && p.attackCameras.Length > 0)
					? p.attackCameras : onAttackCameras;
		FireFx(bg, cam);
	}
	// ▣ Rigidbody2D까지 함께 Z회전 적용
	private void ApplyZRotation(GameObject go, float zDeg, bool alsoRigid2D)
	{
		// Transform 회전
		go.transform.rotation = Quaternion.Euler(0f, 0f, zDeg);

		// Rigidbody2D가 있으면 물리 회전도 적용
		if (alsoRigid2D)
		{
			var rb = go.GetComponent<Rigidbody2D>();
			if (rb)
			{
				// FreezeRotation(Z)가 켜져 있으면 꺼서 회전 허용
				if ((rb.constraints & RigidbodyConstraints2D.FreezeRotation) != 0)
					rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;

				rb.SetRotation(zDeg);   // 물리 회전 각도 지정
				rb.angularVelocity = 0; // 잔여 각속도 제거
			}
		}
	}
	// ▣ 경고 프리팹이 살아있는 동안 대상 바라보게 계속 회전
	private IEnumerator CoTrackWarnRotation(Transform tf, Transform target, float offsetZ, bool alsoRigid2D)
	{
		if (!tf || !target) yield break;

		// Rigidbody2D 캐시(있을 때만)
		var rb = tf.GetComponent<Rigidbody2D>();

		while (tf && target)
		{
			Vector2 dir = (Vector2)(target.position - tf.position);
			if (dir.sqrMagnitude > 1e-6f)
			{
				float z = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + offsetZ; // 오른쪽이 정면인 스프라이트 기준
				tf.rotation = Quaternion.Euler(0f, 0f, z);

				if (alsoRigid2D && rb)
				{
					// 매 프레임 물리 회전도 동기화
					rb.SetRotation(z);
					rb.angularVelocity = 0;
				}
			}
			yield return null; // 다음 프레임
		}
	}
	// ▣ Player 자동 탐색(없으면 null)
	private Transform FindPlayerTransform()
	{
		var p = GameObject.FindGameObjectWithTag("Player");
		return p ? p.transform : null;
	}

	// ─────────────────────────────────────────────────────────────
	// 유틸
	// ─────────────────────────────────────────────────────────────
	float GetHpPercent()
	{
		// 보스 연동 시 실시간 값 사용
		if (!useTestHp && bossBase != null)
		{
			float max = Mathf.Max(1, bossBase.maxHP);
			return Mathf.Clamp01((float)bossBase.CurrentHP / max) * 100f;
		}

		// 그 외 테스트 값
		if (testMaxHp <= 0f) return 0f;
		return Mathf.Clamp01(testCurrentHp / testMaxHp) * 100f;
	}



	void OnValidate()
	{
		testMaxHp = Mathf.Max(1f, testMaxHp);
		testCurrentHp = Mathf.Clamp(testCurrentHp, 0f, testMaxHp);
	}
}
