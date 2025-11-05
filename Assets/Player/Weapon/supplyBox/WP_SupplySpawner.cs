using UnityEngine;

/// <summary>
/// 보급 스폰러(씬에 1개).
/// - Q 키로 전투 시작 후에만 스폰 타이머 가동
/// - 시작 후 firstDelay 1회, 이후 interval 주기
/// - T: 즉시 랜덤 스폰(전투 시작 후에만)
/// - L+1~6: 특정 무기 스폰(치트, 전투 시작 후에만)
/// </summary>
public class WP_SupplySpawner : MonoBehaviour
{
    [Header("필수 참조")]
    public Transform player;             // 플레이어 Transform
    public Camera cam;                   // 메인 카메라

    [Header("보급 프리팹(겉모양)")]
    public GameObject boxPrefab;         // WP_SupplyBox가 붙은 상자 프리팹

    [Header("스폰 타이밍")]
    public float firstDelay = 5f;        // 전투 시작(Q) 후 첫 지연
    public float interval = 25f;         // 이후 간격

    [Header("스폰 위치")]
    public float abovePlayerY = 8f;      // 플레이어 머리 위 높이
    public float camMarginX = 2f;        // 화면 밖 여유
    public float xRange = 6f;            // 플레이어 기준 좌우 난수 범위
    public bool clampToCamera = true;    // 화면 밖 스폰 금지
    public float screenPadding = 0.5f;   // 화면 가장자리 여유

    [Header("키 설정")]
    public KeyCode startKey = KeyCode.Q; // 전투 시작 키(보스 시작과 동일 키)
    public KeyCode debugSpawnKey = KeyCode.T; // 디버그 즉시 스폰
    public KeyCode cheatKey = KeyCode.L;      // L+숫자 = 지정 스폰

    // 내부
    bool started = false;                // 전투 시작 여부
    float nextAt = float.PositiveInfinity;

    void Awake()
    {
        if (!cam) cam = Camera.main;
#if UNITY_2023_1_OR_NEWER
        if (!player) { var pm = FindFirstObjectByType<PlayerMovement>(); if (pm) player = pm.transform; }
#else
        if (!player) { var pm = FindObjectOfType<PlayerMovement>(); if (pm) player = pm.transform; }
#endif
    }

    void Update()
    {
        // 1) 전투 시작 대기(Q)
        if (!started)
        {
            if (Input.GetKeyDown(startKey)) StartSpawning(); // Q 누르면 시작
            return;
        }

        // 2) 타이머 스폰
        if (Time.time >= nextAt)
        {
            SpawnRandom();
            nextAt = Time.time + Mathf.Max(0.1f, interval);
        }

        // 3) 디버그 스폰
        if (Input.GetKeyDown(debugSpawnKey)) SpawnRandom();

        // 4) 치트 스폰
        if (Input.GetKey(cheatKey))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnTyped(WP_SupplyBox.SupplyType.Revolver);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnTyped(WP_SupplyBox.SupplyType.AR);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnTyped(WP_SupplyBox.SupplyType.SR);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnTyped(WP_SupplyBox.SupplyType.Shotgun);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SpawnTyped(WP_SupplyBox.SupplyType.Rocket);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SpawnTyped(WP_SupplyBox.SupplyType.SMG);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SpawnTyped(WP_SupplyBox.SupplyType.Heal);
        }
    }

    /// <summary>전투 시작(Q): 타이머 가동 시작</summary>
    public void StartSpawning()
    {
        if (started) return;
        started = true;
        nextAt = Time.time + Mathf.Max(0.1f, firstDelay); // 첫 스폰 예약
        Debug.Log("[SupplySpawner] Start");
    }

    /// <summary>전투 중지: 타이머 해제</summary>
    public void StopSpawning()
    {
        started = false;
        nextAt = float.PositiveInfinity;
        Debug.Log("[SupplySpawner] Stop");
    }

    void SpawnRandom() => SpawnTyped(WP_SupplyBox.SupplyType.Random);

    void SpawnTyped(WP_SupplyBox.SupplyType t)
    {
        if (!boxPrefab || !player) return;

        // 1) 플레이어 기준 난수 X
        float x = player.position.x + Random.Range(-xRange, xRange);

        // 2) 화면 안으로 클램프(옵션)
        if (clampToCamera && cam)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float minX = cam.transform.position.x - halfW + screenPadding;
            float maxX = cam.transform.position.x + halfW - screenPadding;
            x = Mathf.Clamp(x, minX, maxX);
        }

        // 3) Y = 머리 위
        Vector3 spawn = new Vector3(x, player.position.y + abovePlayerY, 0f);

        var go = Instantiate(boxPrefab, spawn, Quaternion.identity);
        var box = go.GetComponent<WP_SupplyBox>();
        if (box) box.type = t;

    }
}
