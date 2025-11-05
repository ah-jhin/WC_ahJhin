// NextScenes.cs
// 유니티 6(= 2023 LTS 계열) 대응. 씬 전환 + 페이드 + 보스/플레이어 사망 규칙 처리.
// 사용 요약
// 1) 빈 오브젝트에 본 스크립트 부착.
// 2) [Boss] 슬롯에 씬의 BossBase 할당(없으면 자동 탐색 시도).
// 3) [Scene Names]에 nextSceneName, menuSceneName 설정.
// 4) [Fade]에 화면 전체를 덮는 CanvasGroup 할당. 없으면 런타임에 자동 생성.
// 5) 플레이어 사망 시, 플레이어 스크립트에서 NextScenes.OnPlayerDied()를 1회 호출.
// 6) R 키는 언제든 메뉴로 돌아감.
//
// 주의: 모든 메서드/필드에 // 주석으로 간단 설명 포함.

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // 자동 생성되는 페이드용 Image에 필요

[AddComponentMenu("Game/NextScenes")]
public class NextScenes : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    [Header("Scene Names")]
    [Tooltip("보스 격파 후 이동할 다음 씬 이름")]
    [SerializeField] private string nextSceneName = "stage_2";

    [Tooltip("언제든 R 키로 이동할 메뉴 씬 이름")]
    [SerializeField] private string menuSceneName = "menu";

    // ─────────────────────────────────────────────────────────
    [Header("Boss")]
    [Tooltip("씬에 배치된 BossBase. 비워두면 자동 탐색")]
    [SerializeField] private BossBase boss;

    [Tooltip("보스 사망 후 자동 이동까지 대기 시간(초)")]
    [SerializeField] private float bossAutoDelay = 5f;

    // ─────────────────────────────────────────────────────────
    [Header("Player")]
    [Tooltip("플레이어가 사망하면 OnPlayerDied()를 외부에서 호출해야 함")]
    [SerializeField] private KeyCode menuKey = KeyCode.R; // 항상 허용

    // ─────────────────────────────────────────────────────────
    [Header("Fade")]
    [Tooltip("화면 전체를 덮는 CanvasGroup. 없으면 런타임에 자동 생성됨")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Tooltip("페이드 시간(초). 어두워짐/밝아짐 동일 시간 사용")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("씬 시작 시 검은 화면에서 서서히 밝아지기")]
    [SerializeField] private bool fadeInOnSceneStart = true;

    [Tooltip("Time.timeScale과 무관하게 페이드할지 여부")]
    [SerializeField] private bool useUnscaledTime = true;

    // ─────────────────────────────────────────────────────────
    // 내부 상태
    private Coroutine pendingAutoLoad; // 보스 사망 후 5초 대기 코루틴
    private bool isLoading;            // 중복 로드 방지 플래그
    private static NextScenes _inst;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        // 중복 방지 + 씬 전환 중에도 객체 유지
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        DontDestroyOnLoad(gameObject);

        // 페이드 캔버스가 없으면 자동 생성
        if (!fadeCanvasGroup)
            fadeCanvasGroup = FindOrCreateFullScreenFader();

        // 시작 시 알파 초기화
        if (fadeCanvasGroup)
        {
            // 씬 시작 시 페이드 인을 원하면 처음에 검정(α=1)에서 시작
            fadeCanvasGroup.alpha = fadeInOnSceneStart ? 1f : 0f;
            fadeCanvasGroup.blocksRaycasts = true; // 전환 중 입력 차단
            fadeCanvasGroup.interactable = false;
        }
    }

    private void OnEnable()
    {
        // BossBase 자동 탐색(Inspector가 비었을 때만)
        if (!boss)
        {
#if UNITY_2023_1_OR_NEWER
            boss = FindFirstObjectByType<BossBase>();
#else
            boss = FindObjectOfType<BossBase>();
#endif
        }

        // 보스 이벤트 구독: 보스 사망 → 다음 씬 자동 이동 예약
        if (boss != null)
            boss.OnBossDie += OnBossDied;
    }

    private void Start()
    {
        // 씬 시작 시 페이드 인
        if (fadeCanvasGroup && fadeInOnSceneStart)
            StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));
    }

    private void OnDisable()
    {
        if (boss != null)
            boss.OnBossDie -= OnBossDied;
    }

    private void Update()
    {
        // 언제든 R 키로 메뉴로 이동
        if (Input.GetKeyDown(menuKey))
        {
            if (GameScore.I) GameScore.I.ResetScore();          // 점수 초기화
            StartSceneLoad(menuSceneName);                      // 반드시 코루틴 경로로
        }
    }

    // ─────────────────────────────────────────────────────────
    // [보스 사망 처리] : 5초 후 자동 이동 예약
    private void OnBossDied(BossBase _)
    {
        // 이미 로딩 중이면 무시
        if (isLoading) return;

        // 기존 예약 취소 후 다시 예약
        if (pendingAutoLoad != null)
            StopCoroutine(pendingAutoLoad);

        pendingAutoLoad = StartCoroutine(CoWaitAndGoNext(bossAutoDelay));
    }

    // 외부(플레이어 스크립트)에서 호출: 플레이어 사망 시 자동 이동 즉시 취소
    public void OnPlayerDied()
    {
        // 보스 사망으로 잡아둔 예약을 해제
        if (pendingAutoLoad != null)
        {
            StopCoroutine(pendingAutoLoad);
            pendingAutoLoad = null;
        }
        // 이후 R 키로 메뉴 이동만 허용(요구사항: 언제든 R로 메뉴)
    }

    // ─────────────────────────────────────────────────────────
    // [수동 호출용] 인스펙터 버튼이 필요하면 ContextMenu를 사용해도 됨
    public void GoNext()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            StartSceneLoad(nextSceneName);
    }

    public void GoMenu()
    {
        if (!string.IsNullOrEmpty(menuSceneName))
            StartSceneLoad(menuSceneName);
    }

    // ─────────────────────────────────────────────────────────
    // 내부: 일정 시간 대기 후 다음 씬 로드
    private IEnumerator CoWaitAndGoNext(float seconds)
    {
        // 경과 대기: unscaled / scaled 중 선택
        float t = 0f;
        while (t < seconds)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        pendingAutoLoad = null;
        GoNext();
    }

    // 씬 로드 시작(중복 방지)
    private void StartSceneLoad(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(CoLoadSceneWithFade(sceneName));
    }

    // 페이드 → 로드 → 페이드인
    private IEnumerator CoLoadSceneWithFade(string sceneName)
    {
        isLoading = true;

        // 1) 어두워짐
        if (fadeCanvasGroup)
            yield return FadeRoutine(fadeCanvasGroup.alpha, 1f, fadeDuration);

        // 2) 타임스케일 초기화(혹시 멈춰있을 수 있으므로)
        Time.timeScale = 1f;

        // 3) 씬 로드
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone)
            yield return null;

        // 4) 새 씬에서 페이드 컴포넌트 다시 찾기(씬이 바뀌었으므로)
#if UNITY_2023_1_OR_NEWER
        fadeCanvasGroup = FindFirstObjectByType<CanvasGroup>(FindObjectsInactive.Exclude);
#else
        fadeCanvasGroup = FindObjectOfType<CanvasGroup>();
#endif
        if (!fadeCanvasGroup)
            fadeCanvasGroup = FindOrCreateFullScreenFader();

        // 5) 밝아짐
        if (fadeCanvasGroup)
        {
            // 새 씬 시작 직후에는 α=1로 가정하고 0으로 페이드
            fadeCanvasGroup.alpha = 1f;
            yield return FadeRoutine(1f, 0f, fadeDuration);
        }

        isLoading = false;
    }

    // α 보간 코루틴
    private IEnumerator FadeRoutine(float from, float to, float dur)
    {
        if (!fadeCanvasGroup)
            yield break;

        // 입력 차단
        fadeCanvasGroup.blocksRaycasts = true;

        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        fadeCanvasGroup.alpha = from;

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;

        // 완전히 투명해지면 입력 허용
        if (Mathf.Approximately(to, 0f))
            fadeCanvasGroup.blocksRaycasts = false;
    }

    // 전체 화면을 덮는 페이더를 자동 생성
    private CanvasGroup FindOrCreateFullScreenFader()
    {
        // 1) 씬에 존재하는 CanvasGroup 중, 화면 전체를 덮는 것을 우선 검색
#if UNITY_2023_1_OR_NEWER
        var all = FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<CanvasGroup>(true);
#endif
        foreach (var cg in all)
        {
            if (cg.name.ToLower().Contains("fader") || cg.name.ToLower().Contains("screenfader"))
                return cg;
        }

        // 2) 없으면 생성
        var root = new GameObject("ScreenFader_Auto");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 카메라 설정과 무관하게 항상 위
        canvas.sortingOrder = short.MaxValue;              // 최상단

        // 이벤트 차단용
        root.AddComponent<GraphicRaycaster>();

        // 풀스크린 이미지 + 캔버스 그룹
        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(root.transform, false);
        var img = imgGO.AddComponent<Image>();
        img.raycastTarget = true;
        img.color = Color.black;

        var rect = img.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var cgNew = imgGO.AddComponent<CanvasGroup>();
        cgNew.alpha = 1f;              // 기본 검정
        cgNew.blocksRaycasts = true;   // 전환 중 입력 차단

        return cgNew;
    }
}
