using UnityEngine;

/// <summary>
/// 발사체(2D)
/// - 무기에서 SetLifetime → Inject 순으로 호출해야 함
/// - Inject 전에는 충돌을 비활성(armed=false)하여 잘못된 피격을 막음
/// - 약점은 "보너스 더하기"로 처리
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [Header("수명(초) — SO에서 덮어씀")]
    public float lifetime = 2.5f;         // 기본값. 무기에서 SetLifetime으로 재설정

    [Header("디폴트 피해(비상용)")]
    public int defaultDamage = 1;         // Inject 누락 시 사용할 최소값

    // 내부 상태
    int damage;                            // 기본 피해
    int weakBonus;                         // 약점 보너스(+)
    bool armed = false;                    // Inject 호출 전까지 비무장
    bool warned = false;                   // 경고 1회용

    [Header("폭발(로켓 전용)")]
    public bool aoeOnHit = false;          // true면 범위피해
    public float aoeRadius = 2.5f;         // 반경(월드 단위)
    public LayerMask aoeMask = ~0;         // 감지 레이어(기본 전체)
    public GameObject explosionFx;         // 폭발 이펙트(선택)
    public AudioClip explosionSfx;         // 폭발 SFX(선택)
    Collider2D col;
    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col) col.enabled = false;     // ■ Inject 전 충돌 금지
    }

    void OnEnable()
    {
        // 프리팹 기본값으로도 동작. 무기에서 SetLifetime을 다시 호출하면 타이머 갱신됨.
        CancelInvoke(nameof(Die));
        if (lifetime > 0f) Invoke(nameof(Die), lifetime);
    }

    /// <summary>
    /// 무기에서 수명을 재설정한다.
    /// </summary>
    public void SetLifetime(float seconds)
    {
        lifetime = seconds;
        CancelInvoke(nameof(Die));
        if (lifetime > 0f) Invoke(nameof(Die), lifetime);
    }

    /// <summary>
    /// 무기에서 피해/약점 보너스를 주입한다. 호출 시 충돌을 활성화(무장)한다.
    /// </summary>
    public void Inject(int baseDamage, int weakAdd)
    {
        damage = Mathf.Max(0, baseDamage);
        weakBonus = Mathf.Max(0, weakAdd);
        Arm();                              // ■ 무장 + 타이머 보강
    }

    void Arm()
    {
        armed = true;
        if (col) col.enabled = true;        // ■ 이제 충돌 허용
        // 안전: 수명 타이머 재보장
        CancelInvoke(nameof(Die));
        if (lifetime > 0f) Invoke(nameof(Die), lifetime);
    }

    void Die()
    {
        if (this) Destroy(gameObject);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed)
        {
            if (!warned) { Debug.LogWarning("[Bullet] Inject() 호출 전 충돌"); warned = true; }
            return;
        }

        // 약점 판정(태그로만)
        bool isWeak = other.CompareTag("WeakPoint");

        if (aoeOnHit) // ★ 로켓 등
        {
            // 폭발 이펙트/SFX
            if (explosionFx) Instantiate(explosionFx, transform.position, Quaternion.identity);
            if (explosionSfx)
            {
                Vector3 p = Camera.main ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(explosionSfx, p, 1f);
            }

            // 중복 타격 방지용
            System.Collections.Generic.HashSet<IDamageable> hit = new System.Collections.Generic.HashSet<IDamageable>();
            var cols = Physics2D.OverlapCircleAll(transform.position, aoeRadius, aoeMask);
            foreach (var c in cols)
            {
                var t = c.GetComponent<IDamageable>();
                if (t == null || hit.Contains(t)) continue;

                bool weakHere = c.CompareTag("WeakPoint"); // 범위 내 약점 콜라이더는 약점처리
                t.TakeDamage(damage, weakHere, weakBonus);
                hit.Add(t);
            }
        }
        else // 단일 타격 탄
        {
            var target = other.GetComponent<IDamageable>();
            if (target != null)
                target.TakeDamage(damage, isWeak, weakBonus);
        }

        Die(); // 충돌 후 소멸
    }

}