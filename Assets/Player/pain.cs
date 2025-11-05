using UnityEngine;

/// <summary>
/// 오브젝트와 '접촉' 시 플레이어에게 피해를 주는 함정/투사체용 스크립트  
/// - 태그: Player, block 사용  
/// - 이 스크립트의 '무적 시간'이 PlayerHealth 무적보다 우선한다(오버로드 호출).  
/// - 트리거/충돌 둘 다 지원(OnTriggerEnter2D / OnCollisionEnter2D)  
/// - 투사체 속도, 좌표 추적, 플레이어 추적, 관성(중력), 크기 증가 기능 추가  
/// </summary>
[AddComponentMenu("Game/Pain")]
[DisallowMultipleComponent]
public class pain : MonoBehaviour
{
    [Header("수치")]
    [Tooltip("최소 피해값")]
    public int minDamage = 5;
    [Tooltip("최대 피해값")]
    public int maxDamage = 10;
    [Tooltip("넉백 힘(좌/우는 랜덤, 위로 50% 가산)")]
    public float knockback = 10f;
    [Tooltip("같은 함정에 재접촉 시 이 시간 간격으로만 피해 적용(초)")]
    public float localInvincible = 0.4f;

    [Header("접촉(플레이어)")]
    [Tooltip("접촉 시 즉시 소멸할지 여부")]
    public bool destroyOnTouch = false;
    [Tooltip("접촉 시 생성될 일반 프리팹(데칼 등)")]
    public GameObject touchPrefab;
    [Tooltip("접촉 시 VFX 프리팹")]
    public GameObject touchVFX;
    [Tooltip("접촉 시 SFX")]
    public AudioClip touchSFX;

    [Header("물리")]
    [Tooltip("중력을 사용할지 여부")]
    public bool useGravity = false;
    [Tooltip("중력 값(리지드바디2D의 gravityScale로 반영)")]
    public float gravityScale = 1f;
    [Tooltip("블럭(tag=block)과 충돌 체크할지 여부")]
    public bool checkBlockCollision = true;
    [Tooltip("블럭과 충돌 시 소멸할지 여부")]
    public bool destroyOnBlock = true;
    [Tooltip("블럭 충돌 시 생성할 일반 프리팹")]
    public GameObject blockHitPrefab;
    [Tooltip("블럭 충돌 시 VFX")]
    public GameObject blockHitVFX;
    [Tooltip("블럭 충돌 시 SFX")]
    public AudioClip blockHitSFX;

    [Header("이동")]
    [Tooltip("투사체 이동 속도")]
    public float speed = 0f;
    [Tooltip("추적할 목표 좌표 (월드 좌표)")]
    public Vector2 targetPosition;
    [Tooltip("플레이어를 지속 추적할지 여부")]
    public bool trackPlayer = false;
    [Tooltip("관성(물리) 적용 여부")]
    public bool useInertia = false;
    [Tooltip("관성/중력 값 (Rigidbody2D.gravityScale로 적용)")]
    public float inertiaValue = 1f;
    [Tooltip("시간 경과에 따라 투사체 크기 증가 여부")]
    public bool increaseSize = false;

    [Header("소멸(타이머)")]
    [Tooltip("시간 경과로 자동 소멸할지 여부")]
    public bool autoDespawn = false;
    [Tooltip("생성 후 n초 뒤 자동 소멸")]
    public float despawnAfter = 10f;
    [Tooltip("소멸 시 생성할 일반 프리팹")]
    public GameObject despawnPrefab;
    [Tooltip("소멸 시 VFX")]
    public GameObject despawnVFX;
    [Tooltip("소멸 시 SFX")]
    public AudioClip despawnSFX;

    [Header("태그 설정")]
    [Tooltip("플레이어 태그명")]
    public string playerTag = "Player";
    [Tooltip("블럭 태그명")]
    public string blockTag = "block";

    // 내부 구성요소
    Rigidbody2D _rb;
    Collider2D _col;
    AudioSource _audio;
    // 이동/추적 관련 내부 변수
    Transform _playerTransform;  // 플레이어 추적 대상 Transform
    bool _arrived = false;       // 목표 지점 도달 여부

    // 이 함정 자체의 쿨다운(한 플레이어 게임 가정)
    float _nextTouchTime = 0f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        // 관성/중력 설정 및 Rigidbody2D 구성
        if (useInertia)
        {
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.gravityScale = inertiaValue;
        }
        else if (useGravity)
        {
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.gravityScale = gravityScale;
        }
        else if (_rb != null)
        {
            _rb.gravityScale = 0f;
        }
        // 이동 기능 사용 시 Rigidbody2D 생성 보장
        if (_rb == null && (speed != 0f || trackPlayer || targetPosition != Vector2.zero))
        {
            _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
        }

        // 자동 소멸 타이머
        if (autoDespawn)
            Invoke(nameof(DoDespawn), Mathf.Max(0.01f, despawnAfter));
    }

    void Start()
    {
        // 플레이어 추적 대상 찾기
        if (trackPlayer)
        {
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null)
                _playerTransform = playerObj.transform;
        }

        // 초기 이동 속도 설정 (추적 없음 상태에서 발사 방향 지정)
        if (!trackPlayer && targetPosition == Vector2.zero && speed != 0f && _rb != null)
        {
            // 오브젝트가 바라보는 방향으로 초기 속도 부여
            _rb.linearVelocity = transform.right * speed;
        }
    }

    void FixedUpdate()
    {
        // 좌표 추적용 도달 여부 확인 (이미 도달한 경우 이동 중지)
        if (_arrived) return;

        // 1) 플레이어 지속 추적
        if (trackPlayer && _playerTransform != null)
        {
            // 플레이어 방향 단위 벡터 계산
            Vector2 dir = (_playerTransform.position - transform.position).normalized;
            if (_rb != null)
            {
                if (useInertia || useGravity)
                {
                    // 관성/중력 적용: 수평 속도만 목표 방향으로 조절 (수직은 중력에 맡김)
                    float targetVelX = dir.x * speed;
                    if (useInertia)
                    {
                        // 부드러운 추적: 수평 속도를 서서히 변경
                        _rb.linearVelocity = new Vector2(Mathf.MoveTowards(_rb.linearVelocity.x, targetVelX, speed * 2f * Time.fixedDeltaTime), _rb.linearVelocity.y);
                    }
                    else
                    {
                        // 즉시 추적: 수평 속도를 즉시 변경
                        _rb.linearVelocity = new Vector2(targetVelX, _rb.linearVelocity.y);
                    }
                }
                else
                {
                    // 중력 미사용: 목표 방향으로 바로 이동
                    _rb.linearVelocity = dir * speed;
                }
            }
            else
            {
                // (예외 처리) Rigidbody2D가 없을 경우 Transform 이동
                transform.position = Vector2.MoveTowards(transform.position, _playerTransform.position, speed * Time.deltaTime);
            }
        }
        // 2) 지정된 좌표로 이동
        else if (!trackPlayer && targetPosition != Vector2.zero)
        {
            Vector2 targetPos = targetPosition;
            float dist = Vector2.Distance(transform.position, targetPos);
            if (dist <= speed * Time.fixedDeltaTime)
            {
                // 목표 지점 도달: 정확한 위치로 설정하고 정지
                transform.position = targetPos;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector2.zero;
                    // 관성/중력 사용 중이면 중력 비활성화 (제자리 유지)
                    if (useInertia || useGravity) _rb.gravityScale = 0f;
                }
                _arrived = true;
            }
            if (!_arrived)
            {
                // 목표를 향해 이동 지속
                Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
                if (_rb != null)
                {
                    if (useInertia || useGravity)
                    {
                        float targetVelX = dir.x * speed;
                        if (useInertia)
                        {
                            _rb.linearVelocity = new Vector2(Mathf.MoveTowards(_rb.linearVelocity.x, targetVelX, speed * 2f * Time.fixedDeltaTime), _rb.linearVelocity.y);
                        }
                        else
                        {
                            _rb.linearVelocity = new Vector2(targetVelX, _rb.linearVelocity.y);
                        }
                    }
                    else
                    {
                        _rb.linearVelocity = dir * speed;
                    }
                }
                else
                {
                    transform.position = Vector2.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
                }
            }
        }
        // 3) 이동/추적 기능 없음
        else
        {
            // 초기 속도나 중력에 의한 이동만 처리됨 (추가 업데이트 불필요)
        }
    }

    void Update()
    {
        // 크기 증가 처리
        if (increaseSize)
        {
            // 시간이 지날수록 투사체 크기 증가 (초당 약 0.1 단위)
            transform.localScale += Vector3.one * 0.1f * Time.deltaTime;
        }
    }

    // --------- 트리거 방식 ----------
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            TryDamagePlayer(other.GetComponent<PlayerHealth>());
        else if (checkBlockCollision && other.CompareTag(blockTag))
            OnBlockHit();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            TryDamagePlayer(other.GetComponent<PlayerHealth>());
    }

    // --------- 충돌 방식 ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(playerTag))
            TryDamagePlayer(collision.collider.GetComponent<PlayerHealth>());
        else if (checkBlockCollision && collision.collider.CompareTag(blockTag))
            OnBlockHit();
    }

    /// <summary>
    /// 플레이어에게 피해 시도. 이 스크립트의 쿨다운(localInvincible) 기준으로만 제한.
    /// PlayerHealth 무적시간은 '무시'하도록 오버로드를 호출한다.
    /// </summary>
    void TryDamagePlayer(PlayerHealth ph)
    {
        if (ph == null) return;

        // 이 함정의 로컬 쿨다운
        if (Time.time < _nextTouchTime) return;
        _nextTouchTime = Time.time + Mathf.Max(0f, localInvincible);

        // 피해 적용: PlayerHealth 무적을 '무시'하는 오버로드 사용
        ph.ApplyPain(minDamage, maxDamage, knockback, transform.position, true);

        // 접촉 연출
        SpawnAll(touchPrefab, touchVFX, touchSFX);

        if (destroyOnTouch)
            DoDespawn();
    }

    /// <summary>블럭(tag=block) 충돌 처리</summary>
    void OnBlockHit()
    {
        SpawnAll(blockHitPrefab, blockHitVFX, blockHitSFX);
        if (destroyOnBlock)
            DoDespawn();
    }

    /// <summary>소멸 처리 + 연출</summary>
    void DoDespawn()
    {
        SpawnAll(despawnPrefab, despawnVFX, despawnSFX);
        Destroy(gameObject);
    }

    /// <summary>프리팹, VFX, SFX를 한 번에 처리</summary>
    void SpawnAll(GameObject prefab, GameObject vfx, AudioClip sfx)
    {
        if (prefab) Instantiate(prefab, transform.position, Quaternion.identity);
        if (vfx) Instantiate(vfx, transform.position, Quaternion.identity);
        if (sfx && _audio) _audio.PlayOneShot(sfx);
    }
}
