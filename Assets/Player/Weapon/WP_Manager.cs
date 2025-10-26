using UnityEngine;
using UnityEngine.UI;   // Image 사용

/// <summary>
/// 무기 관리(입력/교체/발사 쿨타임만 담당).
/// - 발사 간격은 활성 무기의 IWeaponInfo.FireInterval을 사용.
/// - Z 또는 Fire1을 누르고 있으면 자동 연사.
/// - 무기 탄약 0이 되면 자동 제거 후 기본무기로 복귀.
/// </summary>
public class WP_Manager : MonoBehaviour
{
    [Header("무기 슬롯 (0=권총, 1~2=보급)")]
    public GameObject[] weaponSlots = new GameObject[3]; // 슬롯 컨테이너(무기 오브젝트)
    [Header("HUD 아이콘(선택)")]
    public Image slot1Icon;   // 1번 슬롯 아이콘    
    public Image slot2Icon;   // 2번 슬롯 아이콘    
    [Header("입력")]
    public KeyCode nextKey = KeyCode.A;          // 무기 순환
    public KeyCode shootKey = KeyCode.Z;         // 발사
    public KeyCode dropKey  = KeyCode.F;        // 드랍
    public UIHUD hud;                             // HUD 참조(선택)

    [Header("권총 프리팹")]
    public GameObject pistolPrefab;              // 시작 시 0번 비면 스폰

    int _cur = 0;                                // 현재 슬롯
    float _nextFireTime;                         // 다음 발사 가능 시각

    [Header("효과음")]
    public AudioSource audioSource;      // AudioSource
    public AudioClip swapWeaponSFX;      // 무기 교체 시 재생할 효과음
    public AudioClip dropWeaponSFX;      // 무기 드롭 시 재생할 효과음

    void Start()
    {
        // 0번 슬롯 보장
        if (weaponSlots[0] == null && pistolPrefab != null)
        {
            weaponSlots[0] = Instantiate(pistolPrefab, transform);
            weaponSlots[0].name = "Pistol(runtime)";
        }
        ActivateCurrent(); // 0번 활성화
        _nextFireTime = Time.time;
    }

    void Update()
    {
        HandleSwapInput();
        HandleFireInput(); // 자동연사
        HandleDropInput(); 
    }

    void HandleSwapInput()
    {
        // 무기 변경 입력 처리: 무기 변경 발생 시 효과음 재생
        if (Input.GetKeyDown(nextKey))
        {
            int prevIndex = _cur;
            SwapNext();
            // 이전 무기와 다른 무기로 실제로 변경된 경우에만 효과음 재생
            if (audioSource != null && swapWeaponSFX != null && _cur != prevIndex)
            {
                audioSource.PlayOneShot(swapWeaponSFX);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            int prevIndex = _cur;
            SwapTo(0);
            if (audioSource != null && swapWeaponSFX != null && _cur != prevIndex)
            {
                audioSource.PlayOneShot(swapWeaponSFX);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            int prevIndex = _cur;
            SwapTo(1);
            if (audioSource != null && swapWeaponSFX != null && _cur != prevIndex)
            {
                audioSource.PlayOneShot(swapWeaponSFX);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            int prevIndex = _cur;
            SwapTo(2);
            if (audioSource != null && swapWeaponSFX != null && _cur != prevIndex)
            {
                audioSource.PlayOneShot(swapWeaponSFX);
            }
        }
    }

    void HandleFireInput()
    {
        bool firePressed = Input.GetButton("Fire1") || Input.GetKey(shootKey);
        if (!firePressed) return;

        var go = GetActiveWeapon(); if (!go) return;

        // 무기별 발사 간격 사용
        var wi = go.GetComponent<IWeaponInfo>();
        float interval = wi != null ? Mathf.Max(0.01f, wi.FireInterval) : 0.2f;
        if (Time.time < _nextFireTime) return;

        go.SendMessage("Shoot", SendMessageOptions.DontRequireReceiver);
        _nextFireTime = Time.time + interval;

        // HUD 탄약 갱신(구현 시)
        if (hud && wi != null) hud.SetWeapon(wi.Icon, wi.DisplayName, wi.Ammo, wi.IsInfinite);

        // 비무한 + 탄약 0이면 제거
        if (wi != null && !wi.IsInfinite && wi.Ammo <= 0)
            OnWeaponEmpty(go.GetComponent<WP_Pistol>()); // 해당 무기 타입 전달(없으면 무시)
    }
    void HandleDropInput()
    {
        if (!Input.GetKeyDown(dropKey)) return;
        // 권총(slot 0)은 버리지 못함
        if (_cur == 0) return;
        var go = GetActiveWeapon();
        if (!go) return;
        // 현재 무기 파괴 및 슬롯 비우기
        Destroy(go);
        weaponSlots[_cur] = null;
        // 0번 슬롯 (권총)으로 복귀
        _cur = 0;
        ActivateCurrent();
        // 무기 드랍 효과음 재생 (AudioSource나 AudioClip이 없으면 재생하지 않음)
        if (audioSource != null && dropWeaponSFX != null)
        {
            audioSource.PlayOneShot(dropWeaponSFX);
        }
    }

    // 무기 비었을 때 호출됨(무기가 SendMessageUpwards로 부름)
    void OnWeaponEmpty(object _)
    {
        var go = GetActiveWeapon(); if (!go) return;

        // 0번이면 남겨두고, 보급(1~2)이면 제거 후 0번 복귀
        if (_cur > 0)
        {
            Destroy(go);
            weaponSlots[_cur] = null;
            _cur = 0;
            ActivateCurrent();
        }
    }
    // 보급 상자 등이 호출: 무기 프리팹을 슬롯(1~2)에 장착
public bool AddWeapon(GameObject weaponPrefab, bool select = true)
{
    if (!weaponPrefab) { Debug.LogWarning("[WP_Manager] AddWeapon: prefab=null"); return false; }

    int slot = -1;
    for (int i = 1; i < weaponSlots.Length; i++)
        if (weaponSlots[i] == null) { slot = i; break; }

    if (slot == -1)
    {
        Debug.LogWarning("[WP_Manager] 빈 슬롯(1~2) 없음");
        return false;
    }

    var go = Instantiate(weaponPrefab, transform);
    if (!go) { Debug.LogError("[WP_Manager] Instantiate 실패"); return false; }

    // 필수 컴포넌트 점검
    var wi = go.GetComponent<IWeaponInfo>();
    if (wi == null) Debug.LogError("[WP_Manager] IWeaponInfo 미구현(예: WP_Pistol 누락)");

    weaponSlots[slot] = go;
    if (select) { _cur = slot; ActivateCurrent(); } else go.SetActive(false);

    Debug.Log($"[WP_Manager] 슬롯 {slot} 장착: {go.name}");
    RefreshSlotIcons();
    return true;
}

    void RefreshSlotIcons()
    {
        void Set(Image img, GameObject go)
        {
            if (!img) return;
            var wi = go ? go.GetComponent<IWeaponInfo>() : null;
            img.sprite = wi != null ? wi.Icon : null;
            img.enabled = img.sprite != null;
        }
        Set(slot1Icon, (weaponSlots.Length > 1) ? weaponSlots[1] : null);
        Set(slot2Icon, (weaponSlots.Length > 2) ? weaponSlots[2] : null);
    }
    public void SwapNext()
    {
        int start = _cur;
        do { _cur = (_cur + 1) % weaponSlots.Length; }
        while (weaponSlots[_cur] == null && _cur != start);
        ActivateCurrent();
    }

    public void SwapTo(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;
        if (weaponSlots[index] == null) return;
        _cur = index; ActivateCurrent();
    }

    void ActivateCurrent()
    {
        // 슬롯 활성/비활성
        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] != null) weaponSlots[i].SetActive(i == _cur);

        // HUD 갱신
        var go = GetActiveWeapon();
        var wi = go ? go.GetComponent<IWeaponInfo>() : null;
        if (hud && wi != null) hud.SetWeapon(wi.Icon, wi.DisplayName, wi.Ammo, wi.IsInfinite);

        // ★ 쿨타임 리셋 금지
        // _nextFireTime = Time.time;   // ← 삭제

        RefreshSlotIcons();
    }

    GameObject GetActiveWeapon()
    {
        if (_cur < 0 || _cur >= weaponSlots.Length) return null;
        return weaponSlots[_cur];
    }
}
