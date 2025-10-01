using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 모든 보스의 공통 기능 (체력/데미지/HP UI)
/// </summary>
public class BossBase : MonoBehaviour
{
    [Header("Boss Stats")]
    public int maxHP = 100;
    protected int currentHP;
    public int currentHealth;

    [Header("UI")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText; // 체력바 밑 숫자

    protected virtual void Start()
    {
        currentHP = maxHP;
        UpdateUI();
    }

    /// <summary>
    /// 보스가 피해를 입을 때 호출
    /// </summary>
    /// <param name="damage">기본 피해</param>
    /// <param name="isWeak">약점 여부</param>
    /// <param name="weakMultiplier">무기별 약점 배율</param>
    public virtual void TakeDamage(int damage, bool isWeak, float weakMultiplier)
    {
        if (isWeak)
            damage = Mathf.RoundToInt(damage * weakMultiplier);

        // 체력 감소
        currentHP -= damage;

        // 0 ~ maxHP 범위로 고정
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        UpdateUI();
        Debug.Log("[BossBase] " + gameObject.name + " 피해=" + damage + " / 남은체력=" + currentHP);

        // 체력이 0일 때만 사망 처리
        if (currentHP <= 0)
            Die();
    }


    protected virtual void Die()
    {
        Debug.Log("[BossBase] " + gameObject.name + " 사망");
        Destroy(gameObject);
    }

    protected void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }
        if (hpText != null)
        {
            hpText.text = currentHP + " / " + maxHP;
        }
    }
}
