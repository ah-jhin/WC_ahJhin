// Assets/Scripts/Loading/SceneLoader.cs
// 기능: '하나의 정적 호출'로 씬 전환 + 자동 페이드. App 씬 불필요.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    // 정적 진입점: 어디서든 SceneLoader.Load("stage_1");
    public static void Load(string sceneName, float fadeOut = 0.5f, float fadeIn = 0.25f, bool stopAllAudio = true)
    {
        // 실행자 생성(DontDestroyOnLoad)
        var go = new GameObject("SceneLoader");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneLoader>().StartCoroutine(go.GetComponent<SceneLoader>()
            .LoadRoutine(sceneName, fadeOut, fadeIn, stopAllAudio));
    }

    // 전환 코루틴
    private IEnumerator LoadRoutine(string sceneName, float fadeOut, float fadeIn, bool stopAllAudio)
    {
        // 1) 로딩용 전체화면 캔버스 즉석 생성(프리팹 불필요)
        var canvasGO = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGO);
        var cv = canvasGO.GetComponent<Canvas>();
        var csv = canvasGO.GetComponent<CanvasScaler>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; // 카메라와 독립
        cv.sortingOrder = 10000;                       // 항상 맨 위
        csv.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csv.referenceResolution = new Vector2(1920, 1080);
        csv.matchWidthOrHeight = 0.5f;

        // 검은 패널 + Fader
        var imgGO = new GameObject("Black", typeof(Image), typeof(CanvasGroup), typeof(Fader));
        imgGO.transform.SetParent(canvasGO.transform, false);
        var img = imgGO.GetComponent<Image>();
        img.color = Color.black;
        var fader = imgGO.GetComponent<Fader>();
        fader.initialAlpha = 0f;                       // 시작은 투명

        // 2) 페이드 아웃
        yield return fader.FadeTo(1f, fadeOut);

        // 3) 필요 시 모든 오디오 정지(간단하고 확실)
        if (stopAllAudio)
            foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                a.Stop();

        // 4) 비동기 로드(단일 모드). 복잡한 0.9 대기 없이 바로 활성화.
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone) yield return null;          // 로딩 중 프레임 유지

        // 5) 새 씬 한 프레임 안정화 후 페이드 인
        yield return null;
        yield return fader.FadeTo(0f, fadeIn);

        // 6) 정리
        Destroy(canvasGO);
        Destroy(gameObject);
    }
}
