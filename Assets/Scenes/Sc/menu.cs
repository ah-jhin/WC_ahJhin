// Assets/Scripts/UI/Menu.cs
// 역할: 키보드 메뉴. ↑/↓(또는 W/S) 이동, Z 확정.
// "게임 시작"은 SimpleSceneLoader로 동기 전환(암전→로드→밝게).
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
    public Color normalColor = Color.gray;      // 비선택 색
    public Color highlightColor = Color.white;  // 선택 색

    [Header("입력 설정")]
    [Tooltip("이동 입력 쿨다운(중복 입력 방지)")]
    public float moveCooldown = 0.15f;

    [Header("씬 이동 설정")]
    [Tooltip("게임 시작 시 로드할 씬 이름(빌드 세팅 등록 필수)")]
    public string startSceneName = "stage_1";

    [Header("오디오")]
    public AudioClip sfxMove;       // 이동 SFX
    public AudioClip sfxConfirm;    // 확정 SFX
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

    // 내부 상태
    private int _index;                 // 현재 선택 인덱스
    private float _lastMoveTime;        // 최근 이동 입력 시각
    private AudioSource _audio;         // 효과음 출력
    private bool _locked;               // 확정 후 입력 잠금
    private Vector3 _camDefault;        // 카메라 기본 위치(0,0,현재Z)
    private Coroutine _camMoveCo;       // 카메라 이동 코루틴

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
    }

    void Update()
    {
        if (_locked) return;

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
    }

    // 입력 쿨다운
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

    // 하이라이트 색상 적용
    private void ApplyHighlight()
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            items[i].color = (i == _index) ? highlightColor : normalColor;
        }
    }

    // Z 확정 처리
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
                SimpleSceneLoader.Load(startSceneName, 0.3f, 0.2f, true); // 암전→로드→밝게
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
}
