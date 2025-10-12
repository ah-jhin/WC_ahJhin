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
        // �÷��̾ ó��, �� ��(��/�ٴ� ��)�� �ƹ� �͵� �� ��
        if (other.CompareTag("Player"))
        {
            var hp = other.GetComponent<PlayerHealth>(); // �� �÷��̾� ü�� ��ũ��Ʈ��
            if (hp) hp.TakeDamage(damage, false, 1f); // ���� �ƴ�, ���� 1
			Destroy(gameObject);
        }
    }
}