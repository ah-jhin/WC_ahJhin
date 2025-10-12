using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 모든 보스 공통 베이스
/// - 체력/피해/HP UI 처리 단일화
/// - IDamageable 구현으로 Bullet과 직접 연동
/// </summary>
public class BossBase : MonoBehaviour, IDamageable
{
	[Header("Boss Stats")]
	public int maxHP = 100;            // 기본 체력(Inspector에서 설정)
	protected int currentHP;           // 현재 체력(내부 관리 전용)

	[Header("UI")]
	public Slider hpSlider;            // 보스 체력바
	public TextMeshProUGUI hpText;     // "현재/최대" 숫자

	protected virtual void Start()
	{
		// 시작 시 체력 초기화
		currentHP = maxHP;
		UpdateUI();
	}

	/// <summary>
	/// IDamageable 구현: 어떤 탄/공격이든 여기로 수렴
	/// </summary>
	public void TakeDamage(int amount, bool weak, float weakMultiplier)
	{
		// 1) 최종 데미지 계산(약점이면 배율 적용)
		int final = weak ? Mathf.RoundToInt(amount * weakMultiplier) : amount;

		// 2) 체력 감소(0 하한)
		currentHP = Mathf.Max(0, currentHP - final);

		// 3) UI 갱신
		UpdateUI();

		// 4) 사망 처리
		if (currentHP == 0) Die();
	}

	/// <summary>사망 공통 처리(파생에서 override 가능)</summary>
	protected virtual void Die()
	{
		Debug.Log($"[BossBase] {name} 사망");
		Destroy(gameObject);
	}

	/// <summary>HP UI 업데이트</summary>
	protected void UpdateUI()
	{
		if (hpSlider)
		{
			hpSlider.maxValue = maxHP;
			hpSlider.value = currentHP;
		}
		if (hpText) hpText.text = $"{currentHP} / {maxHP}";
	}
}
