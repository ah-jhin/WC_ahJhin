using UnityEngine;

/// <summary>
/// 무기 스왑과 탄약 관리를 담당하는 매니저
/// </summary>
public class WP_Manager : MonoBehaviour
{
    [Header("무기 슬롯 (0=기본 권총)")]
    public GameObject[] weaponSlots = new GameObject[3]; // 최대 3슬롯
    private int currentIndex = 0; // 현재 선택된 슬롯 인덱스

    void Start()
    {
        // 시작 시 권총만 활성화
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != null)
                weaponSlots[i].SetActive(i == 0);
        }
    }

    void Update()
    {
        HandleSwapInput();
    }

    /// <summary>
    /// 입력 처리 (스왑 전용)
    /// </summary>
    private void HandleSwapInput()
    {
        // A키 스왑
        if (Input.GetKeyDown(KeyCode.A))
        {
            SwapNext();
        }

        // 키보드 1~3 직접 선택 (후순위)
        if (Input.GetKeyDown(KeyCode.Alpha1)) { SwapTo(0); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { SwapTo(1); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { SwapTo(2); }
    }

    /// <summary>
    /// 다음 무기 슬롯으로 이동 (빈 슬롯 건너뜀)
    /// </summary>
    private void SwapNext()
    {
        int startIndex = currentIndex;
        do
        {
            currentIndex = (currentIndex + 1) % weaponSlots.Length;
        } while (weaponSlots[currentIndex] == null && currentIndex != startIndex);

        ActivateCurrentWeapon();
    }

    /// <summary>
    /// 특정 슬롯으로 직접 이동
    /// </summary>
    private void SwapTo(int index)
    {
        if (weaponSlots[index] == null) return;
        currentIndex = index;
        ActivateCurrentWeapon();
    }

    /// <summary>
    /// 현재 슬롯 무기만 활성화
    /// </summary>
    private void ActivateCurrentWeapon()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != null)
                weaponSlots[i].SetActive(i == currentIndex);
        }

        Debug.Log("[WeaponManager] 현재 무기 슬롯: " + (currentIndex + 1));
    }

    /// <summary>
    /// 보급품에서 무기 획득 (빈 슬롯에 장착)
    /// </summary>
    public void AddWeapon(GameObject newWeaponPrefab)
    {
        for (int i = 1; i < weaponSlots.Length; i++) // 0은 권총 고정
        {
            if (weaponSlots[i] == null)
            {
                // 새 무기를 Player 자식으로 생성
                GameObject weapon = Instantiate(newWeaponPrefab, transform);
                weapon.SetActive(false);
                weaponSlots[i] = weapon;

                Debug.Log("[WeaponManager] 새로운 무기 장착: " + newWeaponPrefab.name);
                return;
            }
        }

        Debug.Log("[WeaponManager] 빈 슬롯 없음, 무기 추가 실패");
    }
}
