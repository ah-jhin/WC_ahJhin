using UnityEngine;

/// <summary>
/// 무기 스탯(Inspector에서 설정하는 SO).
/// 피해=정수 랜덤, 약점=정수 보너스(더하기), 발사간격·탄퍼짐·탄수·사거리·탄약·아이콘·SFX 포함
/// </summary>
[CreateAssetMenu(menuName="WC/WeaponData")]
public class WP_Data : ScriptableObject
{
    [Header("표시/HUD")]
    public string displayName = "Pistol";   // HUD 표기 이름
    public Sprite icon;                     // HUD 아이콘

    [Header("피해(기본)")]
    public int minDamage = 2;               // 최소 피해
    public int maxDamage = 4;               // 최대 피해

    [Header("약점 보너스(더하기)")]
    public int weakBonusMin = 0;            // 최소 보너스
    public int weakBonusMax = 0;            // 최대 보너스

    [Header("발사 설정")]
    public float fireInterval = 0.15f;      // 발사 간격(초) ← 자동연사에 사용
    public int bulletsPerShot = 1;          // 1회 발사 탄수(샷건 등)
    public float spreadDeg = 0f;            // 퍼짐(도 단위, 0=직선)

    [Header("발사체")]
    public GameObject bulletPrefab;         // 탄 프리팹(구슬/직선 등)
    public float bulletSpeed = 22f;         // 탄속
    public float bulletLifetime = 2.5f;     // 사거리(존속 시간)

    [Header("탄약")]
    public bool infiniteAmmo = true;        // 권총=무한
    public int ammoInitial = 0;             // 무한이 아니면 지급 탄약

    [Header("SFX")]
    public AudioClip sfxShoot;              // 발사음(선택)
}
