using UnityEngine;

/// <summary>
/// 보급 상자(플레이어만 트리거).
/// - 낙하: 천천히 아래로 이동(충돌 무시 느낌)
/// - Y 한계/수명 초과 시 삭제
/// - 접촉 시: 타입별 처리(회복=난수, 무기 지급) + SFX + 삭제
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WP_SupplyBox : MonoBehaviour
{
    // ★ SMG 추가
    public enum SupplyType { Heal, Revolver, AR, SR, Shotgun, Rocket, SMG, Random }

    [Header("기본")]
    public SupplyType type = SupplyType.Random;     // 보급 종류
    public float fallSpeed = 1.5f;                  // 낙하 속도(느리게)
    public float lifeTime = 10f;                    // 자동 소멸 시간(초)
    public float killAbsY = 150f;                   // |Y| > 이 값이면 삭제

    [Header("회복(난수)")]
    public int healMin = 10;                        // ★ 최소 회복량(기본 10)
    public int healMax = 35;                        // ★ 최대 회복량(기본 35)

    [Header("무기 프리팹")]
    public GameObject revolverPrefab;
    public GameObject arPrefab;
    public GameObject srPrefab;
    public GameObject shotgunPrefab;
    public GameObject rocketPrefab;
    public GameObject smgPrefab;                    // ★ SMG 프리팹

    [Header("외형(아이콘)")]
    public SpriteRenderer iconRenderer;   // 상자 위 아이콘용 SR
    public Sprite sprHeal, sprRevolver, sprAR, sprSR, sprShotgun, sprRocket, sprSMG;
    WP_SupplyBox.SupplyType _rolled;      // 스폰 시 확정된 타입

    [Header("SFX(습득)")]
    public AudioSource sfx;                         // 없으면 자동 추가
    public AudioClip sfxHeal;
    public AudioClip sfxRevolver;
    public AudioClip sfxAR;
    public AudioClip sfxSR;
    public AudioClip sfxShotgun;
    public AudioClip sfxRocket;
    public AudioClip sfxSMG;                        // ★ SMG SFX
    public float sfxVolume = 1f;

    Collider2D col;
    float dieAt;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true; // 플레이어만 트리거
        if (!sfx) { sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; }
    }

    void OnEnable()
    {
        dieAt = Time.time + Mathf.Max(0.1f, lifeTime);
        // 스폰 시 타입 확정(랜덤이면 굴림)
        _rolled = (type == SupplyType.Random) ? RollRandomType() : type;
        ApplyIcon(_rolled);
    }
    void ApplyIcon(SupplyType t)
    {
        if (!iconRenderer) return;
        Sprite s = null;
        switch (t)
        {
            case SupplyType.Heal:     s = sprHeal; break;
            case SupplyType.Revolver: s = sprRevolver; break;
            case SupplyType.AR:       s = sprAR; break;
            case SupplyType.SR:       s = sprSR; break;
            case SupplyType.Shotgun:  s = sprShotgun; break;
            case SupplyType.Rocket:   s = sprRocket; break;
            case SupplyType.SMG:      s = sprSMG; break;
        }
        iconRenderer.sprite = s;
        iconRenderer.enabled = s != null;
    }

    void Update()
    {
        // 천천히 낙하
        transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
        // 수명/높이 한계
        if (Time.time >= dieAt || Mathf.Abs(transform.position.y) > killAbsY)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var t = _rolled;

        bool ok = (t == SupplyType.Heal) ? GiveHeal(other.gameObject)
                                         : GiveWeapon(other.gameObject, t);

        PlayPickupSfx(t);
        Destroy(gameObject);
    }

    SupplyType RollRandomType()
    {
        // Heal 포함. 0~6 중 하나 반환(Heal, Revolver, AR, SR, Shotgun, Rocket, SMG)
        int r = Random.Range(0, 7);
        return (SupplyType)r;
    }

    bool GiveHeal(GameObject player)
    {
        var ph = player.GetComponent<PlayerHealth>(); if (!ph) return false;

        // ★ 회복량 = [healMin ~ healMax] 난수
        int amount = Random.Range(Mathf.Min(healMin, healMax), Mathf.Max(healMin, healMax) + 1);
        int before = ph.currentHP;
        ph.currentHP = Mathf.Min(ph.maxHP, ph.currentHP + amount);

        // HUD 갱신
        ph.hud?.SetHP(ph.currentHP, ph.maxHP);
        Debug.Log($"[Supply] Heal +{amount} ({before}->{ph.currentHP})");
        return true;
    }
    bool GiveWeapon(GameObject player, SupplyType t)
    {
        // 1) 리짓바디 루트 기준으로 탐색
        Transform root = player.transform;
        var rb = player.GetComponentInParent<Rigidbody2D>();
        if (rb) root = rb.transform;

        // 2) 부모 체인에서 우선 탐색
        var mgr = root.GetComponentInParent<WP_Manager>();

        // 3) 실패 시 전역 폴백
    #if UNITY_2023_1_OR_NEWER
        if (!mgr) mgr = FindFirstObjectByType<WP_Manager>();
    #else
    #pragma warning disable CS0618
        if (!mgr) mgr = FindObjectOfType<WP_Manager>();
    #pragma warning restore CS0618
    #endif

        if (!mgr) { Debug.LogWarning("[Supply] WP_Manager 없음(루트/전역 탐색 실패)"); return false; }

        GameObject prefab = GetPrefabByType(t);
        if (!prefab) { Debug.LogWarning($"[Supply] {t} 프리팹 미지정"); return false; }

        bool added = mgr.AddWeapon(prefab, select:true);
        Debug.Log(added ? $"[Supply] {t} 지급 성공" : $"[Supply] {t} 지급 실패(빈 슬롯 없음?)");
        return added;
    }

    GameObject GetPrefabByType(SupplyType t)
    {
        switch (t)
        {
            case SupplyType.Revolver: return revolverPrefab;
            case SupplyType.AR:       return arPrefab;
            case SupplyType.SR:       return srPrefab;
            case SupplyType.Shotgun:  return shotgunPrefab;
            case SupplyType.Rocket:   return rocketPrefab;
            case SupplyType.SMG:      return smgPrefab;     // ★
            default: return null;
        }
    }

    void PlayPickupSfx(SupplyType t)
    {
        AudioClip clip = null;
        switch (t) {
            case SupplyType.Heal:     clip = sfxHeal; break;
            case SupplyType.Revolver: clip = sfxRevolver; break;
            case SupplyType.AR:       clip = sfxAR; break;
            case SupplyType.SR:       clip = sfxSR; break;
            case SupplyType.Shotgun:  clip = sfxShotgun; break;
            case SupplyType.Rocket:   clip = sfxRocket; break;
            case SupplyType.SMG:      clip = sfxSMG; break;
        }
        if (clip) AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume); 
    }
}
