// BossSequenceController.cs
// 요구사항:
//  - 보스 스폰 2가지 모드 지원: Prefab, SceneObject(씬에 미리 배치 후 비활성)
//  - 프리팹에는 BossBase 스크립트가 없음. BossBase는 'Controller' 오브젝트에 존재.
//  - Q로 1회 소환. 보스 사망 후 다음 씬 또는 R로 초기화하면 Q 재소환 가능.
//  - BGM 충돌 방지(교체/정지). BossBarMarker로 UI 자동 배선.

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class BossSequenceController : MonoBehaviour
{
	// -----------------------------
	// ① 보스 스폰 설정
	// -----------------------------
	public enum SpawnMode { Prefab, SceneObject }   // 스폰 방식
	[Header("① 보스 스폰")]
	[Tooltip("Prefab: 프리팹을 Instantiate. SceneObject: 씬에 미리 배치된 오브젝트를 활성화")]
	public SpawnMode spawnMode = SpawnMode.Prefab;

	[Tooltip("SpawnMode=Prefab 일 때 사용할 보스 외형 프리팹(스크립트 없음)")]
	public GameObject bossPrefab;

	[Tooltip("SpawnMode=SceneObject 일 때 활성화할 보스 외형 오브젝트(씬에 미리 배치, 시작 시 비활성 권장)")]
	public GameObject sceneBossActor;      // 루트 오브젝트

	[Tooltip("SceneObject 모드에서 지정 좌표로 이동할지 여부")]
	public bool moveSceneActorToSpawnPos = true;

	[Tooltip("보스 소환 월드 좌표")]
	public Vector3 bossWorldPos = new Vector3(8, 2, 0);

	// 내부 참조
	BossBase boss;               // 씬의 Controller 오브젝트에 붙은 BossBase
	Transform currentActor;      // 현재 보스 외형 루트(프리팹 인스턴스 또는 씬 오브젝트)

	// -----------------------------
	// ② 보스바 UI(Screen Space - Camera)
	// -----------------------------
	[Header("② 보스바 UI(Screen Space - Camera 캔버스)")]
	public RectTransform bossBarRoot;
	public Slider hpSlider;
	public TextMeshProUGUI hpText;
	public Image hpFill;
	public TextMeshProUGUI nameText;

	// -----------------------------
	// ③ 보스바 연출
	// -----------------------------
	[Header("③ 보스바 연출")]
	public float barAppearTime = 0.35f;
	public float chargeSeconds = 1.5f;
	public AudioClip sfxIntro;
	public ParticleSystem fxIntro;

	// -----------------------------
	// ④ BGM 제어
	// -----------------------------
	[Header("④ BGM")]
	public AudioSource bgmSource;    // 이 컨트롤러의 자식으로 생성됨
	public AudioClip bgmClip;
	public bool loopBgm = true;

	// ─────────────────────────────────────────────
	// ⑤ 보스전 시작 시 배경/카메라 연출(인스펙터용)
	// ─────────────────────────────────────────────
	[System.Serializable]
	public struct Tisiphone_BgTween
	{
		[Tooltip("변경 대상 배경(BackgroundUVScroller)")]
		public BackgroundUVScroller target;

		[Header("목표값")]
		[Tooltip("목표 UV 스크롤 속도(초당). X=가로, Y=세로")]
		public Vector2 uvSpeed;
		[Tooltip("목표 회전 속도(도/초)")]
		public float rotationSpeed;

		[Header("진행/복귀")]
		[Tooltip("현재→목표로 보간 시간(초). 0이면 즉시 적용")]
		public float lerpTime;
		[Tooltip("목표 상태 유지 시간(초). 0이면 유지 없이 즉시 다음 단계")]
		public float holdTime;
		[Tooltip("유지 후 원래 값으로 되돌릴지 여부")]
		public bool revert;
		[Tooltip("보간 이징(비워두면 선형)")]
		public AnimationCurve ease;
	}

	[System.Serializable]
	public struct Tisiphone_CamFx
	{
		[Tooltip("카메라 연출을 적용할 CameraEffects. 비우면 자동 탐색")]
		public CameraEffects cam;

		[Header("쉐이크")]
		public bool shake;
		public float shakeDuration;
		public float shakeAmplitude;
		public float shakeFrequency;

		[Header("줌")]
		public bool zoom;
		public float zoomSize;
		public float zoomTime;

		[Header("회전")]
		public bool rotate;
		public float rotateZ;
		public float rotateTime;

		[Header("자동 리셋")]
		[Tooltip("적용 후 자동으로 기본값으로 복귀할지")]
		public bool autoReset;
		[Tooltip("복귀까지 대기 시간(초)")]
		public float autoResetDelay;
		[Tooltip("복귀 보간 시간(초)")]
		public float resetEaseTime;
	}

	[Header("⑤ 보스전 시작 시 배경 연출 목록")]
	public Tisiphone_BgTween[] startBackgroundTweens;

	[Header("⑥ 보스전 시작 시 카메라 연출 목록")]
	public Tisiphone_CamFx[] startCameraEffects;

	[Header("⑦ 패턴 시작 제어")]
	[Tooltip("보스를 소환한 뒤 패턴을 자동으로 시작한다")]
	public bool startPatternsOnSpawn = true;

	[Tooltip("보스 소환 뒤 패턴을 시작하기 전 대기 시간(초)")]
	public float patternStartDelay = 5.0f;


	// 상태
	bool spawned = false;

	// ───────────────────────────────────────────────────────
	// 라이프사이클
	// ───────────────────────────────────────────────────────
	void Awake()
	{
		// 씬의 Controller 오브젝트에 존재하는 BossBase
		boss = GetComponent<BossBase>();

		// SceneObject 모드면 시작 시 안전하게 비활성화(실수 방지)
		if (spawnMode == SpawnMode.SceneObject && sceneBossActor)
			sceneBossActor.SetActive(false);
	}

	void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
		ResetSpawnGate(); // 에디터에서 단독 실행해도 항상 초기화
	}

	void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		if (bgmSource) bgmSource.Stop();   // 잔류 BGM 방지
	}

	void Update()
	{
		if (!spawned && Input.GetKeyDown(KeyCode.Q))
			SpawnBossOnce();
	}

	// ───────────────────────────────────────────────────────
	// 스폰 1회 처리
	// ───────────────────────────────────────────────────────
	void SpawnBossOnce()
	{
		if (spawned) return;
		spawned = true;

		if (!boss) { Debug.LogError("[BossSeq] 씬의 BossBase가 필요합니다."); spawned = false; return; }

		// 1) 외형(Actor) 확보
		switch (spawnMode)
		{
			case SpawnMode.Prefab:
				if (!bossPrefab) { Debug.LogError("[BossSeq] bossPrefab 미지정"); spawned = false; return; }
				var go = Instantiate(bossPrefab, bossWorldPos, Quaternion.identity);
				currentActor = go.transform;
				break;

			case SpawnMode.SceneObject:
				if (!sceneBossActor) { Debug.LogError("[BossSeq] sceneBossActor 미지정"); spawned = false; return; }
				currentActor = sceneBossActor.transform;
				if (moveSceneActorToSpawnPos) currentActor.position = bossWorldPos;
				sceneBossActor.SetActive(true);  // 여기서 활성화
				break;
		}

		// 2) BossBase에 '배우(Actor)' 바인딩
		//    - BindActor(Transform)가 있으면 호출
		//    - 없으면 자주 쓰는 필드명으로 세팅
		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var bind = boss.GetType().GetMethod("BindActor", flags);
		if (bind != null) bind.Invoke(boss, new object[] { currentActor });
		else
		{
			var f = boss.GetType().GetField("actor", flags)
				 ?? boss.GetType().GetField("actorRoot", flags)
				 ?? boss.GetType().GetField("modelRoot", flags);
			if (f != null) f.SetValue(boss, currentActor);
			else Debug.LogWarning("[BossSeq] BossBase에 BindActor/actor/actorRoot/modelRoot가 없습니다. 간단한 public void BindActor(Transform t) 추가 권장.");
		}

		// 3) 보스바 UI 자동 배선
		AutoWireBossBarUI();
		if (bossBarRoot) bossBarRoot.gameObject.SetActive(true);

		// 4) UI·연출 파라미터 전달(필드명이 존재할 때만 주입)
		TrySetField("bossBarRoot", bossBarRoot);
		TrySetField("hpSlider", hpSlider);
		TrySetField("hpText", hpText);
		TrySetField("hpFill", hpFill);
		TrySetField("nameTextTarget", nameText);
		TrySetField("barAnimTime", barAppearTime);

		// 5) 인트로 연출 + BGM 시작
		CallMethodIfExists("ShowBarWithCharge", chargeSeconds);
		StopAllLoopingBgm(null);
		StartOrSwapBgm(bgmClip, loopBgm);
		if (sfxIntro) AudioSource.PlayClipAtPoint(sfxIntro, Camera.main ? Camera.main.transform.position : transform.position, 1f);
		if (fxIntro) Instantiate(fxIntro, currentActor.position, Quaternion.identity);

		// 6) 이벤트 구독(중복 방지)
		boss.OnBgmSwapRequest -= HandleBgmSwap;
		boss.OnBgmSwapRequest += HandleBgmSwap;
		boss.OnBossDie -= HandleBossDie;
		boss.OnBossDie += HandleBossDie;

		// ---- 로컬 유틸(리플렉션 보조) ----
		void TrySetField(string name, object value)
		{
			var f2 = boss.GetType().GetField(name, flags);
			if (f2 != null) f2.SetValue(boss, value);
		}
		void CallMethodIfExists(string name, params object[] args)
		{
			var m2 = boss.GetType().GetMethod(name, flags);
			if (m2 != null) m2.Invoke(boss, args);
		}
		ApplyStartFx();  // Q로 시작 시 배경/카메라 연출 트리거
						 // 보스 소환 후 패턴 시작 신호(지연 포함)
		if (startPatternsOnSpawn)
			StartCoroutine(CoSignalPatternsAfterDelay());

	}

	// ───────────────────────────────────────────────────────
	// 보스바 자동 배선(이름 무관, BossBarMarker 사용)
	// ───────────────────────────────────────────────────────
	void AutoWireBossBarUI()
	{
#if UNITY_2023_1_OR_NEWER
		var marker = UnityEngine.Object.FindFirstObjectByType<BossBarMarker>(FindObjectsInactive.Include);
#else
#pragma warning disable CS0618
        var marker = UnityEngine.Object.FindObjectOfType<BossBarMarker>();
#pragma warning restore CS0618
#endif
		if (marker) bossBarRoot = marker.GetComponent<RectTransform>();

		if (bossBarRoot)
		{
			if (!hpSlider) hpSlider = bossBarRoot.GetComponentInChildren<Slider>(true);
			if (!hpFill && hpSlider && hpSlider.fillRect) hpFill = hpSlider.fillRect.GetComponent<Image>();

			if (!hpText || !nameText)
			{
				var texts = bossBarRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
				if (!hpText && texts.Length > 0) hpText = texts[0];
				if (!nameText && texts.Length > 1) nameText = texts[1];
			}
		}

		if (!bossBarRoot || !hpSlider)
			Debug.LogError("[BossSeq] BossBar UI 미설정. BossBarMarker를 보스바 루트에 붙여라.");
	}

	// ───────────────────────────────────────────────────────
	// BGM 유틸
	// ───────────────────────────────────────────────────────
	void StopAllLoopingBgm(AudioSource except)
	{
		var list = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
		foreach (var a in list)
		{
			if (!a || a == except) continue;
			bool looksLikeBgm =
				a.loop
				|| (a.outputAudioMixerGroup != null && a.outputAudioMixerGroup.name.Contains("BGM"))
				|| a.gameObject.name.Contains("[BGM]");
			if (looksLikeBgm) a.Stop();
		}
	}

	void StartOrSwapBgm(AudioClip clip, bool loop)
	{
		if (!clip) return;

		if (!bgmSource)
		{
			var go = new GameObject("BGM_Source");
			go.transform.SetParent(transform, false); // 씬 전환 시 함께 파괴
			bgmSource = go.AddComponent<AudioSource>();
			bgmSource.spatialBlend = 0f;
			bgmSource.playOnAwake = false;
		}

		StopAllLoopingBgm(except: bgmSource);

		bgmSource.loop = loop;
		bgmSource.clip = clip;
		bgmSource.Stop();
		bgmSource.Play();
	}

	private void HandleBgmSwap(AudioClip clip, bool loop)
	{
		StartOrSwapBgm(clip, loop);
	}

	private void HandleBossDie(BossBase _)
	{
		if (bgmSource && bgmSource.isPlaying) bgmSource.Stop();
		// 같은 씬에서 재도전까지 허용하고 싶으면 아래 주석 해제
		// spawned = false;
		// if (spawnMode == SpawnMode.SceneObject && sceneBossActor) sceneBossActor.SetActive(false);
	}

	// 배경 보간 코루틴
	System.Collections.IEnumerator CoApplyBg(Tisiphone_BgTween t)
	{
		if (!t.target) yield break;

		// 원래 값 보관
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
				e += Time.unscaledDeltaTime;
				float k = Mathf.Clamp01(e / dur);
				if (t.ease != null) k = t.ease.Evaluate(k);

				// ★ BackgroundUVScroller의 공개 필드만 변경 (uvSpeed/rotationSpeed) :contentReference[oaicite:3]{index=3}
				t.target.uvSpeed = Vector2.LerpUnclamped(fromSpeed, t.uvSpeed, k);
				t.target.rotationSpeed = Mathf.LerpUnclamped(fromRot, t.rotationSpeed, k);
				yield return null;
			}
			t.target.uvSpeed = t.uvSpeed;
			t.target.rotationSpeed = t.rotationSpeed;
		}

		// 유지
		if (t.holdTime > 0f)
			yield return new WaitForSecondsRealtime(t.holdTime);

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
					if (t.ease != null) k = t.ease.Evaluate(k);
					t.target.uvSpeed = Vector2.LerpUnclamped(t.uvSpeed, fromSpeed, k);
					t.target.rotationSpeed = Mathf.LerpUnclamped(t.rotationSpeed, fromRot, k);
					yield return null;
				}
				t.target.uvSpeed = fromSpeed;
				t.target.rotationSpeed = fromRot;
			}
		}
	}

	CameraEffects GetCam(CameraEffects prefer)
	{
#if UNITY_2023_1_OR_NEWER
    return prefer ? prefer : UnityEngine.Object.FindFirstObjectByType<CameraEffects>(FindObjectsInactive.Include);
#else
#pragma warning disable CS0618
		return prefer ? prefer : UnityEngine.Object.FindObjectOfType<CameraEffects>();
#pragma warning restore CS0618
#endif
	}

	void PlayCamFx(Tisiphone_CamFx fx)
	{
		var cam = GetCam(fx.cam);
		if (!cam) return;

		// ★ CameraEffects API 사용: Shake/ZoomTo/RotateTo/ResetAll :contentReference[oaicite:4]{index=4}
		if (fx.shake)
			cam.Shake(fx.shakeDuration, fx.shakeAmplitude, fx.shakeFrequency);
		if (fx.zoom)
			cam.ZoomTo(fx.zoomSize, fx.zoomTime);
		if (fx.rotate)
			cam.RotateTo(fx.rotateZ, fx.rotateTime);

		if (fx.autoReset)
			StartCoroutine(CoCamAutoReset(cam, Mathf.Max(0f, fx.autoResetDelay), Mathf.Max(0f, fx.resetEaseTime)));
	}

	System.Collections.IEnumerator CoCamAutoReset(CameraEffects cam, float delay, float ease)
	{
		if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
		cam.ResetAll(ease);
	}
	// 보스 소환 후 n초 대기→해당 보스에 달린 Pattern들 시작
	System.Collections.IEnumerator CoSignalPatternsAfterDelay()
	{
		if (patternStartDelay > 0f)
			yield return new WaitForSeconds(patternStartDelay);

		// 현재 소환된 외형 아래에서 Pattern을 찾는다
		Pattern[] list = null;
		if (currentActor) list = currentActor.GetComponentsInChildren<Pattern>(true);

		// 없으면 컨트롤러 자신에서 시도(프로젝트 구조 유연성)
		if (list == null || list.Length == 0)
			list = GetComponentsInChildren<Pattern>(true);

		foreach (var p in list)
		{
			// 외부 신호를 기다리도록 되어 있다면 그 경로 사용
			// 아니라면 직접 시작
			p.autoStart = false; // Start()에서 먼저 돌지 않게 방지
			if (p.waitForSpawnSignal) p.SignalBossSpawned();
			else p.StartPatterns();
		}
	}


	void ApplyStartFx()
	{
		if (startBackgroundTweens != null)
			foreach (var t in startBackgroundTweens)
				StartCoroutine(CoApplyBg(t));

		if (startCameraEffects != null)
			foreach (var c in startCameraEffects)
				PlayCamFx(c);
	}

	// ───────────────────────────────────────────────────────
	// 씬 전환 시 초기화(Q 재허용 + 잔여 오브젝트 정리)
	// ───────────────────────────────────────────────────────
	void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResetSpawnGate();
	}

	void ResetSpawnGate()
	{
		spawned = false;

		// 이전 스폰 외형 정리
		if (currentActor)
		{
			// SceneObject 모드면 파괴가 아니라 비활성화
			if (spawnMode == SpawnMode.SceneObject && sceneBossActor && currentActor == sceneBossActor.transform)
			{
				sceneBossActor.SetActive(false);
			}
			else
			{
				Destroy(currentActor.gameObject);
			}
			currentActor = null;
		}
	}
}
