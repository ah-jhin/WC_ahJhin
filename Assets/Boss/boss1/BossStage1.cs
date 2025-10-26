using UnityEngine;
using System.Collections;

/// <summary>
/// 스테이지1 보스(패턴 전용)
/// - 스카이 스킬: 발동 후 '고정 쿨타임'(기본 7초) / 옵션으로 랜덤 쿨 전환 가능
/// - 일렉트릭 빔: 스카이와 독립 쿨, 동시에도 가능 / 일정 확률로 2연속
/// - 스카이 중에는 지상 고정 스폰에서 빔을 쏘도록 선택 가능(forceGroundBeamWhenInSky)
/// </summary>
public class BossStage1 : BossBase
{
    [Header("Electric Sweep (Boss-local spawns)")]
    public Transform beamSpawnL;                  // 보스 기준(좌)
    public Transform beamSpawnR;                  // 보스 기준(우)
    public GameObject electricBeamPrefab;
    public float beamSpeed = 8f;
    [Tooltip("참고값(구 루프용). 랜덤 패턴으로 교체됨")]
    public float beamInterval = 7f;               // (참고)
    public int beamCount = 1;                     // 한 트리거 내 같은 방향으로 몇 줄
    public float beamGap = 0.7f;                  // 같은 트리거 내 간격

    [Header("Stage Beam Spawns (ground-fixed)")]
    [Tooltip("지상에 박아두는 스폰 포인트(씬 오브젝트). 스카이 중에 여기를 사용해 빔이 하늘로 따라가지 않게 함")]
    public Transform stageBeamSpawnL;             // 지상 고정(좌)
    public Transform stageBeamSpawnR;             // 지상 고정(우)
    [Tooltip("체크 시 스카이 중에는 반드시 지상 스폰을 사용")]
    public bool forceGroundBeamWhenInSky = true;

    [Header("HP Override(선택)")]
    public int overrideMaxHP = 0;

    [Header("Attack (Ground / Normal)")]
    public Transform firePoint;
    public GameObject normalBulletPrefab;
    public float bulletSpeed = 12f;
    public float attackCooldown = 1.0f;
    public float attackRange = 30f;
    public float rangeMargin = 2f;
    public bool useLeadShot = false;
    [Range(0f, 1f)] public float leadStrength = 0.6f;
    private float _nextAttackTime;

    private Transform _player;
    private Rigidbody2D _playerRb;

    [Header("Sky Skill")]
    public Transform[] skyPoints;
    [Tooltip("참고값(구 루프용). 랜덤 패턴으로 교체됨")]
    public float skillInterval = 6f;              // (참고)
    public float popJumpHeight = 0.6f;
    public AnimationCurve popCurve;
    public float skyFireRate = 0.25f;
    public float skyAimJitter = 6f;
    public int skyShotCount = 8;
    public GameObject skyBulletPrefab;
    public float skyBulletSpeed = 10f;
    public bool returnToGround = true;

    [Header("Skill Flash")]
    public GameObject skillFlashPrefab;
    private GameObject _skillFlash;

    // ===== 랜덤/고정 쿨 옵션 =====
    [Header("Sky Cooldown Mode")]
    [Tooltip("켜면 스카이는 '사용 종료 후 정확히 skyCooldown초' 후 재사용(기본 7초). 끄면 아래 랜덤 범위 사용")]
    public bool useFixedSkyCooldown = true;
    public float skyCooldown = 7f;                // 고정 7초

    [Tooltip("랜덤 쿨을 쓰고 싶을 때만 사용하는 범위(초)")]
    public float skyCDMin = 5f;
    public float skyCDMax = 9f;

    [Header("Beam Cooldown (Random)")]
    public float beamCDMin = 3f;
    public float beamCDMax = 6f;
    [Range(0f, 1f), Tooltip("빔 2연속 확률(낮춤). 기본 0.15 = 15%")]
    public float doubleBeamChance = 0.15f;
    [Tooltip("플레이어 대시 쿨(연속 빔 최소 간격 보장)")]
    public float dashCooldown = 1f;

    // 내부 스케줄
    private float _nextSkyTime;
    private float _nextBeamTime;

    private bool _inSkySkill = false;
    private Vector2 _groundPos;
    private Coroutine _patternLoopCo;

    protected override void Start()
    {
        if (overrideMaxHP > 0) maxHP = overrideMaxHP;
        base.Start();

        var pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj)
        {
            _player = pObj.transform;
            _playerRb = pObj.GetComponent<Rigidbody2D>();
        }

        if (popCurve == null)
            popCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.4f, 1), new Keyframe(1, 0));

        ScheduleNextSky();     // 초기 예약
        ScheduleNextBeam();    // 초기 예약
        _patternLoopCo = StartCoroutine(PatternLoop());
    }

    void Update()
    {
        // 지상 평사격
        if (_inSkySkill) return;
        if (!_player || !firePoint || !normalBulletPrefab) return;

        Vector2 center = firePoint.position;
        float dist = Vector2.Distance(center, _player.position);
        if (dist > attackRange + rangeMargin) return;
        if (Time.time < _nextAttackTime) return;

        _nextAttackTime = Time.time + attackCooldown;
        ShootNormal();
    }

    // === 패턴 루프: 스카이/빔 독립 스케줄 ===
    private IEnumerator PatternLoop()
    {
        while (true)
        {
            float now = Time.time;

            // 빔: 스카이와 동시 가능
            if (now >= _nextBeamTime)
            {
                StartCoroutine(DoElectricSweepRandom());
                ScheduleNextBeam(); // 빔은 즉시 다음 쿨 예약
            }

            // 스카이: 사용 후 쿨
            if (!_inSkySkill && now >= _nextSkyTime)
            {
                StartCoroutine(DoSkySkill());
                // 다음 쿨 예약은 스카이 종료 시점에 수행
            }

            yield return null;
        }
    }

    private void ScheduleNextSky()
    {
        if (useFixedSkyCooldown)
            _nextSkyTime = Time.time + skyCooldown; // 정확히 7초
        else
            _nextSkyTime = Time.time + Random.Range(skyCDMin, skyCDMax);
    }

    private void ScheduleNextBeam()
    {
        _nextBeamTime = Time.time + Random.Range(beamCDMin, beamCDMax);
    }

    /// <summary>지상 일반 사격</summary>
    private void ShootNormal()
    {
        Vector2 origin = firePoint.position;
        Vector2 target = _player ? (Vector2)_player.position : origin;
        Vector2 dir = (target - origin).normalized;

        if (useLeadShot && _playerRb)
        {
            Vector2 toTarget = target - origin;
            float t = toTarget.magnitude / Mathf.Max(0.01f, bulletSpeed);
            Vector2 leadPos = (Vector2)_player.position + _playerRb.linearVelocity * t * leadStrength;
            dir = (leadPos - origin).normalized;
        }

        var b = Instantiate(normalBulletPrefab, origin, Quaternion.identity);
        var rb = b.GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = dir * bulletSpeed;
    }

    /// <summary>스카이 스킬(연출+탄막) — 종료 후 쿨 예약</summary>
    private IEnumerator DoSkySkill()
    {
        if (skyPoints == null || skyPoints.Length == 0) yield break;

        _inSkySkill = true;
        _groundPos = transform.position;

        // 순간이동 + 팝 점프
        Transform t = skyPoints[Random.Range(0, skyPoints.Length)];
        Vector2 skyPos = t.position;
        var sr = GetComponentInChildren<SpriteRenderer>();

        if (sr) sr.enabled = false;
        yield return new WaitForSeconds(0.05f);
        transform.position = skyPos;
        if (sr) sr.enabled = true;
        yield return StartCoroutine(PopJump());

        // 플래시 FX
        if (skillFlashPrefab && _skillFlash == null)
        {
            _skillFlash = Instantiate(skillFlashPrefab, transform.position, Quaternion.identity, transform);
            _skillFlash.transform.localPosition = Vector3.zero;
            var fx = _skillFlash.GetComponent<FlashFX>();
            if (fx) fx.Play();
        }

        // 하늘 연사
        yield return StartCoroutine(SkyShootBurst(skyShotCount));

        // 플래시 정리
        if (_skillFlash)
        {
            var fx = _skillFlash.GetComponent<FlashFX>();
            if (fx) yield return StartCoroutine(fx.StopAndFade());
            else Destroy(_skillFlash);
            _skillFlash = null;
        }

        // 귀환
        if (returnToGround)
        {
            if (sr) sr.enabled = false;
            yield return new WaitForSeconds(0.05f);
            transform.position = _groundPos;
            if (sr) sr.enabled = true;
        }

        _inSkySkill = false;

        // 사용 후 쿨 예약
        ScheduleNextSky();
    }

    /// <summary>위치 점프 연출</summary>
    private IEnumerator PopJump()
    {
        float t = 0f;
        Vector3 basePos = transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            float yOff = popCurve.Evaluate(Mathf.Clamp01(t)) * popJumpHeight;
            transform.position = new Vector3(basePos.x, basePos.y + yOff, basePos.z);
            yield return null;
        }
        transform.position = basePos;
    }

    /// <summary>하늘에서 연사</summary>
    private IEnumerator SkyShootBurst(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_player && firePoint)
            {
                Vector2 origin = firePoint.position;
                Vector2 target = _player.position;
                Vector2 dir = (target - origin).normalized;

                float jitter = Random.Range(-skyAimJitter, skyAimJitter) * Mathf.Deg2Rad;
                Vector2 jitterDir = new(
                    dir.x * Mathf.Cos(jitter) - dir.y * Mathf.Sin(jitter),
                    dir.x * Mathf.Sin(jitter) + dir.y * Mathf.Cos(jitter)
                );
                jitterDir.Normalize();

                GameObject prefab = skyBulletPrefab ? skyBulletPrefab : normalBulletPrefab;
                float speed = skyBulletPrefab ? skyBulletSpeed : bulletSpeed;

                if (prefab)
                {
                    var b = Instantiate(prefab, origin, Quaternion.identity);
                    var rb = b.GetComponent<Rigidbody2D>();
                    if (rb) rb.linearVelocity = jitterDir * speed;
                }
            }
            yield return new WaitForSeconds(skyFireRate);
        }
    }

    // === 랜덤 빔 스윕(1회 또는 2연속) ===
    private IEnumerator DoElectricSweepRandom()
    {
        if (!electricBeamPrefab) yield break;

        bool fromLeft = (Random.value < 0.5f);
        int burst = (Random.value < doubleBeamChance) ? 2 : 1;
        float gap = Mathf.Max(beamGap, dashCooldown);

        for (int i = 0; i < burst; i++)
        {
            // 같은 트리거 내에서 멀티빔
            SpawnBeam(fromLeft);
            for (int k = 1; k < Mathf.Max(1, beamCount); k++)
            {
                yield return new WaitForSeconds(beamGap);
                SpawnBeam(fromLeft);
            }

            if (burst > 1) fromLeft = !fromLeft; // 2연속이면 방향 뒤집기

            if (i < burst - 1) yield return new WaitForSeconds(gap);
        }
    }

    // ★ 스폰 위치 선택 로직: 스카이 중에는 지상 고정 스폰 우선 사용
    private void SpawnBeam(bool fromLeft)
    {
        if (!electricBeamPrefab) return;

        Transform bossLocal = fromLeft ? beamSpawnL : beamSpawnR;
        Transform stageFixed = fromLeft ? stageBeamSpawnL : stageBeamSpawnR;

        Transform spawn;
        if (_inSkySkill && forceGroundBeamWhenInSky && stageFixed != null)
        {
            // 스카이 중에는 지상 고정 스폰 사용 (빔이 하늘로 따라가지 않도록)
            spawn = stageFixed;
        }
        else
        {
            // 평소엔 보스 기준 스폰(또는 지상 스폰이 없을 때 대체)
            spawn = bossLocal != null ? bossLocal : stageFixed;
        }

        if (!spawn) return;

        var go = Instantiate(electricBeamPrefab, spawn.position, Quaternion.identity);
        var beam = go.GetComponent<ElectricBeam>();
        if (beam)
        {
            beam.moveDir = fromLeft ? Vector2.right : Vector2.left;
            beam.speed = beamSpeed;
        }
    }
}
