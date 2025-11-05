// SimpleSceneLoader.cs
// 동기 전환(간단/확실): 화면 암전 → LoadScene → 밝게.
// 프리팹·설정 불필요. 메뉴에서 한 줄로 호출.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SimpleSceneLoader : MonoBehaviour
{
    /// <summary>
    /// 어디서든 호출: SimpleSceneLoader.Load("stage_1", 0.3f, 0.2f, true);
    /// </summary>
    public static void Load(string sceneName, float fadeOut = 0.3f, float fadeIn = 0.2f, bool stopAllAudio = true)
    {
        var runnerGO = new GameObject("SimpleSceneLoader_Runner");
        Object.DontDestroyOnLoad(runnerGO);
        var runner = runnerGO.AddComponent<SimpleSceneLoader>();
        runner.StartCoroutine(runner.LoadRoutine(sceneName, fadeOut, fadeIn, stopAllAudio));
    }

    private IEnumerator LoadRoutine(string sceneName, float fadeOut, float fadeIn, bool stopAllAudio)
    {
        // 1) 최상단 Overlay 캔버스 즉석 생성(카메라 의존 0)
        var canvasGO = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(canvasGO);
        var cv = canvasGO.GetComponent<Canvas>();
        var cs = canvasGO.GetComponent<CanvasScaler>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 10000;
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;

        // 2) 전체화면 검정 이미지 + CanvasGroup
        var imgGO = new GameObject("Black", typeof(Image), typeof(CanvasGroup));
        imgGO.transform.SetParent(canvasGO.transform, false);
        var img = imgGO.GetComponent<Image>();
        img.color = Color.black;

        var rt = (RectTransform)imgGO.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var cg = imgGO.GetComponent<CanvasGroup>();
        cg.alpha = 0f;               // 시작: 투명
        cg.blocksRaycasts = true;    // 로딩 중 입력 차단

        // 3) 페이드 아웃
        yield return Fade(cg, 1f, fadeOut);

        // 4) 모든 오디오 정지(옵션)
        if (stopAllAudio)
            foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) a.Stop();

        // 5) 동기 로드(간단/확실)
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

        // 6) 한 프레임 안정화 후 페이드 인
        yield return null;
        yield return Fade(cg, 0f, fadeIn);

        // 7) 정리
        Destroy(canvasGO);
        Destroy(gameObject);
    }

    // CanvasGroup 알파 보간(타임스케일 무시)
    private IEnumerator Fade(CanvasGroup cg, float target, float duration)
    {
        target = Mathf.Clamp01(target);
        if (duration <= 0f) { cg.alpha = target; yield break; }
        float start = cg.alpha, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        cg.alpha = target;
    }
}
