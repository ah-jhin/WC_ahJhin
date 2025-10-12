using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 체력 스크
/// - IDamageable 구현으로 보스/몹/함정 등에서 통일 호출
/// - 무적 시간, 죽음 처리 포함
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
	[Header("HP")]
	public int maxHP = 100;          // 최대 체력
	public int currentHP;            // 현재 체력

	[Header("피격 무적/연출")]
	public float invincibleTime = 0.6f; // 피격 후 무적 시간
	private float lastHitTime = -999f;  // 마지막 피격 시각
	public SpriteRenderer sr;           // 깜빡임용 렌더러

	[Header("Death")]
	public float restartDelay = 1.5f;   // 사망 후 재시작 지연
	private bool isDead = false;        // 사망 플래그

	void Awake()
	{
		// 시작 체력 초기화
		currentHP = maxHP;

		// sr 자동 할당(없으면 자식에서 탐색)
		if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
	}

	/// <summary>
	/// IDamageable 구현.
	/// 약점 여부와 배율이 들어오지만, 플레이어에게 약점 개념이 없으면 무시 가능.
	/// </summary>
	public void TakeDamage(int amount, bool weak, float weakMultiplier)
	{
		// 1) 무적/사망 상태면 무시
		if (Time.time - lastHitTime < invincibleTime || isDead) return;
		lastHitTime = Time.time;

		// 2) 최종 데미지 계산(약점 적용이 필요 없으면 아래 한 줄로 대체: int final = amount;)
		int final = weak ? Mathf.RoundToInt(amount * weakMultiplier) : amount;

		// 3) 체력 감소
		currentHP = Mathf.Max(0, currentHP - final);

		// 4) UI 갱신 지점(사용 중인 HUD 메서드 호출)
		// Example: UIHUD.I.SetHP(currentHP, maxHP);

		// 5) 피격 연출(선택)
		// StartCoroutine(Blink());
		// DamageNumberPool.I?.Spawn(transform.position, final, Color.red);

		// 6) 사망 처리
		if (currentHP <= 0)
		{
			isDead = true;
			OnDead();
		}
	}

	/// <summary>레거시 호환. 과거 코드가 TakeDamage(int)만 호출해도 동작.</summary>
	public void TakeDamage(int amount) { TakeDamage(amount, false, 1f); }

	/// <summary>체력 회복</summary>
	public void Heal(int amount)
	{
		if (isDead) return;
		currentHP = Mathf.Min(maxHP, currentHP + Mathf.Max(0, amount));
		// UIHUD 갱신 필요 시 호출
	}

	/// <summary>깜빡임 연출(선택)</summary>
	System.Collections.IEnumerator Blink()
	{
		float end = Time.time + 0.25f;
		while (Time.time < end && !isDead)
		{
			if (sr) sr.enabled = !sr.enabled;
			yield return new WaitForSeconds(0.05f);
		}
		if (sr) sr.enabled = true;
	}

	/// <summary>사망 로직</summary>
	void OnDead()
	{
		Debug.Log("[PlayerHealth] Player Dead");

		// 이동/공격 비활성
		var move = GetComponent<PlayerMovement>(); if (move) move.enabled = false;
		var pistol = GetComponent<WP_Pistol>(); if (pistol) pistol.enabled = false;

		// 물리 정지
		var rb = GetComponent<Rigidbody2D>();
		if (rb) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }

		// 게임오버 연동(있을 때만)
		// if (GameOverManager.Instance) GameOverManager.Instance.GameOver();

		// 씬 재시작 예약
		Invoke(nameof(RestartScene), restartDelay);
	}

	void RestartScene()
	{
		Scene current = SceneManager.GetActiveScene();
		SceneManager.LoadScene(current.buildIndex);
	}
}
