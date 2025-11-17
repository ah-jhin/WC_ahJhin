using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아스트라 보스 패턴 총괄 컨트롤러
/// - BossBase, CameraController를 사용하여 페이지/패턴을 관리한다.
/// - 1페이지: 구체 패턴 3종
/// - 2페이지: 레이저 1 / 레이저 2 / 구체 패턴(2-2, 2-3)
/// - 3페이지: 마지막 경로 구체 + 카메라 회전 루프
/// - 각 패턴 종료 후: 보스 위치/카메라 원복 → patternInterval 만큼 대기 후 다음 패턴
/// </summary>
[RequireComponent(typeof(BossBase))]
public class AstraBossController : MonoBehaviour
{
	// ================================================
	#region 공통 레퍼런스 / 기본 설정
	// ================================================

	[Header("공통 레퍼런스")]
	[Tooltip("보스 공통 베이스 스크립트. HP, 보스바, 이벤트 등을 관리하는 컴포넌트.")]
	[SerializeField] private BossBase bossBase;

	[Tooltip("플레이어 Transform (예: PlayerMovement / PlayerHealth가 붙어있는 오브젝트).")]
	[SerializeField] private Transform player;

	[Tooltip("카메라 연출용 컨트롤러. 비워두면 CameraController.Instance를 자동 사용.")]
	[SerializeField] private CameraController cameraController;

	[Tooltip("보스의 홈 포지션. 비워두면 현재 위치를 기준으로 홈 Transform을 자동 생성한다.")]
	[SerializeField] private Transform homePosition;

	[Tooltip("보스가 목표 위치에 도달했다고 판단할 최소 거리(유닛). 너무 작으면 멈추지 못할 수 있다.")]
	[SerializeField] private float bossStopDistance = 0.05f;

	[Tooltip("보스가 이동할 때 사용하는 속도(유닛/초).")]
	[SerializeField] private float bossMoveSpeed = 8f;

	[Tooltip("보스가 소환된 이후, 실제 패턴이 시작되기까지의 대기 시간(초). 인트로 연출과 맞추면 된다.")]
	[SerializeField] private float startDelayAfterSpawn = 2.0f;

	[Header("패턴 공통 설정")]
	[Tooltip("하나의 패턴이 끝난 후, 홈/카메라를 원상복구하고 다음 패턴으로 넘어가기 전까지 기다리는 시간(초). 기본 3초.")]
	[SerializeField] private float patternInterval = 3f;

	// 내부: 실제 보스 외형(Actor) Transform
	private Transform actor;

	// 내부: 현재 페이지 (1,2,3)
	private int currentPage = 1;

	// 내부: 메인 루틴 코루틴 핸들
	private Coroutine mainRoutine;

	#endregion

	// ================================================
	#region 카메라 프리셋 + 회전 SFX
	// ================================================

	/// <summary>
	/// 페이지/패턴에서 사용할 카메라 상태 프리셋
	/// </summary>
	[System.Serializable]
	public class AstraCameraPreset
	{
		[Tooltip("카메라 Z축 회전 각도(도). 양수: 반시계, 음수: 시계 방향.")]
		public float targetAngle = 0f;

		[Tooltip("이 각도로 회전하는 데 걸리는 시간(초). 0이면 거의 즉시 회전.")]
		public float rotateTime = 0.5f;

		[Tooltip("카메라 오소그래픽 사이즈(줌). 0 이하이면 기존 값을 유지한다.")]
		public float zoomSize = 0f;

		[Tooltip("줌 변경에 걸리는 시간(초). 0이면 거의 즉시 변경.")]
		public float zoomTime = 0.5f;

		[Tooltip("회전 직후 화면 흔들림을 줄지 여부.")]
		public bool useShake = true;

		[Tooltip("흔들림 지속 시간(초).")]
		public float shakeDuration = 0.2f;

		[Tooltip("흔들림 강도.")]
		public float shakeMagnitude = 0.25f;

		[Tooltip("흔들림 주기(Hz). 값이 클수록 더 빠르게 떤다.")]
		public float shakeFrequency = 25f;
	}

	[Header("페이지별 카메라 프리셋")]
	[Tooltip("1페이지에서 랜덤으로 사용할 카메라 회전/줌 프리셋 목록.")]
	[SerializeField] private AstraCameraPreset[] page1CameraPresets;

	[Tooltip("2페이지에서 랜덤으로 사용할 카메라 회전/줌 프리셋 목록.")]
	[SerializeField] private AstraCameraPreset[] page2CameraPresets;

	[Tooltip("3페이지(마지막)에서 랜덤으로 사용할 카메라 회전/줌 프리셋 목록.")]
	[SerializeField] private AstraCameraPreset[] page3CameraPresets;

	[Header("카메라 회전 SFX")]
	[Tooltip("카메라 회전 시작 시 재생할 공통 SFX.")]
	[SerializeField] private AudioClip cameraRotateSFX;

	[Tooltip("카메라 회전 SFX를 재생할 AudioSource. 비워두면 PlayClipAtPoint 사용.")]
	[SerializeField] private AudioSource cameraSfxSource;

	#endregion

	// ================================================
	#region 보스 이동 위치 / 스폰 그룹
	// ================================================

	[Header("보스 이동 위치")]
	[Tooltip("보스가 랜덤으로 이동할 수 있는 위치 목록. 직접 넣거나, 아래 그룹 부모에서 자동 생성할 수 있다.")]
	[SerializeField] private Transform[] randomMovePoints;

	[Tooltip("스폰 포인트 그룹 부모들 (예: Right / Left / Center). 각 자식 Transform을 모두 randomMovePoints에 자동 등록한다.")]
	[SerializeField] private Transform[] randomMoveGroupParents;
	[Header("보스 이동 후 대기 시간")]
	[Tooltip("보스가 패턴 중 위치를 이동을 마친 뒤, 실제 공격을 시작하기까지 기다릴 시간(초).")]
	[SerializeField] private float delayAfterMove = 1f;

	#endregion

	// ================================================
	#region 패턴용 프리팹 / 설정 (구체/레이저)
	// ================================================

	[Header("공통 구체 / 레이저 프리팹")]
	[Tooltip("공통 구체(탄막) 프리팹. AstraOrbProjectile이 붙어 있어야 한다.")]
	[SerializeField] private AstraOrbProjectile orbPrefab;

	[Tooltip("공통 레이저 프리팹. AstraLaserHazard가 붙어 있어야 한다.")]
	[SerializeField] private AstraLaserHazard laserPrefab;

	[Header("1페이지: 패턴별 구체 프리팹/속도")]
	[Tooltip("1-1 패턴에서 사용할 구체 프리팹. 비워두면 공통 orbPrefab 사용.")]
	[SerializeField] private AstraOrbProjectile p1_11_orbPrefab;
	[Tooltip("1-1 패턴 구체 이동 속도(유닛/초). 0 이하면 프리팹 기본 속도 사용.")]
	[SerializeField] private float p1_11_orbSpeed = 6f;

	[Tooltip("1-2 패턴에서 사용할 구체 프리팹.")]
	[SerializeField] private AstraOrbProjectile p1_12_orbPrefab;
	[Tooltip("1-2 패턴 구체 이동 속도(유닛/초).")]
	[SerializeField] private float p1_12_orbSpeed = 6f;

	[Tooltip("1-3 패턴에서 사용할 구체 프리팹.")]
	[SerializeField] private AstraOrbProjectile p1_13_orbPrefab;
	[Tooltip("1-3 패턴 구체 이동 속도(유닛/초).")]
	[SerializeField] private float p1_13_orbSpeed = 6f;

	[Header("2페이지: 구체 패턴 프리팹/속도")]
	[Tooltip("2-2 패턴(낮은 명중률 유도탄)에 사용할 구체 프리팹.")]
	[SerializeField] private AstraOrbProjectile p2_22_orbPrefab;
	[Tooltip("2-2 패턴 구체 이동 속도(유닛/초).")]
	[SerializeField] private float p2_22_orbSpeed = 7f;

	[Tooltip("2-3 패턴(사방 10발)에 사용할 구체 프리팹.")]
	[SerializeField] private AstraOrbProjectile p2_23_orbPrefab;
	[Tooltip("2-3 패턴 구체 이동 속도(유닛/초).")]
	[SerializeField] private float p2_23_orbSpeed = 6f;

	[Header("구체 배치 패턴 SFX(일괄 생성용)")]
	[Tooltip("1-2 패턴(5발 사방 발사)에서 여러 구체 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p1_12_orbBatchSfx;

	[Tooltip("2-3 패턴(사방 10발)에서 여러 구체 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p2_23_orbBatchSfx;

	[Tooltip("3페이지 원형 배치 패턴에서 여러 구체 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p3_orbBatchSfx;

	[Header("1페이지: 반복/시간 설정")]
	[Tooltip("1-1 패턴 반복 횟수 (플레이어 추적 구체 1개).")]
	[SerializeField] private int p1_pattern11Repeat = 5;

	[Tooltip("1-2 패턴 반복 횟수 (보스 주변 5발 사방 발사).")]
	[SerializeField] private int p1_pattern12Repeat = 4;

	[Tooltip("1-3 패턴 반복 횟수 (플레이어 아래에서 위로 올라오는 구체).")]
	[SerializeField] private int p1_pattern13Repeat = 7;

	[Tooltip("1-1 패턴: 구체 생성 후 플레이어 방향으로 움직이기까지의 대기 시간(초).")]
	[SerializeField] private float p1_11_orbDelayToChase = 1.0f;

	[Tooltip("1-1 패턴: 추적이 시작된 후 보스가 새 위치로 이동하기까지의 대기 시간(초).")]
	[SerializeField] private float p1_11_delayBeforeMoveBoss = 1.0f;

	[Tooltip("1-2 패턴: 5개 구체 생성 후 사방으로 퍼지기까지의 대기 시간(초).")]
	[SerializeField] private float p1_12_orbDelayToSpread = 1.5f;

	[Tooltip("1-2 패턴: 퍼진 뒤 보스가 이동하기까지의 대기 시간(초).")]
	[SerializeField] private float p1_12_delayBeforeMoveBoss = 1.0f;

	[Tooltip("1-3 패턴: 플레이어 아래에서 생성된 구체가 위로 올라오기까지의 대기 시간(초).")]
	[SerializeField] private float p1_13_orbDelayToRise = 0.5f;

	[Header("2페이지: 유도 구체 패턴 설정(2-2)")]

	[Tooltip("2-2 패턴에서 구체를 생성하는 간격(초). 0이면 즉시 다음 사이클로 넘어간다.")]
	[SerializeField] private float p2_22_spawnInterval = 0.6f;

	[Tooltip("2-2 패턴에서 실제로 플레이어를 맞출 확률(0~1). 0.2 = 20% 명중률.")]
	[Range(0f, 1f)]
	[SerializeField] private float p2_22_aimAccuracy = 0.2f;

	[Tooltip("2-2 패턴에서 한 번에 생성할 작은 구체의 개수. (기본값 5)")]
	[SerializeField] private int p2_22_orbsPerSpawn = 5;   // ★ 추가

	[Tooltip("2-2 패턴에서 구체 생성 사이클(파동)의 횟수. (기본값 5회)")]
	[SerializeField] private int p2_22_spawnCount = 5;      // ★ 추가

	[Header("2페이지: 레이저 1 (고정 위치 레이저)")]
	[Tooltip("레이저 1 패턴에서 사용할 위치 그룹 부모들.\n각 그룹의 자식 Transform 들이 실제 레이저 위치가 되며,\n패턴 실행 시 그룹들 중 하나를 랜덤으로 선택하여 사용한다.")]
	[SerializeField] private Transform[] p2_laser1GroupParents;

	[Tooltip("레이저 1 패턴에서 사용할 경고 프리팹.")]
	[SerializeField] private GameObject p2_laser1WarningPrefab;

	[Tooltip("레이저 1 패턴에서 경고 프리팹 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p2_laser1WarningSfx;

	[Tooltip("레이저 1 패턴에서 레이저 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p2_laser1FireSfx;

	[Tooltip("레이저 1 패턴 반복 횟수. 기본 5회.")]
	[SerializeField] private int p2_laser1RepeatCount = 5;

	[Tooltip("레이저 1 경고 표기 시간(초). 기본 0.6초.")]
	[SerializeField] private float p2_laser1WarningDuration = 0.6f;

	[Tooltip("레이저 1 한 사이클 후 다음 사이클까지 기다리는 시간(초). 기본 0.8초.")]
	[SerializeField] private float p2_laser1Interval = 0.8f;

	[Tooltip("레이저 1에서 레이저가 이동할 방향. 고정 레이저면 (0,0) 또는 속도 0 사용.")]
	[SerializeField] private Vector2 p2_laser1Direction = Vector2.up;

	[Tooltip("레이저 1 레이저 이동 속도(유닛/초). 0이면 이동하지 않는 고정 레이저.")]
	[SerializeField] private float p2_laser1Speed = 0f;

	[Header("2페이지: 레이저 2 (플레이어 추적 레이저)")]
	[Tooltip("레이저 2 패턴에서 사용할 경고 프리팹.")]
	[SerializeField] private GameObject p2_laser2WarningPrefab;

	[Tooltip("레이저 2 패턴에서 경고 프리팹 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p2_laser2WarningSfx;

	[Tooltip("레이저 2 패턴에서 레이저 생성 시 재생할 SFX.")]
	[SerializeField] private AudioClip p2_laser2FireSfx;

	[Tooltip("레이저 2 패턴 반복 횟수. 기본 2회.")]
	[SerializeField] private int p2_laser2RepeatCount = 2;

	[Tooltip("레이저 2에서 경고가 표시되는 시간(초). 기본 0.7초.")]
	[SerializeField] private float p2_laser2WarningDuration = 0.7f;

	[Tooltip("레이저 2에서 레이저가 플레이어를 추적하는 시간(초). 기본 1초.")]
	[SerializeField] private float p2_laser2FollowDuration = 1f;

	[Tooltip("레이저 2에서 레이저 이동 속도(유닛/초). 기본 5.")]
	[SerializeField] private float p2_laser2Speed = 5f;

	[Header("3페이지: 랜덤 유도 구체 설정")]
	[Tooltip("3페이지 시작 시 보스가 이동할 고정 위치. 비워두면 현재 위치를 사용한다.")]
	[SerializeField] private Transform page3FixedPosition;

	[Tooltip("3페이지에서 구체를 생성할 위치들. 여기 등록된 Transform 중 하나가 매번 랜덤으로 선택된다.")]
	[SerializeField] private Transform[] p3_spawnPoints;

	[Tooltip("3페이지에서 사용할 구체 프리팹. 비워두면 공통 orbPrefab을 사용한다.")]
	[SerializeField] private AstraOrbProjectile p3_orbPrefab;

	[Tooltip("3페이지 유도 구체의 이동 속도(유닛/초).")]
	[SerializeField] private float p3_orbSpeed = 6f;

	[Tooltip("구체가 생성된 후, 실제로 움직이기까지의 대기 시간(초). 요구사항 기준 1초.")]
	[SerializeField] private float p3_orbDelayToChase = 1f;

	[Tooltip("플레이어를 정확히 겨냥할 확률(0~1). 0.2 = 20% 명중률.")]
	[Range(0f, 1f)]
	[SerializeField] private float p3_orbAimAccuracy = 0.2f;

	[Tooltip("구체 사격 사이의 간격(초). 0.5~1.0 정도로 조절할 수 있다.")]
	[SerializeField] private float p3_spawnInterval = 0.7f;

	[Tooltip("3페이지 패턴 루프 사이의 추가 대기 시간(초). 필요 없으면 0.")]
	[SerializeField] private float p3_patternLoopDelay = 0.0f;
	[Header("3페이지: 카메라 회전 설정")]
	[Tooltip("3페이지에서 카메라가 Z축으로 회전하는 속도(도/초).\n양수면 반시계, 음수면 시계 방향. 0이면 회전하지 않는다.")]
	[SerializeField] private float p3_cameraRotateSpeed = 30f;


	[Header("공통 패턴 SFX 관리")]
	[Tooltip("패턴 SFX 재생용 AudioSource. 비워두면 PlayClipAtPoint 사용.")]
	[SerializeField] private AudioSource patternSfxSource;

	[Tooltip("같은 패턴 SFX가 너무 짧은 시간에 여러 번 울리는 것을 막기 위한 최소 간격(초).")]
	[SerializeField] private float patternSfxCooldown = 0.05f;

	// 내부: 마지막 패턴 SFX 재생 시각
	private float _lastPatternSfxTime = -999f;
	// 3페이지 구체 랜덤 이동용 위치 리스트 캐시
	#endregion

	// ================================================
	#region 초기화 / 페이지 계산 / 보스 사망 처리
	// ================================================

	private void Awake()
	{
		if (!bossBase) bossBase = GetComponent<BossBase>();
		if (!cameraController && CameraController.Instance != null)
			cameraController = CameraController.Instance;

		// 스폰 포인트 그룹에서 자식들을 자동 모아 randomMovePoints 구성
		BuildRandomMovePointsFromGroups();
	}

	private void Start()
	{
		if (bossBase != null)
		{
			actor = bossBase.actor;
			bossBase.OnHpChanged += OnBossHpChanged;
			bossBase.OnBossDie += OnBossDie;
		}

		mainRoutine = StartCoroutine(MainLoop());
	}

	/// <summary>
	/// HP 변화 시 현재 페이지(1/2/3)를 계산한다.
	/// </summary>
	private void OnBossHpChanged(int current, int max)
	{
		float hpPercent = (max > 0) ? (current / (float)max) : 0f;

		if (hpPercent > 0.66f) currentPage = 1;
		else if (hpPercent > 0.33f) currentPage = 2;
		else currentPage = 3;
	}

	/// <summary>
	/// 보스 사망 시 호출. 카메라 상태를 복구하고 메인 루틴을 종료한다.
	/// </summary>
	private void OnBossDie(BossBase _)
	{
		if (cameraController != null)
			cameraController.ResetAll(true, true, false); // 회전/줌 복구

		if (mainRoutine != null)
			StopCoroutine(mainRoutine);
	}

	#endregion

	// ================================================
	#region 메인 루프
	// ================================================

	private IEnumerator MainLoop()
	{
		// BossBase와 actor 준비될 때까지 대기
		while (bossBase == null || bossBase.actor == null)
			yield return null;

		actor = bossBase.actor;

		// 홈 포지션이 없으면 현재 위치 기준으로 자동 생성
		if (homePosition == null)
		{
			GameObject home = new GameObject("Astra_HomePosition");
			home.transform.position = actor.position;
			homePosition = home.transform;
		}

		// 소환 후 시작 대기
		if (startDelayAfterSpawn > 0f)
			yield return new WaitForSeconds(startDelayAfterSpawn);

		// 보스가 죽을 때까지 페이지별 패턴 반복
		while (bossBase != null && !bossBase.IsDead)
		{
			if (!player)
			{
				yield return null;
				continue;
			}

			if (currentPage == 1)
			{
				yield return StartCoroutine(RunPage1Pattern());
			}
			else if (currentPage == 2)
			{
				yield return StartCoroutine(RunPage2Pattern());
			}
			else // 3페이지
			{
				yield return StartCoroutine(RunPage3Loop());
				yield break;    // 3페이지는 내부 루프가 끝까지 돌도록 한 뒤 종료
			}

			// 패턴 종료 → 보스/카메라 원복
			yield return StartCoroutine(ResetBossAndCamera());

			// 다음 패턴 시작 전 대기
			if (patternInterval > 0f)
				yield return new WaitForSeconds(patternInterval);
		}
	}

	#endregion

	// ================================================
	#region 페이지 1 패턴 (1-1, 1-2, 1-3)
	// ================================================

	private IEnumerator RunPage1Pattern()
	{
		ApplyRandomCameraPreset(page1CameraPresets, allowShake: true);

		// 카메라 회전 후, 보스를 먼저 이동시키고 1초 대기
		yield return MoveBossToRandomPoint();

		int choice = Random.Range(0, 3); // 0~2
		switch (choice)
		{
			case 0:
				yield return StartCoroutine(Pattern_1_1());
				break;
			case 1:
				yield return StartCoroutine(Pattern_1_2());
				break;
			default:
				yield return StartCoroutine(Pattern_1_3());
				break;
		}
	}

	/// <summary>
	/// 1-1: 보스 위치에서 구체 1개 생성 → 일정 시간 후 플레이어 추적 → 보스 랜덤 이동
	/// </summary>
	private IEnumerator Pattern_1_1()
	{
		for (int i = 0; i < p1_pattern11Repeat; i++)
		{
			if (bossBase.IsDead || !player) yield break;

			var orb = SpawnOrbAt(actor.position, p1_11_orbPrefab, p1_11_orbSpeed);
			if (orb)
				orb.SetupHomingOnce(player, p1_11_orbDelayToChase);

			if (p1_11_orbDelayToChase > 0f)
				yield return new WaitForSeconds(p1_11_orbDelayToChase);

			if (p1_11_delayBeforeMoveBoss > 0f)
				yield return new WaitForSeconds(p1_11_delayBeforeMoveBoss);

			yield return MoveBossToRandomPoint();
		}
	}

	/// <summary>
	/// 1-2: 보스 위치에서 구체 5개를 생성 → 일정 시간 후 360도 방향으로 퍼뜨린다.
	/// </summary>
	private IEnumerator Pattern_1_2()
	{
		for (int i = 0; i < p1_pattern12Repeat; i++)
		{
			if (bossBase.IsDead || !player) yield break;

			// 다수 구체 생성 SFX: 한 번만 재생
			PlayPatternSfx(p1_12_orbBatchSfx);

			List<AstraOrbProjectile> orbs = new List<AstraOrbProjectile>();
			int count = 7;
			float angleStep = 360f / count;

			// 구체 5개 생성 (스폰 SFX는 끄고, 여기서 일괄 SFX만 사용)
			for (int n = 0; n < count; n++)
			{
				var orb = SpawnOrbAt(actor.position, p1_12_orbPrefab, p1_12_orbSpeed);
				if (orb)
				{
					orb.SetSpawnSfxEnabled(false);
					orbs.Add(orb);
				}
			}

			// 발사 전 연출용 대기
			if (p1_12_orbDelayToSpread > 0f)
				yield return new WaitForSeconds(p1_12_orbDelayToSpread);

			// 360도 방향으로 사방 발사
			for (int n = 0; n < orbs.Count; n++)
			{
				if (!orbs[n]) continue;
				float angle = angleStep * n;
				Vector2 dir = DegreeToDir(angle);
				orbs[n].SetupStraight(dir);
			}

			if (p1_12_delayBeforeMoveBoss > 0f)
				yield return new WaitForSeconds(p1_12_delayBeforeMoveBoss);

			yield return MoveBossToRandomPoint();
		}
	}

	/// <summary>
	/// 1-3: 플레이어 아래에 구체 생성 → 일정 시간 후 위쪽으로 상승
	/// </summary>
	private IEnumerator Pattern_1_3()
	{
		for (int i = 0; i < p1_pattern13Repeat; i++)
		{
			if (bossBase.IsDead || !player) yield break;

			Vector3 spawnPos = player.position + new Vector3(0f, -15f, 0f);
			var orb = SpawnOrbAt(spawnPos, p1_13_orbPrefab, p1_13_orbSpeed);
			if (orb)
			{
				orb.SetupStraight(Vector2.up);
				orb.SetStartMoveDelay(p1_13_orbDelayToRise);
			}

			if (p1_13_orbDelayToRise > 0f)
				yield return new WaitForSeconds(p1_13_orbDelayToRise);
		}
	}

	#endregion

	// ================================================
	#region 페이지 2 패턴 (레이저1/2 + 구체 2-2, 2-3)
	// ================================================

	/// <summary>
	/// 2페이지 패턴
	/// - 카메라 연출 → 보스 랜덤 위치 이동(+1초 대기)
	/// - 레이저1 / 레이저2 / 유도 구체(2-2) 중 하나를 랜덤 선택해서 1회 실행
	/// </summary>
	private IEnumerator RunPage2Pattern()
	{
		// 1) 카메라 회전/줌
		ApplyRandomCameraPreset(page2CameraPresets, allowShake: true);

		// 2) 보스 이동 후 delayAfterMove(예: 1초) 만큼 대기
		yield return MoveBossToRandomPoint();

		// 3) 패턴 랜덤 선택
		int choice = Random.Range(0, 3); // 0,1,2

		switch (choice)
		{
			case 0:
				// 레이저 1
				yield return StartCoroutine(Pattern_2_Laser1());
				break;

			case 1:
				// 레이저 2
				yield return StartCoroutine(Pattern_2_Laser1());
				break;

			case 2:
				// 유도 구체 (2-2)
				yield return StartCoroutine(Pattern_2_2_HomingSmallOrbs());
				break;
		}

		// 필요하면 여기서 추가 대기도 가능:
		// if (patternInterval > 0f)
		//     yield return new WaitForSeconds(patternInterval);
	}


	/// <summary>
	/// 레이저 1
	/// - p2_laser1GroupParents 배열에 들어있는 그룹들 중 하나를 랜덤으로 선택
	/// - 선택된 그룹의 자식 위치들에 경고 프리팹 생성
	/// - p2_laser1WarningDuration 후 경고 삭제 + 같은 위치에 레이저 생성
	/// - p2_laser1Interval 동안 대기 후 다음 그룹을 다시 랜덤으로 선택
	/// - 총 p2_laser1RepeatCount 회 반복
	/// </summary>
	private IEnumerator Pattern_2_Laser1()
	{
		// 레이저 프리팹 또는 그룹 배열이 없으면 패턴 실행 불가
		if (!laserPrefab) yield break;
		if (p2_laser1GroupParents == null || p2_laser1GroupParents.Length == 0) yield break;

		for (int r = 0; r < p2_laser1RepeatCount; r++)
		{
			if (bossBase.IsDead) yield break;

			// 1) 사용할 그룹을 랜덤으로 하나 선택
			Transform root = null;
			// null 이 섞여 있을 수 있으므로, 몇 번까지 시도해서 유효한 그룹을 찾는다.
			const int maxTry = 10;
			for (int i = 0; i < maxTry; i++)
			{
				Transform candidate = p2_laser1GroupParents[Random.Range(0, p2_laser1GroupParents.Length)];
				if (candidate != null)
				{
					root = candidate;
					break;
				}
			}

			// 유효한 그룹이 없으면 패턴 종료
			if (!root) yield break;

			// 2) 선택한 그룹의 자식 위치들을 모은다.
			List<Transform> points = new List<Transform>();
			foreach (Transform child in root)
			{
				if (child != root) // 혹시 모를 자기 자신 제외
					points.Add(child);
			}

			if (points.Count == 0)
				continue; // 자식이 없으면 이 사이클은 건너뛰고 다음 반복으로

			// 3) 경고 프리팹 생성
			List<GameObject> warnings = new List<GameObject>();
			foreach (Transform t in points)
			{
				if (!t) continue;
				if (p2_laser1WarningPrefab)
				{
					GameObject w = Instantiate(p2_laser1WarningPrefab, t.position, Quaternion.identity);
					warnings.Add(w);
				}
			}

			// 경고 SFX 한 번만 재생
			PlayPatternSfx(p2_laser1WarningSfx);

			// 경고 유지 시간
			if (p2_laser1WarningDuration > 0f)
				yield return new WaitForSeconds(p2_laser1WarningDuration);

			// 4) 경고 삭제 + 레이저 생성
			foreach (GameObject w in warnings)
			{
				if (!w) continue;
				Vector3 pos = w.transform.position;
				Destroy(w);

				AstraLaserHazard laser = Instantiate(laserPrefab, pos, Quaternion.identity);
				// p2_laser1Direction / p2_laser1Speed 설정에 따라
				// 고정 레이저 또는 이동 레이저로 사용 가능
				laser.SetupStraight(p2_laser1Direction, p2_laser1Speed);
			}

			// 레이저 발사 SFX
			PlayPatternSfx(p2_laser1FireSfx);

			// 5) 사이클 간 간격
			if (p2_laser1Interval > 0f)
				yield return new WaitForSeconds(p2_laser1Interval);
		}
	}


	/// <summary>
	/// 레이저 2
	/// - 플레이어 현재 위치에 경고 프리팹 생성
	/// - 0.7초 후 경고 삭제 + 해당 위치에서 플레이어를 1초 동안 추적하는 레이저 생성
	/// - p2_laser2RepeatCount회 반복
	/// </summary>
	private IEnumerator Pattern_2_Laser2()
	{
		if (!laserPrefab) yield break;
		if (!player) yield break;

		for (int r = 0; r < p2_laser2RepeatCount; r++)
		{
			if (bossBase.IsDead || !player) yield break;

			// 1) 경고 위치 = 현재 플레이어 위치
			Vector3 warnPos = player.position;
			GameObject warning = null;
			if (p2_laser2WarningPrefab)
				warning = Instantiate(p2_laser2WarningPrefab, warnPos, Quaternion.identity);

			// 경고 SFX
			PlayPatternSfx(p2_laser2WarningSfx);

			// 경고 유지 시간
			if (p2_laser2WarningDuration > 0f)
				yield return new WaitForSeconds(p2_laser2WarningDuration);

			if (!player) yield break;

			if (warning)
			{
				warnPos = warning.transform.position;
				Destroy(warning);
			}

			// 2) 경고 위치에서 레이저 생성 → 플레이어를 추적
			var laser = Instantiate(laserPrefab, warnPos, Quaternion.identity);
			laser.SetupHoming(player, p2_laser2Speed, p2_laser2FollowDuration);

			// 레이저 발사 SFX
			PlayPatternSfx(p2_laser2FireSfx);

			// 추적 시간 동안 대기
			if (p2_laser2FollowDuration > 0f)
				yield return new WaitForSeconds(p2_laser2FollowDuration);
		}
	}

	/// <summary>
	/// 2-2: 보스 주변에서 작은 구체 여러 개를 생성한 뒤,
	///      1초 뒤 플레이어 근처를 향해 날아가지만,
	///      p2_22_aimAccuracy(예: 0.2 = 20%) 확률만 정확히 겨냥하는 패턴.
	/// </summary>
	private IEnumerator Pattern_2_2_HomingSmallOrbs()
	{
		if (bossBase.IsDead || !player || !actor)
			yield break;

		// 생성 사이클이 0 이하면 아무 것도 하지 않음
		if (p2_22_spawnCount <= 0)
			yield break;

		// 각 사이클마다
		for (int wave = 0; wave < p2_22_spawnCount; wave++)
		{
			if (bossBase.IsDead || !player || !actor)
				yield break;

			// 한 사이클에서 p2_22_orbsPerSpawn 개의 구체 생성
			int count = Mathf.Max(1, p2_22_orbsPerSpawn); // 최소 1개는 나오도록 보정

			for (int i = 0; i < count; i++)
			{
				// 보스 주변 랜덤 위치에서 스폰
				Vector2 offset = Random.insideUnitCircle * 4f;
				Vector3 spawnPos = actor.position + (Vector3)offset;

				var orb = SpawnOrbAt(spawnPos, p2_22_orbPrefab, p2_22_orbSpeed);
				if (!orb)
					continue;

				// 기본 방향: 플레이어 방향
				Vector2 dirToPlayer = (player.position - spawnPos).normalized;

				// 명중/빗나감 각도 계산
				// - 기본은 ±40도 정도로 크게 틀어져서 대부분 빗나가게
				// - 명중 케이스(20%)에서는 ±5도 정도의 작은 오차만 줌
				float missAngle = Random.Range(-40f, 40f);
				if (Random.value <= p2_22_aimAccuracy)
				{
					// 명중 (또는 거의 명중)
					missAngle = Random.Range(-5f, 5f);
				}

				Vector2 finalDir =
					(Vector2)(Quaternion.Euler(0f, 0f, missAngle) * dirToPlayer);

				// 1초 뒤 발사되도록 설정
				orb.SetupStraight(finalDir);
				orb.SetStartMoveDelay(1.0f);
			}

			// 사이클 간 간격
			if (p2_22_spawnInterval > 0f)
				yield return new WaitForSeconds(p2_22_spawnInterval);
			else
				yield return null;
		}
	}


	/// <summary>
	/// 2-3: 보스 위치에서 구체 10개를 생성하고 일정 간격으로 360도 방향으로 퍼뜨리는 패턴.
	/// </summary>
	private IEnumerator Pattern_2_3_RadialOrbs10()
	{
		int count = 10;
		float angleStep = 360f / count;
		List<AstraOrbProjectile> orbs = new List<AstraOrbProjectile>();

		// 다수 생성 SFX
		PlayPatternSfx(p2_23_orbBatchSfx);

		for (int i = 0; i < count; i++)
		{
			var orb = SpawnOrbAt(actor.position, p2_23_orbPrefab, p2_23_orbSpeed);
			if (orb)
			{
				orb.SetSpawnSfxEnabled(false);
				orbs.Add(orb);
			}
		}

		yield return new WaitForSeconds(0.5f);

		for (int i = 0; i < orbs.Count; i++)
		{
			if (!orbs[i]) continue;
			float angle = angleStep * i;
			Vector2 dir = DegreeToDir(angle);
			orbs[i].SetupStraight(dir);
		}

		yield return new WaitForSeconds(1f);
	}

	#endregion

	// ================================================
	#region 페이지 3 (마지막 루프)
	// ================================================

	/// <summary>
	/// 3페이지 메인 루프
	/// - 카메라 프리셋 적용
	/// - 보스를 고정 위치로 이동 후 1초 대기
	/// - 카메라 회전 루프 시작
	/// - 보스가 죽을 때까지 "랜덤 위치 유도 구체"를 반복해서 발사
	/// </summary>
	private IEnumerator RunPage3Loop()
	{
		// 1) 카메라 프리셋 적용
		ApplyRandomCameraPreset(page3CameraPresets, allowShake: false);

		// 2) 보스를 고정 위치로 이동 (있다면)
		if (page3FixedPosition != null)
		{
			yield return MoveBossTo(page3FixedPosition.position);

			// 이동 후 1초 대기(공통 delayAfterMove 사용)
			if (delayAfterMove > 0f)
				yield return new WaitForSeconds(delayAfterMove);
		}

		// 3) 카메라 회전 루프 시작
		StartCoroutine(Page3CameraRotateLoop());

		// 4) 보스가 죽을 때까지 “랜덤 위치 유도 구체” 반복
		while (bossBase != null && !bossBase.IsDead)
		{
			// 구체 1발 패턴
			yield return StartCoroutine(Pattern_3_HomingOrbOnce());

			// 발사 간격
			if (p3_spawnInterval > 0f)
				yield return new WaitForSeconds(p3_spawnInterval);

			// 추가 대기(필요 없으면 p3_patternLoopDelay=0)
			if (p3_patternLoopDelay > 0f)
				yield return new WaitForSeconds(p3_patternLoopDelay);
		}
	}

	private IEnumerator Pattern_3_HomingOrbOnce()
	{
		if (!player) yield break;

		// 사용할 프리팹 선택
		AstraOrbProjectile prefab = p3_orbPrefab ? p3_orbPrefab : orbPrefab;
		if (!prefab) yield break;

		// 1) 스폰 위치 선택
		Vector3 spawnPos;

		if (p3_spawnPoints != null && p3_spawnPoints.Length > 0)
		{
			Transform sp = p3_spawnPoints[Random.Range(0, p3_spawnPoints.Length)];
			if (sp != null)
				spawnPos = sp.position;
			else
				spawnPos = actor ? actor.position : Vector3.zero;
		}
		else
		{
			// 스폰 포인트가 하나도 등록되어 있지 않다면, 보스 위치에서 쏜다.
			spawnPos = actor ? actor.position : Vector3.zero;
		}

		// 2) 구체 생성
		AstraOrbProjectile orb = SpawnOrbAt(spawnPos, prefab, p3_orbSpeed);
		if (!orb) yield break;

		// 3) 목표 방향 계산 (기본: 플레이어 방향)
		Vector2 dirToPlayer = (player.position - spawnPos).normalized;

		// 명중/빗나감 각도 계산
		// - 기본은 ±40도 정도로 틀어서 빗나가게
		// - 명중 판정일 때만 ±5도 정도의 작은 오차
		float missAngle = Random.Range(-40f, 40f);
		if (Random.value <= p3_orbAimAccuracy)
		{
			// 명중 케이스
			missAngle = Random.Range(-5f, 5f);
		}

		Vector2 finalDir =
			(Vector2)(Quaternion.Euler(0f, 0f, missAngle) * dirToPlayer);

		// 4) 1초(또는 설정된 시간) 뒤에 발사되도록 설정
		orb.SetupStraight(finalDir);
		orb.SetStartMoveDelay(p3_orbDelayToChase);

		// 필요하다면 여기서 대기 (연출용)
		if (p3_orbDelayToChase > 0f)
			yield return new WaitForSeconds(p3_orbDelayToChase);
	}
	/// <summary>
	/// 3페이지 동안 카메라를 일정 속도로 계속 회전시키는 루프.
	/// - p3_cameraRotateSpeed (도/초)를 사용.
	/// - 시작 시 방향은 랜덤(시계/반시계)으로 결정.
	/// - 보스가 죽을 때까지 회전, 이후 ResetAll 로 복구.
	/// </summary>
	private IEnumerator Page3CameraRotateLoop()
	{
		if (!cameraController) yield break;

		float speed = p3_cameraRotateSpeed;
		if (Mathf.Approximately(speed, 0f))
			yield break; // 속도 0이면 회전 안 함

		// 시계/반시계 방향 랜덤 결정
		if (Random.value < 0.5f)
			speed = -speed;

		// StartRotate: infinite=true 일 때 angle 값은 사용되지 않고,
		// speed(부호 포함)만으로 계속 회전한다.
		cameraController.StartRotate(0f, speed, infinite: true);

		// 보스가 살아 있는 동안 대기
		while (bossBase != null && !bossBase.IsDead)
			yield return null;

		// 3페이지 종료 시 카메라 상태 복구
		cameraController.ResetAll(true, true, false);
	}

	// ================================================
	#region 공통 유틸 (이동, 카메라, 구체 스폰, SFX)
	// ================================================

	/// <summary>
	/// 랜덤 이동 포인트를 스폰 그룹 부모의 자식에서 자동 구성.
	/// </summary>
	private void BuildRandomMovePointsFromGroups()
	{
		if (randomMoveGroupParents == null || randomMoveGroupParents.Length == 0)
			return;

		List<Transform> list = new List<Transform>();

		foreach (Transform root in randomMoveGroupParents)
		{
			if (!root) continue;

			foreach (Transform child in root)
			{
				if (child != root)
					list.Add(child);
			}
		}

		if (list.Count > 0)
			randomMovePoints = list.ToArray();
	}

	/// <summary>
	/// 보스를 지정 위치로 이동시키는 코루틴.
	/// </summary>
	private IEnumerator MoveBossTo(Vector3 targetPos)
	{
		if (!actor) yield break;

		while (Vector3.Distance(actor.position, targetPos) > bossStopDistance)
		{
			Vector3 dir = (targetPos - actor.position).normalized;
			actor.position += dir * bossMoveSpeed * Time.deltaTime;
			yield return null;
		}

		actor.position = targetPos;
	}

	/// <summary>
	/// 보스를 랜덤 이동 포인트 중 하나로 이동시킨다. 없으면 홈 포지션으로 이동.
	/// </summary>
	private IEnumerator MoveBossToRandomPoint()
	{
		if (randomMovePoints == null || randomMovePoints.Length == 0)
		{
			if (homePosition)
				yield return MoveBossTo(homePosition.position);
			yield break;
		}

		Transform pick = randomMovePoints[Random.Range(0, randomMovePoints.Length)];
		if (pick != null)
			yield return MoveBossTo(pick.position);

		// 이동을 마친 뒤 공격 시작까지 기다리는 시간
		if (delayAfterMove > 0f)
			yield return new WaitForSeconds(delayAfterMove);
	}

	/// <summary>
	/// 보스를 홈 포지션으로 이동시키고, 카메라 상태를 초기화한다.
	/// </summary>
	private IEnumerator ResetBossAndCamera()
	{
		if (homePosition)
			yield return MoveBossTo(homePosition.position);

		if (cameraController != null)
			cameraController.ResetAll(true, true, false);

		yield return null;
	}

	/// <summary>
	/// 구체 프리팹을 지정 위치에 생성하고, AstraOrbProjectile 컴포넌트를 반환한다.
	/// - prefabOverride: null이면 공통 orbPrefab 사용
	/// - speedOverride: 0 이하면 프리팹 기본 속도 유지
	/// </summary>
	private AstraOrbProjectile SpawnOrbAt(
		Vector3 position,
		AstraOrbProjectile prefabOverride = null,
		float speedOverride = 0f)
	{
		AstraOrbProjectile prefabToUse = prefabOverride ? prefabOverride : orbPrefab;
		if (!prefabToUse) return null;

		var orb = Instantiate(prefabToUse, position, Quaternion.identity);

		if (orb != null && speedOverride > 0f)
			orb.SetSpeed(speedOverride);

		return orb;
	}

	/// <summary>
	/// 카메라 프리셋 중 하나를 랜덤으로 적용한다.
	/// </summary>
	private void ApplyRandomCameraPreset(AstraCameraPreset[] presets, bool allowShake)
	{
		if (!cameraController) return;
		if (presets == null || presets.Length == 0) return;

		var preset = presets[Random.Range(0, presets.Length)];
		if (preset == null) return;

		float angle = preset.targetAngle;
		float time = Mathf.Max(0f, preset.rotateTime);

		if (Mathf.Abs(angle) > 0.01f)
			PlayCameraRotateSfx();

		if (time <= 0f)
		{
			cameraController.StartRotate(angle, Mathf.Abs(angle) * 100f, false);
		}
		else
		{
			float speed = Mathf.Abs(angle) / time;
			cameraController.StartRotate(angle, speed, false);
		}

		if (preset.zoomSize > 0f)
		{
			float zoomTime = Mathf.Max(0f, preset.zoomTime);
			cameraController.ZoomTo(preset.zoomSize, zoomTime <= 0f ? 0.01f : zoomTime);
		}

		if (allowShake && preset.useShake && preset.shakeDuration > 0f && preset.shakeMagnitude > 0f)
		{
			cameraController.Shake(preset.shakeDuration, preset.shakeMagnitude, preset.shakeFrequency);
		}
	}

	/// <summary>
	/// 카메라 회전 SFX 재생.
	/// </summary>
	private void PlayCameraRotateSfx()
	{
		if (!cameraRotateSFX) return;

		if (cameraSfxSource)
		{
			cameraSfxSource.PlayOneShot(cameraRotateSFX);
		}
		else
		{
			Vector3 pos = actor ? actor.position : Vector3.zero;
			AudioSource.PlayClipAtPoint(cameraRotateSFX, pos, 1f);
		}
	}

	/// <summary>
	/// 패턴 전용 SFX 재생. 너무 짧은 간격으로 여러 번 호출되면 patternSfxCooldown으로 막는다.
	/// </summary>
	private void PlayPatternSfx(AudioClip clip)
	{
		if (!clip) return;
		if (Time.time - _lastPatternSfxTime < patternSfxCooldown)
			return;

		_lastPatternSfxTime = Time.time;

		if (patternSfxSource)
		{
			patternSfxSource.PlayOneShot(clip);
		}
		else
		{
			Vector3 pos = actor ? actor.position : Vector3.zero;
			AudioSource.PlayClipAtPoint(clip, pos, 1f);
		}
	}

	#endregion

	/// <summary>
	/// 도 단위 각도를 2D 방향 벡터로 변환한다.
	/// </summary>
	private static Vector2 DegreeToDir(float deg)
	{
		float rad = deg * Mathf.Deg2Rad;
		return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
	}

	#endregion
}
