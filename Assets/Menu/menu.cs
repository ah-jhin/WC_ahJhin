// Assets/Scripts/UI/Menu.cs
// 역할: 키보드 전용 메뉴. 화살표/W,S로 이동, Z로 확정.
// “게임 시작” 선택 시: 2.5초 페이드 → 4.5초 시점에 지정 씬 로드.
// “더미1~4”: 지정 SFX만 재생. “게임 종료”: 즉시 종료.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public enum MenuAction
{
	Start,   // 게임 시작
	Dummy,   // 더미 SFX
	Quit     // 게임 종료
}

public class Menu : MonoBehaviour
{
	[Header("UI 연결")]
	[Tooltip("메뉴 항목 텍스트 배열(씬에 배치한 순서대로 연결)")]
	public TextMeshProUGUI[] items; // 0:게임 시작, 1~4:더미, 5:게임 종료

	[Header("선택 색상")]
	public Color normalColor = Color.gray; // 비선택 색
	public Color highlightColor = Color.white; // 선택 색

	[Header("입력 설정(키보드 전용)")]
	[Tooltip("이동 입력 쿨다운(중복 스킵 방지)")]
	public float moveCooldown = 0.15f;

	[Header("씬 이동 설정")]
	[Tooltip("게임 시작 시 로드할 씬 이름(빌드 세팅에 등록 필수)")]
	public string startSceneName = "Stage_1"; // <— 여기 수정: 실제 1스테이지 씬 이름과 동일하게
	[Tooltip("페이드 아웃 시간(초). 문서 요구: 2.5초")]
	public float fadeOutDuration = 2.5f;
	[Tooltip("확정 후 씬 전환 수행 절대 시각(초). 문서 요구: 4.5초")]
	public float absoluteLoadAt = 4.5f;

	[Header("오디오")]
	[Tooltip("메뉴 이동(위/아래) SFX")]
	public AudioClip sfxMove;
	[Tooltip("확정(Z) SFX")]
	public AudioClip sfxConfirm;
	[Tooltip("더미 선택 시 재생할 SFX")]
	public AudioClip sfxDummy;

	[Header("BGM")]
	[Tooltip("메뉴 배경음. Awake에서 자동 재생, 게임 시작 시 즉시 정지")]
	public AudioClip bgmClip;              // 인스펙터에 BGM 연결
	[Range(0f, 1f)] public float bgmVolume = 0.6f; // 기본 음량
	private AudioSource _bgm;              // BGM 전용 AudioSource

	[Header("카메라 이동(선택 시 이동)")]
	[Tooltip("이동시킬 카메라 변환. 비우면 Camera.main 사용")]
	public Transform cameraTransform;      // 이동 대상 카메라
	[Tooltip("각 메뉴 아이템별 카메라 타겟 좌표(아이템 개수와 동일 길이 권장)")]
	public Vector3[] cameraTargets;        // 예: [0]=(0,0,-10), [1]=(10,0,-10) 등
	[Tooltip("카메라 이동 시간(초). 0이면 즉시 이동")]
	public float cameraMoveDuration = 0.25f;

	[Header("항목 동작 매핑")]
	[Tooltip("각 메뉴 항목의 동작 타입. items와 인덱스 1:1 매칭")]
	public MenuAction[] itemActions; // ← 인스펙터에서 items 길이와 동일하게 설정


	// 내부 상태
	private int _index;              // 현재 선택 인덱스
	private float _lastMoveTime;     // 최근 이동 입력 시각
	private AudioSource _audio;      // 효과음 출력
	private Fader _fader;            // 화면 페이드 제어
	private bool _locked;            // 확정 후 입력 잠금
	private Vector3 _camDefault;           // 원점 좌표(기본값 0,0,현재Z)
	private Coroutine _camMoveCo;          // 이동 코루틴 핸들

	void Awake()
	{
		_audio = GetComponent<AudioSource>();          // AudioSource 캐시				   
		_fader = Object.FindFirstObjectByType<Fader>(FindObjectsInactive.Include);
														// 씬 내 Fader 검색(Panel+CanvasGroup)

		// BGM AudioSource 생성 및 재생
		if (bgmClip != null)
		{
			_bgm = gameObject.AddComponent<AudioSource>(); // SFX와 분리된 전용 채널
			_bgm.clip = bgmClip;
			_bgm.loop = true;
			_bgm.playOnAwake = false;
			_bgm.volume = bgmVolume;
			_bgm.ignoreListenerPause = true;               // 메뉴 일시정지 무시
			_bgm.Play();                                   // 메뉴 입장과 동시에 재생
		}
		// 카메라 참조 및 기본 좌표 기록
		if (cameraTransform == null)
		{
			if (Camera.main != null) cameraTransform = Camera.main.transform;
		}
		if (cameraTransform != null)
		{
			// 기본 원점: (0,0,현재 Z). 요구사항: ESC/Q 시 (0,0)로 복귀
			_camDefault = new Vector3(0f, 0f, cameraTransform.position.z);
			// 시작 시 현재 선택 인덱스 타겟으로 1회 정렬
			SnapCameraToTarget(_index);
		}

		if (_fader == null)
		{
			Debug.LogError("Fader가 씬에 없습니다. Panel에 CanvasGroup+Fader를 추가하세요.");
		}
		ApplyHighlight();                               // 초기 하이라이트 반영
		Cursor.visible = false;                         // 마우스 사용 지양: 커서 숨김
		Cursor.lockState = CursorLockMode.Locked;       // 의도치 않은 마우스 포커스 방지

		// items 길이에 맞춰 itemActions 자동 리사이즈(부족분은 Dummy로 채움)
		if (items != null && (itemActions == null || itemActions.Length != items.Length))
		{
			var old = itemActions;
			itemActions = new MenuAction[items.Length];
			for (int i = 0; i < itemActions.Length; i++)
				itemActions[i] = (old != null && i < old.Length) ? old[i] : MenuAction.Dummy;
		}

		// [안전] 배열 길이 점검: 길이가 다르면 콘솔 경고
		if (items != null && itemActions != null && items.Length != itemActions.Length)
            Debug.LogWarning("items 길이와 itemActions 길이가 다릅니다. 인스펙터에서 맞추세요.");
    }

	void Update()
	{
		if (_locked) return; // 확정 이후엔 아무 입력 무시

		// ↑/W
		if ((Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) && CanMove())
		{
			_index = (_index - 1 + items.Length) % items.Length;
			AfterMove();
		}

		// ↓/S
		if ((Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) && CanMove())
		{
			_index = (_index + 1) % items.Length;
			AfterMove();
		}

		// ESC/Q: 카메라 원점 복귀
		if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q))
		{
			MoveCameraTo(_camDefault); // (0,0,현재Z)로 복귀
		}

		// 확정: Z
		if (Input.GetKeyDown(KeyCode.Z))
		{
			ConfirmSelection();
		}
	}

	/// <summary>이동 입력 쿨다운 체크</summary>
	private bool CanMove()
	{
		if (Time.unscaledTime - _lastMoveTime < moveCooldown) return false;
		_lastMoveTime = Time.unscaledTime;
		return true;
	}

	/// <summary>이동 후 하이라이트 및 SFX</summary>
	private void AfterMove()
	{
		ApplyHighlight();                       // 색상 갱신
		PlayOneShot(sfxMove);                   // 이동 효과음
		MoveCameraByIndex(_index);              // 선택 변경 시 카메라 이동
	}

	/// <summary>현재 인덱스에 맞춰 텍스트 색상 갱신</summary>
	private void ApplyHighlight()
	{
		for (int i = 0; i < items.Length; i++)
		{
			// 항목이 누락됐다면 안전하게 continue
			if (items[i] == null) continue;
			items[i].color = (i == _index) ? highlightColor : normalColor;
		}
	}

	/// <summary>Z 확정 처리</summary>
	private void ConfirmSelection()
	{
		PlayOneShot(sfxConfirm); // 확정음 재생

		// 인덱스에 따른 분기
		// 0: 게임 시작, 1~3: 더미, 4: 게임 종료
		if (_index == 0)
		{
			// 게임 시작 시 BGM 즉시 정지
			if (_bgm != null && _bgm.isPlaying) _bgm.Stop();

			// 문서 요구: 2.5초 페이드 → 4.5초에 씬 로드
			StartCoroutine(LoadGameSequence());
		}
		else if (_index >= 1 && _index <= 3)	// 3 = 0 1 2 3 4 5 6
		{
			// 더미: SFX만 재생(임시 기능)
			PlayOneShot(sfxDummy);
		}
		else
		{
			// 게임 종료
			Quit();
		}
	}

	/// <summary>게임 시작 시퀀스: 페이드→지정 시각에 씬 로드</summary>
	private IEnumerator LoadGameSequence()
	{
		_locked = true;                          // 입력 잠금
		float t0 = Time.unscaledTime;            // 시작 절대시간

		// 1) 페이드 아웃 2.5초 수행(검은 화면)
		if (_fader != null)
			yield return _fader.FadeTo(1f, fadeOutDuration);

		// 2) 절대 4.5초 시점까지 대기(문서의 “4.5초에 이동” 규칙 충족)
		float remain = (t0 + absoluteLoadAt) - Time.unscaledTime;
		if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

		// 3) 씬 로드
		if (string.IsNullOrWhiteSpace(startSceneName))
		{
			Debug.LogError("startSceneName이 비어 있습니다. 메뉴 인스펙터에서 씬 이름을 지정하세요.");
			_locked = false;
			yield break;
		}
		SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);

		// [검증] 없으면 중단하고 입력 잠금 해제
		if (!Application.CanStreamedLevelBeLoaded(startSceneName))
		{
			Debug.LogError($"Scene '{startSceneName}' 을(를) 로드할 수 없습니다. Build Profiles에 추가했는지 확인하세요.");
			_locked = false; // 멈춤 방지
			yield break;
		}

	}

	// ================= 카메라 이동 유틸 =================

	/// <summary>
	/// 현재 인덱스에 해당하는 타겟 좌표로 카메라 이동
	/// </summary>
	private void MoveCameraByIndex(int idx)
	{
		if (cameraTransform == null) return;

		// 배열 길이를 벗어나면 안전하게 기본 좌표로 이동
		Vector3 target = _camDefault;
		if (cameraTargets != null && idx >= 0 && idx < cameraTargets.Length)
		{
			// Z는 현재 카메라 Z 유지. 2D에서 중요.
			target = new Vector3(cameraTargets[idx].x, cameraTargets[idx].y, cameraTransform.position.z);
		}
		MoveCameraTo(target);
	}

	/// <summary>
	/// 카메라를 지정 좌표로 부드럽게 이동(시간 0이면 즉시 이동)
	/// </summary>
	private void MoveCameraTo(Vector3 target)
	{
		if (cameraTransform == null) return;

		// 진행 중 코루틴 중단
		if (_camMoveCo != null) StopCoroutine(_camMoveCo);
		_camMoveCo = StartCoroutine(CameraLerp(cameraTransform.position, target, cameraMoveDuration));
	}

	/// <summary>
	/// 즉시 스냅(초기 정렬용)
	/// </summary>
	private void SnapCameraToTarget(int idx)
	{
		if (cameraTransform == null) return;

		Vector3 pos = _camDefault;
		if (cameraTargets != null && idx >= 0 && idx < cameraTargets.Length)
			pos = new Vector3(cameraTargets[idx].x, cameraTargets[idx].y, cameraTransform.position.z);

		cameraTransform.position = pos; // 즉시 배치
	}

	/// <summary>
	/// 카메라 선형 보간 이동(메뉴는 시간 정지 무관, unscaled 사용)
	/// </summary>
	private IEnumerator CameraLerp(Vector3 from, Vector3 to, float duration)
	{
		if (duration <= 0f)
		{
			cameraTransform.position = to;
			yield break;
		}

		float t = 0f;
		while (t < duration)
		{
			t += Time.unscaledDeltaTime;
			float k = Mathf.Clamp01(t / duration);
			cameraTransform.position = Vector3.Lerp(from, to, k);
			yield return null;
		}
		cameraTransform.position = to;
	}

	/// <summary>프로그램 종료</summary>
	private void Quit()
		{
	#if UNITY_EDITOR
			// 에디터에서는 플레이 모드 종료
			UnityEditor.EditorApplication.isPlaying = false;
	#else
			Application.Quit();
	#endif
	}

	/// <summary>오디오 헬퍼: null이면 무시</summary>
	private void PlayOneShot(AudioClip clip)
	{
		if (_audio != null && clip != null)
			_audio.PlayOneShot(clip);
	}
}
