using UnityEngine;

/// 총알이 맞을 콜라이더 쪽에 붙임. 씬의 BossBase에게 위임.
public class BossDamageProxy : MonoBehaviour, IDamageable
{
    BossBase boss;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        boss = FindFirstObjectByType<BossBase>();
#else
        boss = FindObjectOfType<BossBase>();
#endif
    }

    public void TakeDamage(int amount, bool weak, float weakBonus)
    {
        if (boss != null) boss.TakeDamage(amount, weak, weakBonus);
    }
}
