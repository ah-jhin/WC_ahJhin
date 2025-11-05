using UnityEngine;

/// <summary>
/// 보스/적(엔티티)용 기본 스크립트
/// - IDamageable 구현: 총알 등으로부터 피해 수신
/// - 체력/사망 연출
/// - 공격 프리팹 발사(타깃: Player / 좌표 / 무작위 각도)
/// </summary>
[AddComponentMenu("Game/Entity")]
[DisallowMultipleComponent]
public class entity : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [Tooltip("체력 시스템을 사용할지 여부")]
    public bool useHealth = true;
    [Tooltip("최대 체력")]
    public int maxHP = 100;
    [Tooltip("현재 체력(런타임 표시용)")]
    public int currentHP = 100;

    [Header("사망")]
    [Tooltip("사망 시 즉시 오브젝트를 제거할지")]
    public bool destroyOnDeath = true;
    [Tooltip("사망 시 생성 프리팹")]
    public GameObject deathPrefab;
    [Tooltip("사망 시 VFX")]
    public GameObject deathVFX;
    [Tooltip("사망 시 SFX")]
    public AudioClip deathSFX;

    [Header("공격")]
    [Tooltip("공격 기능을 사용할지")]
    public bool canAttack = false;
    [Tooltip("발사할 공격 프리팹(투사체 등)")]
    public GameObject attackPrefab;
    [Tooltip("생성된 공격체의 자동 소멸 시간(초)")]
    public float attackDespawn = 5f;
    [Tooltip("공격체 발사 속도(리짓바디2D가 있을 때 velocity로 적용)")]
    public float projectileSpeed = 12f;

    public enum TargetMode { Player, Coordinate, RandomAngle }
    [Tooltip("공격 타깃 방식")]
    public TargetMode targetMode = TargetMode.Player;

    [Tooltip("타깃: Player 태그명")]
    public string playerTag = "Player";
    [Tooltip("타깃: 좌표 모드에서 사용할 목표 지점")]
    public Vector2 targetPosition;
    [Tooltip("타깃: 무작위 각도(도 단위) 범위 [min, max], 0도=우측")]
    public Vector2 randomAngleRange = new Vector2(0f, 360f);

    [Header("공격 트리거")]
    [Tooltip("피해를 받으면 즉시 반격할지")]
    public bool attackOnDamaged = false;
    [Tooltip("반격/수동 공격 쿨다운(초)")]
    public float attackCooldown = 0.5f;

    [Header("효과음(SFX)")]
    public AudioSource sfx;
    public AudioClip attackSFX;

    bool _dead = false;
    float _nextAttackTime = 0f;
    Transform _playerCached; // Player 타깃 캐시

    void Awake()
    {
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        if (sfx == null) sfx = GetComponent<AudioSource>();
        if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
    }

    // ==========================
    // IDamageable 구현부
    // ==========================
    /// <summary>
    /// 외부에서 들어오는 피해(총알 등). weak/weakMultiplier는 필요 시 사용.
    /// </summary>
    public void TakeDamage(int amount, bool weak, float weakMultiplier)
    {
        if (!useHealth || _dead) return;

        // 약점 배율 적용(원치 않으면 amount만 사용)
        int final = amount + (weak ? Mathf.RoundToInt(weakMultiplier) : 0);
        currentHP = Mathf.Max(0, currentHP - Mathf.Max(0, final));

        // 맞으면 반격
        if (attackOnDamaged)
            TryAttack();

        // 체력 0 → 사망
        if (currentHP <= 0 && !_dead)
            Die();
    }

    /// <summary>레거시 시그니처 대응</summary>
    public void TakeDamage(int amount) { TakeDamage(amount, false, 0f); }

    // ==========================
    // 사망 처리
    // ==========================
    void Die()
    {
        _dead = true;

        // 연출
        if (deathPrefab) Instantiate(deathPrefab, transform.position, Quaternion.identity);
        if (deathVFX) Instantiate(deathVFX, transform.position, Quaternion.identity);
        if (deathSFX && sfx) sfx.PlayOneShot(deathSFX);

        // 콜라이더/렌더러 비활성화로 깔끔히 숨김(선택)
        var colls = GetComponentsInChildren<Collider2D>();
        foreach (var c in colls) c.enabled = false;
        var srs = GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in srs) r.enabled = false;

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    // ==========================
    // 공격 발사
    // ==========================
    /// <summary>
    /// 외부(애니메이션 이벤트, 타임라인, 스테이트 머신 등)에서 호출 가능
    /// </summary>
    public void TryAttack()
    {
        if (!canAttack || _dead) return;
        if (Time.time < _nextAttackTime) return;
        _nextAttackTime = Time.time + Mathf.Max(0f, attackCooldown);

        if (attackPrefab == null) return;

        // 발사 방향 계산
        Vector2 dir = GetFireDirection();
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right; // 안전장치

        // 탄 생성
        GameObject proj = Instantiate(attackPrefab, transform.position, Quaternion.FromToRotation(Vector3.right, dir));
        if (attackDespawn > 0f) Destroy(proj, attackDespawn);

        // 리지드바디가 있으면 속도 부여
        var prb = proj.GetComponent<Rigidbody2D>();
        if (prb) prb.linearVelocity = dir.normalized * projectileSpeed;

        // SFX
        if (attackSFX && sfx) sfx.PlayOneShot(attackSFX);
    }

    Vector2 GetFireDirection()
    {
        switch (targetMode)
        {
            case TargetMode.Player:
                {
                    // 플레이어 찾기/캐시
                    if (_playerCached == null)
                    {
                        var p = GameObject.FindGameObjectWithTag(playerTag);
                        if (p) _playerCached = p.transform;
                    }
                    if (_playerCached)
                        return ((Vector2)_playerCached.position - (Vector2)transform.position).normalized;
                    return Vector2.right;
                }
            case TargetMode.Coordinate:
                return (targetPosition - (Vector2)transform.position).normalized;

            case TargetMode.RandomAngle:
                float ang = Random.Range(randomAngleRange.x, randomAngleRange.y);
                float rad = ang * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        return Vector2.right;
    }
}
