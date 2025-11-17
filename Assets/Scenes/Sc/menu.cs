// Assets/Scripts/UI/Menu.cs
// 역할: 키보드 메뉴. ↑/↓(또는 W/S) 이동, Z 확정.
// "게임 시작"은 SceneLoader로 동기 전환(암전→로드→밝게).
// 추가: 메인 메뉴에서 P 키로 '보스러시' 디버그 메뉴 진입.

using System.Collections;
using UnityEngine;
using TMPro;

public enum MenuAction
{
	Start,      // 게임 시작
	Dummy,      // 더미 SFX
	Quit        // 게임 종료
}

public class Menu : MonoBehaviour
{
	[Header("UI 연결")]
	[Tooltip("메뉴 항목 텍스트 배열(씬에 배치한 순서대로 연결)")]
	public TextMeshProUGUI[] items;

	[Header("선택 색상")]
	[Tooltip("일반(비선택) 상태 텍스트 색상")]
	public Color normalColor = Color.gray;      // 비선택 색
	[Tooltip("선택 상태 텍스트 색상")]
	public Color highlightColor = Color.white;  // 선택 색

	[Header("입력 설정")]
	[Tooltip("이동 입력 쿨다운(중복 입력 방지)")]
	public float moveCooldown = 0.15f;

	[Header("씬 이동 설정")]
	[Tooltip("게임 시작 시 로드할 씬 이름(빌드 세팅 등록 필수)")]
	public string startSceneName = "stage_1";

	[Header("오디오")]
	[Tooltip("메뉴 커서 이동 SFX")]
	public AudioClip sfxMove;       // 이동 SFX
	[Tooltip("메뉴 항목 확정 SFX")]
	public AudioClip sfxConfirm;    // 확정 SFX
	[Tooltip("Dummy 액션 SFX")]
	public AudioClip sfxDummy;      // 더미 SFX

	[Header("BGM")]
	[Tooltip("메뉴 배경음. Awake에서 자동 재생, 게임 시작 시 즉시 정지")]
	public AudioClip bgmClip;
	[Range(0f, 1f)] public float bgmVolume = 0.6f;
	private AudioSource _bgm;       // BGM 전용 AudioSource

	[Header("카메라 이동(선택 시 이동)")]
	[Tooltip("이동시킬 카메라. 비우면 Camera.main 사용")]
	public Transform cameraTransform;
	[Tooltip("각 항목별 카메라 타겟(X,Y). Z는 현재 카메라 Z 유지")]
	public Vector3[] cameraTargets;
	[Tooltip("카메라 이동 시간(초). 0이면 즉시 이동")]
	public float cameraMoveDuration = 0.25f;

	[Header("항목 동작 매핑")]
	[Tooltip("각 메뉴 항목의 동작 타입. items와 인덱스 1:1 매칭")]
	public MenuAction[] itemActions;

	// ─────────────────────────────────────
	// 보스 러시(디버그용)
	// ─────────────────────────────────────
	[Header("보스 러시(디버그용)")]
	[Tooltip("보스러시 선택 패널 루트(처음에는 비활성 추천)")]
	[SerializeField] private GameObject bossRushPanel;
	[Tooltip("보스러시 항목 텍스트들(위에서 아래 순서대로 연결)")]
	[SerializeField] private TextMeshProUGUI[] bossRushItems;
	[Tooltip("각 항목이 로드할 보스 씬 이름(배열 크기는 bossRushItems와 동일 권장)")]
	[SerializeField] private string[] bossRushSceneNames;
	[Tooltip("보스러시 비선택 색상")]
	[SerializeField] private Color bossRushNormalColor = Color.gray;
	[Tooltip("보스러시 선택 색상")]
	[SerializeField] private Color bossRushHighlightColor = Color.yellow;
	[Tooltip("보스러시 커서 이동 쿨다운(초)")]
	[SerializeField] private float bossRushMoveCooldown = 0.15f;

	// 내부 상태
	private int _index;                 // 현재 선택 인덱스
	private float _lastMoveTime;        // 최근 이동 입력 시각
	private AudioSource _audio;         // 효과음 출력
	private bool _locked;               // 확정 후 입력 잠금
	private Vector3 _camDefault;        // 카메라 기본 위치(0,0,현재Z)
	private Coroutine _camMoveCo;       // 카메라 이동 코루틴

	// 보스 러시 내부 상태
	private bool _bossRushActive = false;   // 보스러시 UI 활성화 여부
	private int _bossRushIndex = 0;         // 보스러시 선택 인덱스
	private float _bossRushLastMoveTime;    // 보스러시 최근 이동 입력 시간

	void Awake()
	{
		_audio = GetComponent<AudioSource>();

		// BGM 전용 채널 생성 및 재생
		if (bgmClip != null)
		{
			_bgm = gameObject.AddComponent<AudioSource>();
			_bgm.clip = bgmClip;
			_bgm.loop = true;
			_bgm.playOnAwake = false;
			_bgm.volume = bgmVolume;
			_bgm.ignoreListenerPause = true;
			_bgm.Play();
		}

		// 카메라 참조 및 기본 좌표 기록
		if (cameraTransform == null && Camera.main != null)
			cameraTransform = Camera.main.transform;

		if (cameraTransform != null)
		{
			_camDefault = new Vector3(0f, 0f, cameraTransform.position.z);
			SnapCameraToTarget(_index); // 시작 위치 정렬
		}

		// itemActions 길이 자동 보정(누락분은 Dummy)
		if (items != null && (itemActions == null || itemActions.Length != items.Length))
		{
			var old = itemActions;
			itemActions = new MenuAction[items.Length];
			for (int i = 0; i < itemActions.Length; i++)
				itemActions[i] = (old != null && i < old.Length) ? old[i] : MenuAction.Dummy;
		}

		ApplyHighlight();                 // 초기 하이라이트
		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;

		// 보스러시 패널은 시작 시 숨김
		if (bossRushPanel) bossRushPanel.SetActive(false);
	}

	void Update()
	{
		if (_locked) return;

		// ───── 보스러시 모드일 때는 별도 입력 처리 ─────
		if (_bossRushActive)
		{
			UpdateBossRushInput();
			return; // 일반 메뉴 입력은 무시
		}

		// ───── 일반 메뉴 입력 처리 ─────

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
			MoveCameraTo(_camDefault);

		// 확정: Z
		if (Input.GetKeyDown(KeyCode.Z))
			ConfirmSelection();

		// 보스러시: P 키로 디버그 보스 선택 메뉴 열기(메인 메뉴 한정)
		if (Input.GetKeyDown(KeyCode.P))
			TryOpenBossRush();
	}

	// 입력 쿨다운(일반 메뉴용)
	private bool CanMove()
	{
		if (Time.unscaledTime - _lastMoveTime < moveCooldown) return false;
		_lastMoveTime = Time.unscaledTime;
		return true;
	}

	// 이동 후 처리
	private void AfterMove()
	{
		ApplyHighlight();
		PlayOneShot(sfxMove);
		MoveCameraByIndex(_index);
	}

	// 하이라이트 색상 적용(일반 메뉴용)
	private void ApplyHighlight()
	{
		for (int i = 0; i < items.Length; i++)
		{
			if (items[i] == null) continue;
			items[i].color = (i == _index) ? highlightColor : normalColor;
		}
	}

	// Z 확정 처리(일반 메뉴용)
	private void ConfirmSelection()
	{
		PlayOneShot(sfxConfirm);

		// 현재 항목 동작 결정(없으면 Dummy)
		MenuAction action = (itemActions != null && _index >= 0 && _index < itemActions.Length)
			? itemActions[_index] : MenuAction.Dummy;

		switch (action)
		{
			case MenuAction.Start:
				if (_bgm != null && _bgm.isPlaying) _bgm.Stop();      // BGM 즉시 정지
				_locked = true;                                       // 입력 잠금
																	  // 기존 SimpleSceneLoader → 새 SceneLoader 사용
				SceneLoader.Load(startSceneName, 0.3f, 0.2f, true);   // 암전→로드→밝게
				break;

			case MenuAction.Quit:
#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
				break;

			case MenuAction.Dummy:
			default:
				PlayOneShot(sfxDummy);
				break;
		}
	}

	// 카메라 이동 유틸
	private void MoveCameraByIndex(int idx)
	{
		if (cameraTransform == null) return;

		Vector3 target = _camDefault;
		if (cameraTargets != null && idx >= 0 && idx < cameraTargets.Length)
			target = new Vector3(cameraTargets[idx].x, cameraTargets[idx].y, cameraTransform.position.z);

		MoveCameraTo(target);
	}

	private void MoveCameraTo(Vector3 target)
	{
		if (cameraTransform == null) return;
		if (_camMoveCo != null) StopCoroutine(_camMoveCo);
		_camMoveCo = StartCoroutine(CameraLerp(cameraTransform.position, target, cameraMoveDuration));
	}

	private void SnapCameraToTarget(int idx)
	{
		if (cameraTransform == null) return;

		Vector3 pos = _camDefault;
		if (cameraTargets != null && idx >= 0 && idx < cameraTargets.Length)
			pos = new Vector3(cameraTargets[idx].x, cameraTargets[idx].y, cameraTransform.position.z);

		cameraTransform.position = pos;
	}

	private IEnumerator CameraLerp(Vector3 from, Vector3 to, float duration)
	{
		if (duration <= 0f) { cameraTransform.position = to; yield break; }
		float t = 0f;
		while (t < duration)
		{
			t += Time.unscaledDeltaTime; // 메뉴는 시간 정지와 무관
			cameraTransform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
			yield return null;
		}
		cameraTransform.position = to;
	}

	// OneShot 헬퍼
	private void PlayOneShot(AudioClip clip)
	{
		if (_audio == null) _audio = GetComponent<AudioSource>();
		if (_audio != null && clip != null) _audio.PlayOneShot(clip);
	}

	// ─────────────────────────────────────────
	// 보스러시 관련 처리
	// ─────────────────────────────────────────

	// 보스러시 메뉴 열기(P 키)
	private void TryOpenBossRush()
	{
		// 최소한 패널과 항목이 있어야 한다.
		if (!bossRushPanel || bossRushItems == null || bossRushItems.Length == 0)
		{
			Debug.LogWarning("[Menu] BossRushPanel 또는 bossRushItems 가 비어 있어 보스러시를 열 수 없습니다.");
			return;
		}

		_bossRushActive = true;
		_bossRushIndex = 0;
		_bossRushLastMoveTime = 0f;

		bossRushPanel.SetActive(true);
		ApplyBossRushHighlight();
	}

	// 보스러시 메뉴 닫기(ESC)
	private void CloseBossRush()
	{
		_bossRushActive = false;
		if (bossRushPanel) bossRushPanel.SetActive(false);
	}

	// 보스러시 입력 쿨다운
	private bool CanMoveBossRush()
	{
		if (Time.unscaledTime - _bossRushLastMoveTime < bossRushMoveCooldown) return false;
		_bossRushLastMoveTime = Time.unscaledTime;
		return true;
	}

	// 보스러시 모드에서 입력 처리
	private void UpdateBossRushInput()
	{
		// ↑/W
		if ((Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) && CanMoveBossRush())
		{
			_bossRushIndex = (_bossRushIndex - 1 + bossRushItems.Length) % bossRushItems.Length;
			ApplyBossRushHighlight();
			PlayOneShot(sfxMove);
		}

		// ↓/S
		if ((Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) && CanMoveBossRush())
		{
			_bossRushIndex = (_bossRushIndex + 1) % bossRushItems.Length;
			ApplyBossRushHighlight();
			PlayOneShot(sfxMove);
		}

		// Z: 선택된 보스 씬으로 이동
		if (Input.GetKeyDown(KeyCode.Z))
		{
			string sceneName = null;
			if (bossRushSceneNames != null &&
				_bossRushIndex >= 0 && _bossRushIndex < bossRushSceneNames.Length)
			{
				sceneName = bossRushSceneNames[_bossRushIndex];
			}

			if (!string.IsNullOrEmpty(sceneName))
			{
				if (_bgm != null && _bgm.isPlaying) _bgm.Stop();
				_locked = true; // 씬 이동 중 추가 입력 방지
				SceneLoader.Load(sceneName, 0.3f, 0.2f, true);
			}
			else
			{
				Debug.LogWarning("[Menu] 선택된 보스러시 항목에 대응하는 씬 이름이 없습니다.");
				PlayOneShot(sfxDummy);
			}
		}

		// ESC: 보스러시 닫고 원래 메인 메뉴로 복귀
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			CloseBossRush();
		}
	}

	// 보스러시 UI 하이라이트 적용
	private void ApplyBossRushHighlight()
	{
		if (bossRushItems == null) return;

		for (int i = 0; i < bossRushItems.Length; i++)
		{
			var t = bossRushItems[i];
			if (!t) continue;

			t.color = (i == _bossRushIndex) ? bossRushHighlightColor : bossRushNormalColor;
		}
	}
}
