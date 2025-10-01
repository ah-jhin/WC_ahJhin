using UnityEngine;

/// <summary>
/// 권총 무기 스크립트
/// - Z키로 발사
/// - 총알에 권총 데미지와 약점 배율을 전달
/// </summary>
public class WP_Pistol : MonoBehaviour
{
    [Header("총알 설정")]
    public GameObject bulletPrefab;   // Bullet 프리팹
    public Transform firePoint;       // 발사 위치

    [Header("권총 스탯")]
    public int bulletDamage = 10;     // 권총 데미지
    public float bulletSpeed = 15f;   // 권총 총알 속도
    public float weakMultiplier = 1.5f; // 권총 약점 배율

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // Rigidbody로 이동
        Rigidbody2D rb = bulletObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = firePoint.right * bulletSpeed;
        }

        // Bullet.cs에 무기 스탯 전달
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.damage = bulletDamage;
            bulletScript.weakMultiplier = weakMultiplier;
        }

        Debug.Log("[Pistol] 발사됨 / Damage=" + bulletDamage + " WeakMultiplier=" + weakMultiplier);
    }
}
