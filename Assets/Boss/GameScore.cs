using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환 시 HUD 재바인딩
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// 게임 점수(=누적 데미지)를 중앙에서 관리한다.
/// - DontDestroyOnLoad: 씬 이동해도 초기화되지 않음
/// - 태그 규칙:
///   Boss      : 가한 피해 만큼 +
///   WeakPoint : 가한 피해의 2배 +
///   Player    : 플레이어가 받은 피해의 2배 -
/// - HUD(UIHUD)와 자동 동기화
/// </summary>
public class GameScore : MonoBehaviour
{
    public static GameScore I; // 싱글톤

    [Header("Server Settings")]
    [SerializeField] private string endpoint = "http://localhost:8080/rank"; // 점수 전송용(선택)

    [Header("Score State")]
    // totalDamage = totalScore 로 사용. 기존 스크립트 호환을 위해 이름 유지.
    [Tooltip("게임 진행 중 누적 점수(씬 이동해도 유지)")]
    public int totalDamage = 0;

    // 내부 참조
    UIHUD _hud; // 씬마다 새로 찾는다

    void Awake()
    {
        // 싱글톤 + 유지
        if (I == null) { I = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        // 첫 HUD 바인딩 시도
        TryBindHUD();
    }

    void OnEnable()
    {
        // 씬 바뀔 때마다 HUD 다시 찾음
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        TryBindHUD();
        PushScoreToHUD();
    }

    /// <summary>현재 씬에서 UIHUD를 찾아 캐시</summary>
    void TryBindHUD()
    {
#if UNITY_2023_1_OR_NEWER
        _hud = FindFirstObjectByType<UIHUD>(FindObjectsInactive.Exclude);
#else
        _hud = FindObjectOfType<UIHUD>();
#endif
    }

    /// <summary>외부에서 HUD를 수동 주입하고 싶을 때 사용</summary>
    public void BindHUD(UIHUD hud)
    {
        _hud = hud;
        PushScoreToHUD();
    }

    /// <summary>HUD에 점수 반영</summary>
    void PushScoreToHUD()
    {
        if (_hud) _hud.SetScore(totalDamage);
    }

    // ================================
    // 점수 갱신 API 3종
    // ================================

    /// <summary>
    /// 플레이어가 '공격을 가했을 때' 호출.
    /// targetTag 기준으로 가산 규칙 적용.
    /// - Boss: +damage
    /// - WeakPoint: +(damage*2)
    /// </summary>
    /// 
    // === 호환용 래퍼(기존 코드 지원) ===
    // Boss에 가한 피해를 점수에 더함(구 코드: GameScore.I.AddDamage(dmg))
    public void AddDamage(int amount)
    {
        // Boss 태그로 처리 → +amount
        OnDealDamage("Boss", amount);
    }

    // 대상 태그를 명시하는 구 코드용(예: WeakPoint 등)
    public void AddDamage(string targetTag, int amount)
    {
        // "Boss"면 +amount, "WeakPoint"면 +amount*2
        OnDealDamage(targetTag, amount);
    }

    // 플레이어가 받은 피해를 점수에서 차감(구 코드: GameScore.I.AddPlayerDamage(dmg) 대비)
    public void AddPlayerDamage(int amount)
    {
        // Player 피격 규칙: -amount*2
        OnPlayerDamaged(amount);
    }

    public void OnDealDamage(string targetTag, int damage)
    {
        if (damage <= 0) return;

        // 태그에 따른 가산치 계산
        int delta = 0;
        if (targetTag == "Boss") delta = damage;
        else if (targetTag == "WeakPoint") delta = damage * 2;

        if (delta != 0)
        {
            totalDamage += delta;
            PushScoreToHUD();
        }
    }

    /// <summary>
    /// 플레이어가 '피격을 당했을 때' 호출.
    /// - Player: -(damage*2)
    /// </summary>
    public void OnPlayerDamaged(int damage)
    {
        if (damage <= 0) return;
        totalDamage -= (damage * 2);
        PushScoreToHUD();
    }

    /// <summary>
    /// 메뉴로 돌아갈 때 등 '게임 종료/중단 시점'에만 호출.
    /// 진행 중에는 절대 호출하지 말 것.
    /// </summary>
    public void ResetScore()
    {
        totalDamage = 0;
        PushScoreToHUD();
    }

    // ================================
    // 서버 전송(선택 기능)
    // ================================

    /// <summary>플레이어 사망 시 서버로 점수 전송(선택)</summary>
    public void OnPlayerDeath()
    {
        Debug.Log($"[GameScore] 플레이어 사망 - 총 점수 {totalDamage} 전송 중...");
        StartCoroutine(SendScoreToServer());
    }

    IEnumerator SendScoreToServer()
    {
        var payload = new RankPayload { score = totalDamage };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[GameScore] 전송 성공! 점수={totalDamage}");
            else
                Debug.LogError($"[GameScore] 전송 실패: {req.error}");
        }
    }

    [System.Serializable]
    private class RankPayload { public int score; }
}
