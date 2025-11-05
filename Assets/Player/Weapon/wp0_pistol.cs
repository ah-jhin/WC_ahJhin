using UnityEngine;

/// <summary>
/// 권총 로직(데이터는 WP_Data에서 모두 읽음)
/// - 약점은 "정수 보너스 더하기"로 주입한다(곱셈 아님).
/// - bulletsPerShot, spreadDeg, fireInterval, ammo, 아이콘까지 SO에서 처리.
/// </summary>
public class WP_Pistol : MonoBehaviour, IWeaponInfo
{
    [Header("삽입")]
    public WP_Data data;              // ← 반드시 연결
    public Transform firePoint;       // 총구(자동탐색)
    public PlayerMovement pm;         // 조준/방향(선택)
    public AudioSource sfx;           // 사운드(선택)
    public float spawnOffset = 0.6f;  // 총구 앞쪽으로 밀어 생성(자기충돌 방지)

    int _ammo;                        // 현재 탄약(무한이면 0 유지)

    void Awake()
    {
        if (!pm) pm = GetComponentInParent<PlayerMovement>();
        if (!firePoint) ResolveFirePoint();
        // 탄약 초기화
        _ammo = (data && !data.infiniteAmmo) ? Mathf.Max(0, data.ammoInitial) : 0;
    }

    /// <summary>WP_Manager가 호출하는 발사 함수</summary>
    public void Shoot()
    {
        if (!data || !data.bulletPrefab || !firePoint) return;

        // 탄약 체크(무한이 아니면 감소)
        if (!data.infiniteAmmo && _ammo <= 0) return;

        // 발사 방향(위/아래/좌우 포함)
        Vector2 baseDir = pm ? pm.GetAimDir() : (transform.lossyScale.x >= 0 ? Vector2.right : Vector2.left);
        baseDir.Normalize();

        // 1회 발사 탄수만큼 반복(샷건 등)
        for (int i = 0; i < Mathf.Max(1, data.bulletsPerShot); i++)
        {
            // 퍼짐 적용: Z축 회전으로 2D 방향 회전
            float ang = data.spreadDeg <= 0f ? 0f : Random.Range(-data.spreadDeg, data.spreadDeg);
            Vector2 dir = Quaternion.Euler(0, 0, ang) * baseDir;

            // 스폰 위치
            Vector3 pos = firePoint.position + (Vector3)(dir * spawnOffset);

            // 총알 생성
            GameObject go = Instantiate(data.bulletPrefab, pos, Quaternion.identity);

            // 피해 샘플링 + 약점 보너스 범위 준비(보너스 값 전달)
            int baseDmg = Random.Range(data.minDamage, data.maxDamage + 1);
            int weakBonus = Random.Range(data.weakBonusMin, data.weakBonusMax + 1);

            // 총알 주입
            var b = go.GetComponent<Bullet>();
            if (b)
            {
                b.SetLifetime(data.bulletLifetime);    // 1) 수명 먼저 세팅
                b.Inject(baseDmg, weakBonus);          // 2) ★ 피해/보너스 주입 → Arm()되어 충돌 활성
            }
            else
            {
                // 안전망: Bullet 스크립트가 없을 때도 사라지게
                if (data.bulletLifetime > 0f) Destroy(go, data.bulletLifetime);
            }

            // 속도 부여
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.linearVelocity = dir * data.bulletSpeed;
            }

            // 초기 자기충돌 무시(플레이어와)
            var bulletCol = go.GetComponent<Collider2D>();
            if (bulletCol)
            {
                var root = pm ? pm.transform.root : transform.root;
                foreach (var pc in root.GetComponentsInChildren<Collider2D>())
                    Physics2D.IgnoreCollision(bulletCol, pc, true);
            }
        }

        // 탄약 소모
        if (!data.infiniteAmmo) _ammo = Mathf.Max(0, _ammo - 1);

        // SFX
        if (data.sfxShoot)
        {
            Vector3 pos = Camera.main ? Camera.main.transform.position : transform.position; // 주석: 2D 청취 위치
            AudioSource.PlayClipAtPoint(data.sfxShoot, pos, 0.6f);  // ★ 주석: 무기 파괴와 무관하게 끝까지 재생
        }

        // 탄약 소진 시 자기 제거(명세: 탄약 소진하면 사라짐)
        if (!data.infiniteAmmo && _ammo == 0)
            SendMessageUpwards("OnWeaponEmpty", this, SendMessageOptions.DontRequireReceiver);
    }

    bool ResolveFirePoint()
    {
        // 부모 계층에서 "FirePoint" 탐색
        Transform p = transform;
        while (p != null)
        {
            var t = p.Find("FirePoint");
            if (t) { firePoint = t; return true; }
            p = p.parent;
        }
        return false;
    }

    // === IWeaponInfo(HUD/매니저용 정보 제공) ===
    public string DisplayName => data ? data.displayName : "Weapon";
    public Sprite Icon => data ? data.icon : null;
    public int Ammo => data && !data.infiniteAmmo ? _ammo : 0;
    public bool IsInfinite => data && data.infiniteAmmo;
    public float FireInterval => data ? Mathf.Max(0.01f, data.fireInterval) : 0.2f; // 매니저 쿨타임에 사용
}
