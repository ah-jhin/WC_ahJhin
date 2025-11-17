using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제우스 보스 전용 패턴 컨트롤러.
/// - BossBase를 상속해서 HP / UI / 보스바 시스템을 그대로 사용.
/// - BossSequenceController가 BindActor를 호출했을 때만 패턴 시작.
/// - PDF 사양: 페이지 1~3 패턴 + HP 500 이하 고정 구체 폭우.
/// - 각 패턴마다 "경고 SFX" + "공격 SFX" 두 종류를 지원.
/// </summary>
public class ZeusBossBase : BossBase
{
	// ─────────────────────────────────────────────────────────
	#region 공통 레퍼런스
	// ─────────────────────────────────────────────────────────

	[Header("⑧ Zeus 공통 레퍼런스")]

	[Tooltip("플레이어 체력 컴포넌트. 씬의 Player에 있는 PlayerHealth를 할당한다.")]
	public PlayerHealth player;

	[Tooltip("보스의 실제 모델(이동 기준). 일반적으로 BossSequenceController가 BindActor로 넘겨주는 Transform을 사용한다.")]
	public Transform bossModelRoot;

	[Tooltip("패턴 사이에 이동할 위치 포인트들. 보스는 이 포인트들을 순서대로/순환하면서 이동한다.")]
	public Transform[] movePoints;

	[Tooltip("HP 500 이하 고정 패턴에서 사용할 보스 최종 위치.")]
	public Transform finalPhasePoint;

	[Tooltip("패턴 SFX를 재생할 오디오 소스. 없으면 자신 또는 BossBase의 AudioSource를 사용한다.")]
	public AudioSource patternSfxSource;

	[Tooltip("패턴 사이 기본 대기 시간(초). 보스가 위치를 옮긴 뒤 쉬는 시간.")]
	public float patternInterval = 2.5f;

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴별 위치/프리팹 설정
	// ─────────────────────────────────────────────────────────

	[Header("패턴1: 낙뢰(고정 위치)")]
	[Tooltip("낙뢰가 떨어질 수 있는 고정 위치 포인트들(그룹 부모들).")]
	public Transform[] pattern1_LightningPoints;
	[Tooltip("경고가 표시된 후 실제 낙뢰가 떨어질 때까지 지연 시간(초).")]
	public float pattern1_WarningDelay = 0.7f;
	[Tooltip("패턴1 낙뢰 경고를 표시할 때 사용할 경고 프리팹(아이콘, 이펙트 등).")]
	public GameObject pattern1_WarningPrefab;

	[Header("패턴2: 돌진")]
	[Tooltip("돌진 시작 전 경고가 유지되는 시간(초).")]
	public float pattern2_WarningTime = 1.0f;
	[Tooltip("돌진 속도(유닛/초).")]
	public float pattern2_ChargeSpeed = 15f;
	[Tooltip("돌진을 유지하는 시간(초). 이 동안 플레이어와 충돌하면 피해를 준다.")]
	public float pattern2_ChargeDuration = 1.0f;
	[Tooltip("돌진 이후 시작 위치로 되돌아갈 때 속도(유닛/초).")]
	public float pattern2_ReturnSpeed = 3f;
	[Tooltip("돌진 시 사용할 충돌 히트박스(Trigger Collider가 있는 자식 오브젝트). 필요 없으면 비워둔다.")]
	public Collider2D pattern2_Hitbox;
	[Tooltip("패턴2 전용 경고 프리팹(플레이어 위치에 표시). 비워두면 아래 공통 타겟 경고 프리팹을 사용한다.")]
	public GameObject pattern2_WarningPrefab;
	[Header("패턴3: 보스 주변 구체 탄막")]
	[Tooltip("패턴3: 한 번에 발사할 구체의 개수.")]
	public int pattern3_OrbCount = 10;
	[Tooltip("패턴3: 각 구체의 속도.")]
	public float pattern3_OrbSpeed = 8f;
	[Tooltip("패턴3: 경고 표시 후 발사까지 지연 시간.")]
	public float pattern3_WarningDelay = 1.0f;
	[Tooltip("패턴3: 구체 탄막을 표시할 때 사용할 경고 반지/마커 프리팹.")]
	public GameObject pattern3_WarningPrefab;

	[Header("패턴4: 랜덤 구체 폭격")]
	[Tooltip("폭격이 떨어질 수 있는 X좌표 기준 포인트들(그룹 부모들). 이 위치의 위쪽(Y+SpawnHeight)에서 구체가 생성된다.")]
	public Transform[] pattern4_BombPoints;
	[Tooltip("폭격 시, 구체를 생성할 높이(Y). 예: 20")]
	public float pattern4_SpawnHeight = 20f;
	[Tooltip("한 세트에서 반복 생성할 횟수.")]
	public int pattern4_BurstCount = 30;
	[Tooltip("한 발과 다음 발 사이의 간격(초).")]
	public float pattern4_BurstInterval = 0.2f;
	[Tooltip("경고 표시 후 구체가 실제로 떨어지기까지의 지연(초).")]
	public float pattern4_WarningDelay = 1.0f;
	[Tooltip("폭격 구체가 떨어지는 속도(유닛/초).")]
	public float pattern4_FallSpeed = 8f;
	[Tooltip("패턴4 전용 경고 프리팹(폭격 지점에 표시). 비워두면 아래 공통 타겟 경고 프리팹을 사용한다.")]
	public GameObject pattern4_WarningPrefab;

	[Header("패턴5/6: 타겟팅 낙뢰")]
	[Tooltip("플레이어 위치를 기준으로 사용하는 공통 기본 경고 프리팹. 패턴별 전용 프리팹이 비어있을 때 사용된다.")]
	public GameObject targetWarningPrefab;
	[Tooltip("패턴5 전용 경고 프리팹(수직 낙뢰용). 비워두면 위 공통 프리팹을 사용한다.")]
	public GameObject pattern5_WarningPrefab;
	[Tooltip("패턴6 전용 경고 프리팹(각도 낙뢰용). 비워두면 위 공통 프리팹을 사용한다.")]
	public GameObject pattern6_WarningPrefab;
	[Tooltip("타겟 경고 후 낙뢰까지의 지연 시간(초).")]
	public float targetWarningDelay = 0.8f;
	[Tooltip("타겟 낙뢰 패턴5에서 사용될 기본 반복 횟수(페이지에 따라 곱해진다).")]
	public int pattern5_BaseRepeatCount = 1;
	[Tooltip("타겟 낙뢰 패턴6에서 사용될 기본 반복 횟수(페이지에 따라 곱해진다).")]
	public int pattern6_BaseRepeatCount = 1;

	[Header("공통 공격 프리팹")]
	[Tooltip("수직 방향 낙뢰 공격 프리팹.")]
	public GameObject lightningAttackPrefab;
	[Tooltip("수평 또는 회전된 낙뢰(빔) 공격 프리팹. 없으면 lightningAttackPrefab을 회전시켜 사용한다.")]
	public GameObject sideLightningAttackPrefab;
	[Tooltip("보스 주변에서 퍼져나가는 구체 프리팹.")]
	public GameObject radialOrbPrefab;
	[Tooltip("위에서 떨어지는 구체 프리팹(폭우/폭격 공용).")]
	public GameObject fallingOrbPrefab;

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 고정 패턴(HP 500 이하 구체 폭우) 설정
	// ─────────────────────────────────────────────────────────

	[Header("고정 패턴: HP 500 이하 구체 폭우")]
	[Tooltip("HP 500 이하 패턴에서 구체가 떨어질 X좌표 기준 포인트들(그룹 부모들).")]
	public Transform[] fixedOrbPoints;
	[Tooltip("고정 패턴 구체 생성 높이(Y). 예: 20")]
	public float fixedOrbSpawnHeight = 20f;
	[Tooltip("HP 500일 때의 기본 생성 간격(초).")]
	public float fixedOrbBaseInterval = 1.11f;
	[Tooltip("HP가 10 줄어들 때마다 생성 간격을 얼마나 줄일지(초 단위).")]
	public float fixedOrbIntervalPer10Hp = 0.014f;
	[Tooltip("생성 간격의 최소값(초).")]
	public float fixedOrbMinInterval = 0.41f;
	[Tooltip("고정 패턴에서 구체가 떨어지는 속도(유닛/초).")]
	public float fixedOrbFallSpeed = 8f;

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴별 SFX (경고/공격)
	// ─────────────────────────────────────────────────────────

	[Header("패턴1 SFX (낙뢰 필드)")]
	public AudioClip sfxP1_Warn;
	public AudioClip sfxP1_Atk;

	[Header("패턴2 SFX (돌진)")]
	public AudioClip sfxP2_Warn;
	public AudioClip sfxP2_Atk;

	[Header("패턴3 SFX (구체 탄막)")]
	public AudioClip sfxP3_Warn;
	public AudioClip sfxP3_Atk;

	[Header("패턴4 SFX (랜덤 폭격)")]
	public AudioClip sfxP4_Warn;
	public AudioClip sfxP4_Atk;

	[Header("패턴5 SFX (수직 타겟 낙뢰)")]
	public AudioClip sfxP5_Warn;
	public AudioClip sfxP5_Atk;

	[Header("패턴6 SFX (각도 낙뢰)")]
	public AudioClip sfxP6_Warn;
	public AudioClip sfxP6_Atk;

	[Header("고정 패턴 SFX (구체 폭우)")]
	public AudioClip sfxFixed_Start;
	public AudioClip sfxFixed_Drop;

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 내부 상태
	// ─────────────────────────────────────────────────────────

	const float PAGE2_THRESHOLD = 0.55f;
	const float PAGE3_THRESHOLD = 0.25f;

	bool _patternsRunning = false;
	bool _stopRequested = false;
	bool _fixedPatternStarted = false;

	int _page1Index = 0;
	int _page2Index = 0;
	int _page3Index = 0;

	int _lastMovePointIndex = -1;

	bool _loggedStopReason = false; // 디버그용
	bool _pendingStart = false;      // 딜레이 후 패턴을 시작해야 하는지 여부
	float _pendingStartTime = 0f;    // 패턴 시작 예정 시각(Time.time 기준)

	#endregion

	// ─────────────────────────────────────────────────────────
	#region Start / BindActor / Canvas / Update
	// ─────────────────────────────────────────────────────────

	protected override void Start()
	{
		base.Start();               // BossBase: currentHP 초기화 + 보스바 숨김
		ForceEnableCanvasIfNeeded();
	}

	public new void BindActor(Transform t)
	{
		base.BindActor(t);
		Debug.Log("[ZeusBossBase] BindActor 호출됨");

		// 이동 기준 점
		if (!bossModelRoot)
			bossModelRoot = t;

		// 플레이어 자동 연결(비어있다면)
		if (!player)
		{
			player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
			if (!player)
				Debug.LogWarning("[ZeusBossBase] PlayerHealth를 찾지 못했다. 패턴이 즉시 중단될 수 있다.");
		}

		// SFX 소스 자동 연결(비어있다면)
		if (!patternSfxSource)
		{
			patternSfxSource = GetComponent<AudioSource>();
			if (!patternSfxSource && bossModelRoot)
				patternSfxSource = bossModelRoot.GetComponent<AudioSource>();
			if (!patternSfxSource && audioSrc)
				patternSfxSource = audioSrc;
		}

		ForceEnableCanvasIfNeeded();

		// 이전 패턴 코루틴 정리
		StopAllCoroutines();
		_patternsRunning = false;
		_stopRequested = false;
		_fixedPatternStarted = false;
		_loggedStopReason = false;

		// BossSequenceController에서 패턴 지연 시간 읽기
		var seq = GetComponent<BossSequenceController>();
		float delay = seq ? seq.patternStartDelay : 0f;

		_pendingStart = true;
		_pendingStartTime = Time.time + delay;

		Debug.Log($"[ZeusBossBase] 패턴 {delay}초 뒤({_pendingStartTime:F2})에 시작 예약");
	}

	void ForceEnableCanvasIfNeeded()
	{
		Canvas canvas = null;

		if (bossBarRoot)
			canvas = bossBarRoot.GetComponentInParent<Canvas>(true);

		if (!canvas)
			canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

		if (canvas && !canvas.gameObject.activeSelf)
			canvas.gameObject.SetActive(true);
	}

	private void Update()
	{
		if (_pendingStart && !_patternsRunning && !_stopRequested && Time.time >= _pendingStartTime)
		{
			_pendingStart = false;

			Debug.Log("[ZeusBossBase] Update에서 패턴 시작");
			StartPatternLoop();
		}
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 메인 루프 + 공통 유틸
	// ─────────────────────────────────────────────────────────

	void StartPatternLoop()
	{
		if (_patternsRunning)
			return;

		Debug.Log("[ZeusBossBase] StartPatternLoop 진입");

		_patternsRunning = true;
		_stopRequested = false;
		_fixedPatternStarted = false;
		_loggedStopReason = false;

		OnBossDie -= HandleBossDie;
		OnBossDie += HandleBossDie;

		StartCoroutine(CoMainPatternLoop());
	}

	void HandleBossDie(BossBase b)
	{
		_stopRequested = true;
	}

	float GetHpRatio()
	{
		if (maxHP <= 0) return 0f;
		return Mathf.Clamp01((float)currentHP / maxHP);
	}

	bool ShouldStopPattern()
	{
		if (_stopRequested)
		{
			if (!_loggedStopReason)
			{
				Debug.Log("[ZeusBossBase] 패턴 중단: _stopRequested = true");
				_loggedStopReason = true;
			}
			return true;
		}
		if (!player)
		{
			if (!_loggedStopReason)
			{
				Debug.Log("[ZeusBossBase] 패턴 중단: player 레퍼런스가 없다.");
				_loggedStopReason = true;
			}
			return true;
		}
		if (player.IsDead)
		{
			if (!_loggedStopReason)
			{
				Debug.Log("[ZeusBossBase] 패턴 중단: 플레이어 사망.");
				_loggedStopReason = true;
			}
			return true;
		}
		if (currentHP <= 0)
		{
			if (!_loggedStopReason)
			{
				Debug.Log("[ZeusBossBase] 패턴 중단: 보스 HP 0 이하.");
				_loggedStopReason = true;
			}
			return true;
		}
		return false;
	}

	void PlaySfx(AudioClip clip, float volume = 1f)
	{
		if (!clip) return;
		var src = patternSfxSource ? patternSfxSource : audioSrc;
		if (src)
			src.PlayOneShot(clip, volume);
	}

	/// <summary>
	/// 발사체에 초기 속도를 세팅하는 공통 함수.
	/// - ZeusBulletMover 가 있으면 Init 사용
	/// - 없으면 Rigidbody2D.linearVelocity 로 세팅
	/// </summary>
	void SetupProjectile(GameObject proj, Vector2 velocity)
	{
		if (!proj) return;

		var mover = proj.GetComponent<ZeusBulletMover>();
		if (mover)
		{
			mover.Init(velocity);
		}
		else
		{
			var rb = proj.GetComponent<Rigidbody2D>();
			if (rb)
				rb.linearVelocity = velocity;
		}
	}

	IEnumerator MoveAndWait()
	{
		if (ShouldStopPattern())
			yield break;

		if (bossModelRoot && movePoints != null && movePoints.Length > 0)
		{
			int nextIndex = _lastMovePointIndex;
			if (movePoints.Length > 1)
			{
				while (nextIndex == _lastMovePointIndex)
					nextIndex = Random.Range(0, movePoints.Length);
			}
			else nextIndex = 0;

			_lastMovePointIndex = nextIndex;
			Transform target = movePoints[nextIndex];
			const float moveSpeed = 6f;

			while (!ShouldStopPattern() && bossModelRoot && target)
			{
				Vector3 pos = bossModelRoot.position;
				Vector3 to = Vector3.MoveTowards(pos, target.position, moveSpeed * Time.deltaTime);
				bossModelRoot.position = to;

				if (Vector3.Distance(to, target.position) < 0.05f)
					break;

				yield return null;
			}
		}

		if (patternInterval > 0f)
		{
			float t = 0f;
			while (t < patternInterval)
			{
				if (ShouldStopPattern())
					yield break;
				t += Time.deltaTime;
				yield return null;
			}
		}
	}

	IEnumerator CoMainPatternLoop()
	{
		Debug.Log("[ZeusBossBase] CoMainPatternLoop 시작");

		while (!_stopRequested)
		{
			if (ShouldStopPattern())
				yield break;

			// HP 500 이하 고정 패턴 1회 시작
			if (!_fixedPatternStarted && currentHP <= 500)
			{
				_fixedPatternStarted = true;
				StartCoroutine(CoFixedOrbRainPattern());
			}

			float ratio = GetHpRatio();

			if (ratio > PAGE2_THRESHOLD)
				yield return RunNextPage1Pattern();
			else if (ratio > PAGE3_THRESHOLD)
				yield return RunNextPage2Pattern();
			else
				yield return RunNextPage3Pattern();
		}
	}

	IEnumerator RunNextPage1Pattern()
	{
		int idx = _page1Index % 4;
		_page1Index++;

		switch (idx)
		{
			case 0: return CoPattern1_LightningField();
			case 1: return CoPattern5_TargetLightning(1);
			case 2: return CoPattern2_Charge();
			case 3: return CoPattern5_TargetLightning(1);
		}
		return null;
	}

	IEnumerator RunNextPage2Pattern()
	{
		int idx = _page2Index % 5;
		_page2Index++;

		switch (idx)
		{
			case 0: return CoPattern3_OrbBarrage();
			case 1: return CoPattern1_LightningField();
			case 2: return CoPattern5_TargetLightning(2);
			case 3: return CoPattern2_Charge();
			case 4: return CoPattern4_OrbBombardment();
		}
		return null;
	}

	IEnumerator RunNextPage3Pattern()
	{
		int idx = _page3Index % 5;
		_page3Index++;

		switch (idx)
		{
			case 0: return CoPattern3_OrbBarrage(true);
			case 1: return CoPattern5_TargetLightning(3);
			case 2: return CoPattern6_AngledLightning();
			case 3: return CoPattern4_OrbBombardment();
			case 4: return CoPattern2_Charge(true);
		}
		return null;
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴1: 낙뢰(고정 위치)
	// ─────────────────────────────────────────────────────────

	IEnumerator CoPattern1_LightningField()
	{
		Transform[] pointsInGroup = GetRandomGroupPoints(pattern1_LightningPoints);
		if (pointsInGroup == null || pointsInGroup.Length == 0)
		{
			yield return MoveAndWait();
			yield break;
		}

		// 경고 SFX
		PlaySfx(sfxP1_Warn);

		// 경고 생성 + 자동 삭제
		foreach (var p in pointsInGroup)
		{
			if (!p) continue;
			if (ShouldStopPattern())
				yield break;

			if (pattern1_WarningPrefab)
			{
				var w = Instantiate(pattern1_WarningPrefab, p.position, Quaternion.identity);
				Destroy(w, pattern1_WarningDelay + 0.5f);
			}
		}

		float t = 0f;
		while (t < pattern1_WarningDelay)
		{
			if (ShouldStopPattern())
				yield break;
			t += Time.deltaTime;
			yield return null;
		}

		// 공격 SFX
		PlaySfx(sfxP1_Atk);

		// 낙뢰 생성
		foreach (var p in pointsInGroup)
		{
			if (!p) continue;
			if (ShouldStopPattern())
				yield break;

			if (lightningAttackPrefab)
				Instantiate(lightningAttackPrefab, p.position, Quaternion.identity);
		}

		yield return MoveAndWait();
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴2: 돌진
	// ─────────────────────────────────────────────────────────

	IEnumerator CoPattern2_Charge(bool hard = false)
	{
		if (!bossModelRoot)
		{
			yield return MoveAndWait();
			yield break;
		}

		// 경고 SFX
		PlaySfx(sfxP2_Warn);

		// 경고 마커 (플레이어 위치, 패턴2 전용 프리팹 우선 사용)
		if (player)
		{
			GameObject warnPrefab = pattern2_WarningPrefab ? pattern2_WarningPrefab : targetWarningPrefab;
			if (warnPrefab)
			{
				var w = Instantiate(warnPrefab, player.transform.position, Quaternion.identity);
				Destroy(w, pattern2_WarningTime + 0.5f);
			}
		}

		float warnT = 0f;
		while (warnT < pattern2_WarningTime)
		{
			if (ShouldStopPattern())
				yield break;
			warnT += Time.deltaTime;
			yield return null;
		}

		// 공격 SFX
		PlaySfx(sfxP2_Atk);

		// 실제 돌진
		float dirX = -1f;
		float chargeTime = hard ? pattern2_ChargeDuration * 0.8f : pattern2_ChargeDuration;
		float speed = hard ? pattern2_ChargeSpeed * 1.2f : pattern2_ChargeSpeed;

		if (pattern2_Hitbox) pattern2_Hitbox.enabled = true;

		float t = 0f;
		while (t < chargeTime)
		{
			if (ShouldStopPattern())
				yield break;

			Vector3 pos = bossModelRoot.position;
			pos.x += dirX * speed * Time.deltaTime;
			bossModelRoot.position = pos;

			t += Time.deltaTime;
			yield return null;
		}

		if (pattern2_Hitbox) pattern2_Hitbox.enabled = false;

		// 가장 가까운 movePoint로 복귀
		if (movePoints != null && movePoints.Length > 0)
		{
			Transform target = movePoints[0];
			float bestDist = Vector3.Distance(bossModelRoot.position, target.position);
			for (int i = 1; i < movePoints.Length; i++)
			{
				float d = Vector3.Distance(bossModelRoot.position, movePoints[i].position);
				if (d < bestDist)
				{
					bestDist = d;
					target = movePoints[i];
				}
			}

			while (!ShouldStopPattern() && bossModelRoot && target)
			{
				Vector3 pos = bossModelRoot.position;
				Vector3 to = Vector3.MoveTowards(pos, target.position, pattern2_ReturnSpeed * Time.deltaTime);
				bossModelRoot.position = to;

				if (Vector3.Distance(to, target.position) < 0.05f)
					break;

				yield return null;
			}
		}

		yield return MoveAndWait();
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴3: 구체 탄막
	// ─────────────────────────────────────────────────────────

	IEnumerator CoPattern3_OrbBarrage(bool hard = false)
	{
		if (!bossModelRoot || radialOrbPrefab == null)
		{
			yield return MoveAndWait();
			yield break;
		}

		int repeat = hard ? 2 : 1;

		for (int r = 0; r < repeat; r++)
		{
			// 경고 SFX
			PlaySfx(sfxP3_Warn);

			if (pattern3_WarningPrefab)
			{
				var w = Instantiate(pattern3_WarningPrefab, bossModelRoot.position, Quaternion.identity);
				Destroy(w, pattern3_WarningDelay + 0.5f);
			}

			float t = 0f;
			while (t < pattern3_WarningDelay)
			{
				if (ShouldStopPattern())
					yield break;
				t += Time.deltaTime;
				yield return null;
			}

			// 공격 SFX
			PlaySfx(sfxP3_Atk);

			int count = Mathf.Max(1, pattern3_OrbCount);
			for (int i = 0; i < count; i++)
			{
				if (ShouldStopPattern())
					yield break;

				float angle = (360f / count) * i;
				float rad = angle * Mathf.Deg2Rad;
				Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

				GameObject orb = Instantiate(radialOrbPrefab, bossModelRoot.position, Quaternion.identity);
				SetupProjectile(orb, dir * pattern3_OrbSpeed);
			}
		}

		yield return MoveAndWait();
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴4: 랜덤 구체 폭격
	// ─────────────────────────────────────────────────────────

	IEnumerator CoPattern4_OrbBombardment()
	{
		if (fallingOrbPrefab == null || pattern4_BombPoints == null || pattern4_BombPoints.Length == 0)
		{
			yield return MoveAndWait();
			yield break;
		}

		int count = Mathf.Max(1, pattern4_BurstCount);
		float interval = Mathf.Max(0.01f, pattern4_BurstInterval);

		for (int i = 0; i < count; i++)
		{
			if (ShouldStopPattern())
				yield break;

			Transform basePoint = GetRandomPointFromGroups(pattern4_BombPoints);
			if (basePoint)
			{
				// 경고 SFX
				PlaySfx(sfxP4_Warn);

				// 패턴4 전용 경고 프리팹 우선 사용, 없으면 공통 프리팹 사용
				GameObject warnPrefab = pattern4_WarningPrefab ? pattern4_WarningPrefab : targetWarningPrefab;
				if (warnPrefab)
				{
					var w = Instantiate(warnPrefab,
						new Vector3(basePoint.position.x, basePoint.position.y, 0f),
						Quaternion.identity);
					Destroy(w, pattern4_WarningDelay + 0.5f);
				}

				float t = 0f;
				while (t < pattern4_WarningDelay)
				{
					if (ShouldStopPattern())
						yield break;
					t += Time.deltaTime;
					yield return null;
				}

				Vector3 spawnPos = basePoint.position;
				spawnPos.y = pattern4_SpawnHeight;

				// 공격 SFX
				PlaySfx(sfxP4_Atk);

				GameObject orb = Instantiate(fallingOrbPrefab, spawnPos, Quaternion.identity);
				SetupProjectile(orb, Vector2.down * pattern4_FallSpeed);
			}

			float it = 0f;
			while (it < interval)
			{
				if (ShouldStopPattern())
					yield break;
				it += Time.deltaTime;
				yield return null;
			}
		}

		yield return MoveAndWait();
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴5: 수직 타겟 낙뢰
	// ─────────────────────────────────────────────────────────

	IEnumerator CoPattern5_TargetLightning(int repeatMultiplier)
	{
		if (!player)
		{
			yield return MoveAndWait();
			yield break;
		}

		int baseCount = Mathf.Max(1, pattern5_BaseRepeatCount);
		int total = Mathf.Max(1, baseCount * repeatMultiplier);

		for (int i = 0; i < total; i++)
		{
			if (ShouldStopPattern())
				yield break;

			Vector3 targetPos = player.transform.position;

			// 경고 SFX
			PlaySfx(sfxP5_Warn);

			// 패턴5 전용 경고 프리팹 우선 사용
			GameObject warnPrefab = pattern5_WarningPrefab ? pattern5_WarningPrefab : targetWarningPrefab;
			if (warnPrefab)
			{
				var w = Instantiate(warnPrefab, targetPos, Quaternion.identity);
				Destroy(w, targetWarningDelay + 0.5f);
			}

			float t = 0f;
			while (t < targetWarningDelay)
			{
				if (ShouldStopPattern())
					yield break;
				t += Time.deltaTime;
				yield return null;
			}

			// 공격 SFX
			PlaySfx(sfxP5_Atk);

			if (lightningAttackPrefab)
				Instantiate(lightningAttackPrefab, targetPos, Quaternion.identity);
		}

		yield return MoveAndWait();
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 패턴6: 각도 낙뢰
	// ─────────────────────────────────────────────────────────

	IEnumerator CoPattern6_AngledLightning()
	{
		if (!player)
		{
			yield return MoveAndWait();
			yield break;
		}

		int baseCount = Mathf.Max(1, pattern6_BaseRepeatCount);
		int total = baseCount;

		for (int i = 0; i < total; i++)
		{
			if (ShouldStopPattern())
				yield break;

			Vector3 targetPos = player.transform.position;

			// 경고 SFX
			PlaySfx(sfxP6_Warn);

			// 패턴6 전용 경고 프리팹 우선 사용
			GameObject warnPrefab = pattern6_WarningPrefab ? pattern6_WarningPrefab : targetWarningPrefab;
			if (warnPrefab)
			{
				var w = Instantiate(warnPrefab, targetPos, Quaternion.identity);
				Destroy(w, targetWarningDelay + 0.5f);
			}

			float t = 0f;
			while (t < targetWarningDelay)
			{
				if (ShouldStopPattern())
					yield break;
				t += Time.deltaTime;
				yield return null;
			}

			// 공격 SFX
			PlaySfx(sfxP6_Atk);

			GameObject prefab = sideLightningAttackPrefab ? sideLightningAttackPrefab : lightningAttackPrefab;
			if (prefab)
			{
				Vector3 spawnPos = targetPos + Vector3.right * 8f;
				Quaternion rot = Quaternion.Euler(0f, 0f, 180f);

				GameObject bolt = Instantiate(prefab, spawnPos, rot);
				var rb = bolt.GetComponent<Rigidbody2D>();
				if (rb)
					rb.linearVelocity = Vector2.left * 12f;
			}
		}

		yield return MoveAndWait();
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 고정 패턴: HP 500 이하 구체 폭우
	// ─────────────────────────────────────────────────────────

	IEnumerator CoFixedOrbRainPattern()
	{
		// 시작 SFX
		PlaySfx(sfxFixed_Start);

		// 최종 위치로 이동
		if (bossModelRoot && finalPhasePoint)
		{
			const float moveSpeed = 6f;
			while (!ShouldStopPattern() && bossModelRoot && finalPhasePoint)
			{
				Vector3 pos = bossModelRoot.position;
				Vector3 to = Vector3.MoveTowards(pos, finalPhasePoint.position, moveSpeed * Time.deltaTime);
				bossModelRoot.position = to;

				if (Vector3.Distance(to, finalPhasePoint.position) < 0.05f)
					break;

				yield return null;
			}
		}

		// 폭우 루프
		while (!ShouldStopPattern())
		{
			if (fixedOrbPoints == null || fixedOrbPoints.Length == 0 || fallingOrbPrefab == null)
				yield break;

			float interval = fixedOrbBaseInterval;
			if (maxHP > 0)
			{
				int hpLoss = Mathf.Max(0, maxHP - currentHP);
				int step = hpLoss / 10;
				interval = fixedOrbBaseInterval - step * fixedOrbIntervalPer10Hp;
				interval = Mathf.Clamp(interval, fixedOrbMinInterval, fixedOrbBaseInterval);
			}

			Transform basePoint = GetRandomPointFromGroups(fixedOrbPoints);
			if (basePoint)
			{
				Vector3 spawnPos = basePoint.position;
				spawnPos.y = fixedOrbSpawnHeight;

				PlaySfx(sfxFixed_Drop);

				GameObject orb = Instantiate(fallingOrbPrefab, spawnPos, Quaternion.identity);
				SetupProjectile(orb, Vector2.down * fixedOrbFallSpeed);
			}

			float t = 0f;
			while (t < interval)
			{
				if (ShouldStopPattern())
					yield break;
				t += Time.deltaTime;
				yield return null;
			}
		}
	}

	#endregion

	// ─────────────────────────────────────────────────────────
	#region 위치 그룹 유틸
	// ─────────────────────────────────────────────────────────

	Transform[] GetRandomGroupPoints(Transform[] groupRoots)
	{
		if (groupRoots == null || groupRoots.Length == 0)
			return null;

		int idx = Random.Range(0, groupRoots.Length);
		Transform root = groupRoots[idx];
		if (!root)
			return null;

		List<Transform> list = new List<Transform>();
		foreach (Transform child in root)
		{
			if (child != null)
				list.Add(child);
		}

		if (list.Count == 0)
			list.Add(root);

		return list.ToArray();
	}

	Transform GetRandomPointFromGroups(Transform[] groupRoots)
	{
		Transform[] points = GetRandomGroupPoints(groupRoots);
		if (points == null || points.Length == 0)
			return null;

		int idx = Random.Range(0, points.Length);
		return points[idx];
	}

	#endregion
}
