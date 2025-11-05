using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;

public class GameScore : MonoBehaviour
{
	public static GameScore I;

	[Header("Server Settings")]
	[Tooltip("비워두면 현재 페이지 오리진 기준으로 자동 생성됩니다.")]
	[SerializeField] private string endpoint = "";

	[Header("Score State")]
	public int totalDamage = 0;

	UIHUD _hud;

	// 중복 방지 플래그
	bool _submitting = false; // 전송 중
	bool _submitted = false; // 이번 라운드에서 이미 전송 완료(or 시도)됨

	void Awake()
	{
		if (I == null) { I = this; DontDestroyOnLoad(gameObject); }
		else { Destroy(gameObject); return; }

		if (string.IsNullOrEmpty(endpoint))
			endpoint = BuildEndpoint();

		Debug.Log($"[GameScore] endpoint = {endpoint}");
		TryBindHUD();
	}

	void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
	void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

	void OnSceneLoaded(Scene s, LoadSceneMode m)
	{
		TryBindHUD();
		PushScoreToHUD();
	}

	void TryBindHUD()
	{
#if UNITY_2023_1_OR_NEWER
		_hud = FindFirstObjectByType<UIHUD>(FindObjectsInactive.Exclude);
#else
        _hud = FindObjectOfType<UIHUD>();
#endif
	}

	public void BindHUD(UIHUD hud) { _hud = hud; PushScoreToHUD(); }
	void PushScoreToHUD() { if (_hud) _hud.SetScore(totalDamage); }

	// --- 점수 갱신 ---
	public void AddDamage(int amount) => OnDealDamage("Boss", amount);
	public void AddDamage(string tag, int amount) => OnDealDamage(tag, amount);
	public void AddPlayerDamage(int amount) => OnPlayerDamaged(amount);

	public void OnDealDamage(string targetTag, int damage)
	{
		if (damage <= 0) return;
		int delta = (targetTag == "WeakPoint") ? damage * 2 :
					(targetTag == "Boss") ? damage : 0;
		if (delta == 0) return;
		totalDamage += delta;
		PushScoreToHUD();
	}

	public void OnPlayerDamaged(int damage)
	{
		if (damage <= 0) return;
		totalDamage -= (damage * 2);
		PushScoreToHUD();
	}

	public void ResetScore()
	{
		totalDamage = 0;
		_submitted = false;   // 새 라운드에서 다시 전송 가능
		PushScoreToHUD();
	}

	// --- 서버 전송 트리거 ---
	/// <summary>플레이어 사망/게임 종료 때 호출</summary>
	public void OnPlayerDeath()
	{
		if (_submitted || _submitting)
		{
			Debug.Log("[GameScore] 이미 전송되었거나 진행 중이라 무시");
			return;
		}
		Debug.Log($"[GameScore] 플레이어 사망 - 총 점수 {totalDamage} 전송…");
		_submitted = true; // 이 시점부터 추가 호출 차단
		StartCoroutine(SendScoreToServer(totalDamage));
	}

	/// <summary>디버그용 강제 전송 (주의: 중복 방지 동일 적용)</summary>
	public void SendNow()
	{
		if (_submitted || _submitting) return;
		_submitted = true;
		StartCoroutine(SendScoreToServer(totalDamage));
	}

	[System.Serializable] private class RankPayload { public int score; }

	IEnumerator SendScoreToServer(int score)
	{
		if (_submitting) yield break;
		_submitting = true;

		var json = JsonUtility.ToJson(new RankPayload { score = score });

		for (int attempt = 1; attempt <= 3; attempt++)
		{
			using (var req = new UnityWebRequest(endpoint, "POST"))
			{
				req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
				req.downloadHandler = new DownloadHandlerBuffer();
				req.SetRequestHeader("Content-Type", "application/json");
				req.timeout = 10;

				yield return req.SendWebRequest();

				long code = req.responseCode;
				string resp = req.downloadHandler != null ? req.downloadHandler.text : "";

				if (req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
				{
					Debug.Log($"[GameScore] 점수 전송 성공 ({code}) resp='{resp}'");
					_submitting = false;
					yield break; // 성공 → 종료 (DB 1회 저장)
				}

				Debug.LogError($"[GameScore] 전송 실패(시도 {attempt}/3) code={code}, error={req.error}, resp='{resp}'");

				// 다음 재시도 (실패인 경우에만)
				if (attempt < 3) yield return new WaitForSeconds(0.75f * attempt);
			}
		}

		// 3회 실패 → 다음 라운드에서 다시 시도할 수 있게 플래그 풀어줌
		_submitting = false;
		_submitted = false;
	}

	string BuildEndpoint()
	{
#if UNITY_WEBGL && !UNITY_EDITOR
        string abs = Application.absoluteURL;
        if (!string.IsNullOrEmpty(abs))
        {
            var u = new System.Uri(abs);
            string origin = $"{u.Scheme}://{u.Host}{(u.IsDefaultPort ? "" : $":{u.Port}")}";
            return $"{origin}/spring/rank";
        }
#endif
		return "http://localhost:8080/spring/rank";
	}
}
