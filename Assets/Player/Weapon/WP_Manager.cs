using UnityEngine;

/// <summary>
/// WP_Manager 스크(전체 교체본)
/// - 0번 슬롯=권총 고정, 1~2번=보급 무기
/// - 한 번에 하나만 활성화
/// - Update에서 입력 처리(A, 1~3, Fire1) 후 활성 무기의 public void Shoot() 호출
/// - 무기 타입 상관없이 "Shoot()" 메서드만 있으면 동작(SendMessage 사용)
/// </summary>
public class WP_Manager : MonoBehaviour
{
	[Header("무기 슬롯 (0=권총, 1~2=보급)")]
	public GameObject[] weaponSlots = new GameObject[3]; // 플레이어 자식 무기 오브젝트 할당

	[Header("입력")]
	public KeyCode nextKey = KeyCode.A;   // 무기교체
	public KeyCode shootKey = KeyCode.Z;  // 발사


	[Header("발사 공통 쿨타임")]
	public float fireRate = 6f;             // 초당 발사수(예: 6 = 0.166s마다 1발)
	private float _nextFireTime;            // 다음 발사 가능 시각

	private int _cur = 0;                   // 현재 슬롯 인덱스(0~2)

	public GameObject pistolPrefab;			// 권총 프리팹

	void Start()
	{
		// 슬롯 0 이 비어 있으면 권총 프리팹을 자식으로 생성
		if (weaponSlots[0] == null && pistolPrefab != null)
		{
			weaponSlots[0] = Instantiate(pistolPrefab, transform); // 플레이어의 자식
			weaponSlots[0].name = "Pistol(runtime)";
			weaponSlots[0].SetActive(true);
		}
		// 나머지 슬롯 비활성
		for (int i = 0; i < weaponSlots.Length; i++)
		{
			if (weaponSlots[i] != null) weaponSlots[i].SetActive(i == 0);
		}
		_cur = 0;
	}

	void Update()
	{
		HandleSwapInput();      // 무기 교체 입력
		HandleFireInput();      // 발사 입력
	}

	/// <summary>
	/// 무기 교체 입력 처리(A 순환, 1~3 직접)
	/// </summary>
	void HandleSwapInput()
	{
		if (Input.GetKeyDown(nextKey)) SwapNext();

		if (Input.GetKeyDown(KeyCode.Alpha1)) SwapTo(0);
		if (Input.GetKeyDown(KeyCode.Alpha2)) SwapTo(1);
		if (Input.GetKeyDown(KeyCode.Alpha3)) SwapTo(2);
	}

	/// <summary>
	/// 발사 입력 처리(Fire1). 공통 쿨타임 적용 후 활성 무기의 Shoot() 호출.
	/// </summary>
	void HandleFireInput()
	{
		bool firePressed = Input.GetButton("Fire1") || Input.GetKey(shootKey); // 마우스 or Z
		if (!firePressed) return;
		if (Time.time < _nextFireTime) return;

		var active = GetActiveWeapon();
		if (active == null) return;

		active.SendMessage("Shoot", SendMessageOptions.DontRequireReceiver);
		_nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
	}

	/// <summary>
	/// 다음 무기(빈 슬롯은 자동 건너뜀)
	/// </summary>
	public void SwapNext()
	{
		int start = _cur;
		do
		{
			_cur = (_cur + 1) % weaponSlots.Length;
		} while (weaponSlots[_cur] == null && _cur != start);

		ActivateCurrent();
	}

	/// <summary>
	/// 특정 인덱스로 바로 교체
	/// </summary>
	public void SwapTo(int index)
	{
		if (index < 0 || index >= weaponSlots.Length) return;
		if (weaponSlots[index] == null) return;
		_cur = index;
		ActivateCurrent();
	}

	/// <summary>
	/// 현재 슬롯만 활성화
	/// </summary>
	void ActivateCurrent()
	{
		for (int i = 0; i < weaponSlots.Length; i++)
		{
			if (weaponSlots[i] != null)
				weaponSlots[i].SetActive(i == _cur);
		}
		// 교체 시 즉시 발사 가능하도록 약간의 여유(선택)
		_nextFireTime = Mathf.Min(_nextFireTime, Time.time);
		Debug.Log($"[WP_Manager] 현재 무기 슬롯: {_cur + 1}");
	}

	/// <summary>
	/// 활성 무기 오브젝트 반환(없으면 null)
	/// </summary>
	GameObject GetActiveWeapon()
	{
		if (_cur < 0 || _cur >= weaponSlots.Length) return null;
		return weaponSlots[_cur];
	}

	/// <summary>
	/// 보급으로 새 무기 지급: 1→2 순서로 빈 슬롯 채움. 둘 다 차면 2번을 교체.
	/// </summary>
	public void AddWeapon(GameObject newWeaponPrefab)
	{
		if (newWeaponPrefab == null) return;

		// 1번이 비었으면 1번에 생성
		if (weaponSlots[1] == null)
		{
			weaponSlots[1] = Instantiate(newWeaponPrefab, transform);
			weaponSlots[1].SetActive(false);
			_cur = 1;
			ActivateCurrent();
			Debug.Log($"[WP_Manager] 무기 획득(슬롯2): {newWeaponPrefab.name}");
			return;
		}

		// 2번이 비었으면 2번에 생성
		if (weaponSlots[2] == null)
		{
			weaponSlots[2] = Instantiate(newWeaponPrefab, transform);
			weaponSlots[2].SetActive(false);
			_cur = 2;
			ActivateCurrent();
			Debug.Log($"[WP_Manager] 무기 획득(슬롯3): {newWeaponPrefab.name}");
			return;
		}

		// 둘 다 차 있으면 2번을 교체(최신 획득 우선 규칙)
		if (weaponSlots[2] != null)
		{
			Destroy(weaponSlots[2]);
			weaponSlots[2] = Instantiate(newWeaponPrefab, transform);
			weaponSlots[2].SetActive(false);
			_cur = 2;
			ActivateCurrent();
			Debug.Log($"[WP_Manager] 무기 교체(슬롯3): {newWeaponPrefab.name}");
		}
	}
}
