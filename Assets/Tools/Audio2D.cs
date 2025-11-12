using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [전역 오디오 2D 강제 스크립트]
/// - 씬 전환에도 유지되도록 설계 (중복 생성 방지)
/// - 모든 AudioSource를 2D(SpatialBlend=0)로 강제
/// </summary>
[DefaultExecutionOrder(-1000)] // ★ 최대한 이른 실행
public class Audio2D : MonoBehaviour
{
	private static Audio2D _instance; // ★ 중복 방지용 싱글톤

	[Header("적용 대상")]
	[SerializeField] private bool enforceAllAudioSources = true;
	[SerializeField] private bool enforceOneShotAudio = true;

	[Header("세부 옵션(필요시만)")]
	[SerializeField] private bool zeroDoppler = true;
	[SerializeField] private bool centerPan = true;
	[SerializeField] private bool soften3DRolloff = true;

	[Header("수행 주기")]
	[SerializeField] private bool applyOnStart = true;
	[SerializeField] private bool applyOnSceneLoaded = true;
	[SerializeField] private float reapplyIntervalSeconds = 0.2f;

	[Header("수명")]
	[Tooltip("씬 전환 시에도 유지")]
	[SerializeField] private bool dontDestroyOnLoad = true;

	// 내부 캐시
	private readonly HashSet<AudioSource> _processed = new HashSet<AudioSource>();

	private void Awake()
	{
		// ★ 중복 인스턴스 방지: 이미 존재하면 자신을 제거하고 끝
		if (_instance != null && _instance != this)
		{
			Destroy(gameObject);
			return;
		}
		_instance = this;

		// ★ DontDestroyOnLoad는 루트 오브젝트만 허용됨
		//    부모가 있다면 먼저 루트로 탈착한 뒤 호출한다.
		if (dontDestroyOnLoad)
		{
			if (transform.parent != null)
			{
				// 두 번째 인자 true: 월드 좌표 유지
				transform.SetParent(null, true); // ★ 루트로 분리
			}
			DontDestroyOnLoad(gameObject); // ★ 이제 안전하게 유지 가능
		}

		if (applyOnSceneLoaded)
			SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void Start()
	{
		if (applyOnStart)
			Apply();

		if (reapplyIntervalSeconds > 0f)
			StartCoroutine(ReapplyRoutine());
	}

	private void OnDestroy()
	{
		if (_instance == this)
			_instance = null;

		if (applyOnSceneLoaded)
			SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		_processed.Clear();
		Apply();
	}

	/// <summary>
	/// 현재 씬의 모든 AudioSource에 2D 강제 적용
	/// </summary>
	private void Apply()
	{
		if (enforceAllAudioSources)
		{
			var all = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
			foreach (var a in all)
				Enforce2D(a);
		}

		if (enforceOneShotAudio)
			ForceOneShotAudiosNow();
	}

	/// <summary>
	/// 개별 AudioSource를 2D로 세팅
	/// </summary>
	private void Enforce2D(AudioSource a)
	{
		if (!a || _processed.Contains(a)) return;

		a.spatialBlend = 0f;         // ★ 2D 고정
		if (zeroDoppler) a.dopplerLevel = 0f;
		if (centerPan) a.panStereo = 0f;

		if (soften3DRolloff)
		{
			a.rolloffMode = AudioRolloffMode.Linear;
			a.minDistance = 0.1f;
			a.maxDistance = 5000f;
			a.spread = 0f;
			a.reverbZoneMix = 0f;
		}

		_processed.Add(a);
	}

	/// <summary>
	/// "One shot audio" 임시 오디오를 찾아 2D로 강제
	/// </summary>
	private void ForceOneShotAudiosNow()
	{
		var allAudio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
		foreach (var a in allAudio)
		{
			if (!a) continue;
			var go = a.gameObject;
			if (go && go.name == "One shot audio")
				Enforce2D(a);
		}
	}

	/// <summary>
	/// 주기적으로 새로 생긴 AudioSource/One shot audio를 스캔하여 재적용
	/// </summary>
	private IEnumerator ReapplyRoutine()
	{
		var wait = new WaitForSeconds(reapplyIntervalSeconds);
		while (true)
		{
			if (enforceAllAudioSources)
			{
				var all = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
				foreach (var a in all)
					Enforce2D(a);
			}

			if (enforceOneShotAudio)
				ForceOneShotAudiosNow();

			yield return wait;
		}
	}
}
