using UnityEngine;

/// <summary>
/// 보급 스폰러(씬에 1개).
/// - 시작 5초 후 1개, 이후 25초마다
/// - T 키: 즉시 랜덤 1개(디버깅)
/// - L+1~6: 특정 무기 스폰(치트)
/// - 스폰 위치: 플레이어 머리 위 + 오프셋, 카메라 밖 높이
/// </summary>
public class WP_SupplySpawner : MonoBehaviour
{
    [Header("필수 참조")]
    public Transform player;             // 플레이어 Transform
    public Camera cam;                   // 메인 카메라

    [Header("보급 프리팹(겉모양)")]
    public GameObject boxPrefab;         // WP_SupplyBox가 붙은 상자 프리팹(겉모양)

    [Header("스폰 타이밍")]
    public float firstDelay = 5f;        // 시작 후 지연
    public float interval = 25f;         // 이후 간격

    [Header("스폰 위치")]
    public float abovePlayerY = 8f;      // 플레이어 머리 위 높이
    public float camMarginX = 2f;        // 화면 밖으로 벗어나는 X 여유
    public float xRange = 6f;          // 플레이어 기준 좌우 난수 범위
    public bool clampToCamera = true;  // 화면 밖 스폰 금지
    public float screenPadding = 0.5f; // 화면 가장자리 여유

    [Header("디버그/치트")]
    public KeyCode debugSpawnKey = KeyCode.T; // 즉시 랜덤
    public KeyCode cheatKey = KeyCode.L;      // L+숫자=지정 스폰

    float nextAt;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!player) 
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm) player = pm.transform;
        }
    }

    void OnEnable()
    {
        nextAt = Time.time + Mathf.Max(0.1f, firstDelay);
    }

    void Update()
    {
        if (Time.time >= nextAt) { SpawnRandom(); nextAt = Time.time + Mathf.Max(0.1f, interval); }
        if (Input.GetKeyDown(debugSpawnKey)) SpawnRandom();

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
        x = Mathf.Clamp(x, minX, maxX);      // ← 화면 밖 방지
    }

    // 3) Y = 머리 위
    Vector3 spawn = new Vector3(x, player.position.y + abovePlayerY, 0f);

    var go = Instantiate(boxPrefab, spawn, Quaternion.identity);
    var box = go.GetComponent<WP_SupplyBox>();
    if (box) box.type = t;

    Debug.Log($"[SupplySpawner] {t} @ {spawn}");
}

    float WorldHalfWidth()
    {
        if (!cam) return 8f;
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        return w;
    }
}
