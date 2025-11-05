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
