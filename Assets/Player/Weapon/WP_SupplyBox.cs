using UnityEngine;

/// <summary>
/// 보급품 박스 스크립트
/// 플레이어가 닿으면 무작위 무기를 지급하고 박스는 사라진다.
/// </summary>
public class WP_SupplyBox : MonoBehaviour
{
    [Header("지급할 무기 프리팹 리스트 (권총 제외)")]
    public GameObject[] weaponPrefabs; // 샷건, 머신건, 로켓런처 같은 무기 프리팹

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어만 반응
        if (other.CompareTag("Player"))
        {
            // 무기 매니저 찾기
            WP_Manager weaponManager = other.GetComponent<WP_Manager>();
            if (weaponManager != null && weaponPrefabs.Length > 0)
            {
                // 무작위 무기 선택
                int index = Random.Range(0, weaponPrefabs.Length);
                GameObject randomWeapon = weaponPrefabs[index];

                // 무기 지급
                weaponManager.AddWeapon(randomWeapon);

                Debug.Log("[SupplyBox] " + randomWeapon.name + " 지급 완료");
            }

            // 박스 제거
            Destroy(gameObject);
        }
    }
}
