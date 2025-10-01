using UnityEngine;

/// <summary>
/// 총알 스크립트 (공통)
/// - 무기가 생성할 때 damage, weakMultiplier 값을 세팅해줌
/// - 여기서는 충돌 처리만 담당
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("총알 스탯 (무기에서 할당)")]
    public int damage;               // 무기가 발사 시 지정
    public float weakMultiplier;     // 무기마다 다른 약점 배율
    public float lifetime = 3f;      // 총알 지속 시간

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // === 보스 충돌 ===
        if (other.CompareTag("Boss") || other.CompareTag("WeakPoint"))
        {
            bool isWeak = other.CompareTag("WeakPoint");

            BossBase boss = other.GetComponentInParent<BossBase>();
            if (boss != null)
            {
                boss.TakeDamage(damage, isWeak, weakMultiplier);
                Debug.Log("[Bullet] Boss hit! Damage=" + damage + " Weak=" + isWeak);
            }

            Destroy(gameObject); // 총알 파괴
        }

        // === 블럭 충돌 ===
        if (other.CompareTag("block"))
        {
            Debug.Log("[Bullet] 블럭 충돌 → 총알 삭제");
            Destroy(gameObject);
        }
    }
}
