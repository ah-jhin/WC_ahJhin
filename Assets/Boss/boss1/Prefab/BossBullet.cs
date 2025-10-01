using UnityEngine;

public class BossBullet : MonoBehaviour
{
    public int damage = 20;
    public float lifeTime = 4f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어만 처리, 그 외(벽/바닥 등)는 아무 것도 안 함
        if (other.CompareTag("Player"))
        {
            var hp = other.GetComponent<PlayerHealth>(); // 네 플레이어 체력 스크립트명
            if (hp) hp.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}