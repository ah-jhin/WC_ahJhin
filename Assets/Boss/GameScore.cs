using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// 플레이어의 누적 데미지(=점수)를 관리하고,
/// 플레이어 사망 시 서버로 최종 점수를 전송.
/// </summary>
public class GameScore : MonoBehaviour
{
    public static GameScore I;

    [Header("Server Settings")]
    [SerializeField] private string endpoint = "http://localhost:8080/rank"; // 스프링 컨트롤러 주소

    private int totalDamage = 0;  // 누적된 총 데미지(=점수)

    void Awake()
    {
        if (I == null) { I = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    /// <summary>
    /// 보스나 적에게 데미지를 입혔을 때 호출됨
    /// </summary>
    public void AddDamage(int dmg)
    {
        if (dmg <= 0) return;
        totalDamage += dmg;
    }

    /// <summary>
    /// 플레이어 사망 시점에서 호출
    /// </summary>
    public void OnPlayerDeath()
    {
        Debug.Log($"[GameScore] 플레이어 사망 - 총 데미지 {totalDamage} 전송 중...");
        StartCoroutine(SendScoreToServer());
    }

    /// <summary>
    /// 서버로 누적 데미지 전송
    /// </summary>
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
                Debug.Log($"[GameScore] 전송 성공! 총 데미지={totalDamage}");
            else
                Debug.LogError($"[GameScore] 전송 실패: {req.error}");
        }
    }

    [System.Serializable]
    private class RankPayload
    {
        public int score;
    }
}