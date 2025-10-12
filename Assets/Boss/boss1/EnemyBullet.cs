// EnemyBullet.cs
using UnityEngine;
public class EnemyBullet : MonoBehaviour
{
    public float lifetime = 2f;
    public int damage = 10;
    void Start() { Destroy(gameObject, lifetime); }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var hp = other.GetComponent<PlayerHealth>();
            if (hp) hp.TakeDamage(damage, false, 1f);
			Destroy(gameObject);
        }
    }
}