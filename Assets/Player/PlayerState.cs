using UnityEngine;

#region 상태 열거형
public enum State { Idle, Move, Jump, LookUp, JumpLookUp, JumpLookDown }
#endregion

[RequireComponent(typeof(AudioSource))] // 오디오소스 필수
public class PlayerState : MonoBehaviour
{
    [Header("애니메이션 클립")]
    public AnimationClip idleClip;         // "Idle"
    public AnimationClip moveClip;         // "Move"
    public AnimationClip jumpClip;         // "Jump"
    public AnimationClip lookUpClip;       // "LookUp"
    public AnimationClip jumpLookUpClip;   // "JumpLookUp"
    public AnimationClip jumpLookDownClip; // "JumpLookDown"

    [Header("효과음(SFX, 비어있으면 생략)")]
    [SerializeField] private AudioSource sfx; // 재생용 AudioSource
    public AudioClip moveSFX;                 // 이동 발소리
    public AudioClip jumpSFX;                 // 1단 점프
    public AudioClip doubleJumpSFX;           // 2단 점프
    // ※ 무기 교체/드롭 SFX는 WP_Manager가 담당함. 여기서 다루지 않음

    // 내부 컴포넌트
    private Animator animator;
    private Animation legacyAnim;
    private SpriteRenderer spriteRenderer;

    // 상태 및 발소리 타이머
    private State currentState = State.Idle;
    private float footstepTimer = 0f;
    [SerializeField] private float footstepInterval = 0.5f;

    // Animator 레이어 인덱스(보통 0)
    private const int BaseLayer = 0;
    void Awake()
    {
        // AudioSource 자동 확보 및 2D 보장
        if (!sfx) sfx = GetComponent<AudioSource>();                // ★컴포넌트 가져오기
        if (!sfx) sfx = gameObject.AddComponent<AudioSource>();     // ★없으면 추가
        sfx.playOnAwake = false;                                    // ★자동 재생 끔
        sfx.spatialBlend = 0f;                                      // ★2D 모드
        sfx.volume = Mathf.Clamp01(sfx.volume <= 0f ? 1f : sfx.volume); // ★음량 보정

        // 이동 시작 시 바로 한 번 재생되도록 타이머 채움
        footstepTimer = footstepInterval;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        legacyAnim = GetComponent<Animation>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Legacy Animation 사용 시 클립 등록
        if (legacyAnim != null)
        {
            if (idleClip) legacyAnim.AddClip(idleClip, "Idle");                 // ★대기
            if (moveClip) { moveClip.wrapMode = WrapMode.Loop; legacyAnim.AddClip(moveClip, "Move"); } // ★이동 루프
            if (jumpClip) legacyAnim.AddClip(jumpClip, "Jump");                 // ★점프
            if (lookUpClip) legacyAnim.AddClip(lookUpClip, "LookUp");           // ★위보기
            if (jumpLookUpClip) legacyAnim.AddClip(jumpLookUpClip, "JumpLookUp");// ★공중 위보기
            if (jumpLookDownClip) legacyAnim.AddClip(jumpLookDownClip, "JumpLookDown");// ★공중 아래보기
        }
    }

    void Update()
    {
        // TODO: 실제 바닥 체크로 교체(캐릭터컨트롤러/레이캐스트 등)
        bool isGrounded = true;

        // 입력
        bool pressLeftRight = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f; // ★좌우
        bool pressUp = Input.GetKey(KeyCode.UpArrow);                           // ★위
        bool pressDown = Input.GetKey(KeyCode.DownArrow);                       // ★아래

        // 상태 판정
        State newState = currentState;
        if (!isGrounded)
        {
            if (pressUp) newState = State.JumpLookUp;
            else if (pressDown) newState = State.JumpLookDown;
            else newState = State.Jump;
        }
        else
        {
            if (pressUp) newState = State.LookUp;
            else if (pressLeftRight) newState = State.Move;
            else newState = State.Idle;
        }

        // 상태 변경 시 애니메이션 반영
        if (newState != currentState)
        {
            currentState = newState;
            if (animator) animator.Play(currentState.ToString());     // ★Animator 사용 시
            if (legacyAnim) legacyAnim.Play(currentState.ToString()); // ★Legacy 사용 시
        }

        // 이동 발소리(설정되어 있을 때만)
        if (currentState == State.Move && isGrounded && moveSFX)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepInterval)
            {
                PlaySFX(moveSFX);     // ★발소리
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = footstepInterval;   // ★이동 재개 시 즉시 재생
        }

    }

    /// <summary>
    /// SFX 재생 헬퍼. clip 또는 sfx가 없으면 아무 것도 하지 않음.
    /// </summary>
    private void PlaySFX(AudioClip clip)
    {
        if (!clip || !sfx) return; // 널 방어
        sfx.PlayOneShot(clip);     // 단발 재생
    }
    public void OnAnimEvent() { /* 빈 이벤트 처리용. 아무 것도 하지 않음. */ }

    void OnValidate()
    {
        // 에디터에서 인스펙터 비워도 자동 참조
        if (!sfx) sfx = GetComponent<AudioSource>();
        if (sfx) { sfx.playOnAwake = false; sfx.spatialBlend = 0f; }
    }


    /// <summary>
    /// 존재 검증 후 애니메이션 재생.
    /// - Animator가 있으면 Animator 우선 재생. 해당 스테이트가 없으면 스킵.
    /// - Animator가 없고 Legacy가 있으면 Legacy 클립 존재 시만 재생.
    /// - 둘 다 없거나 이름이 불일치하면 아무 것도 하지 않음.
    /// </summary>
    private void SafePlayAnimation(State state)
    {
        string name = state.ToString(); // "Idle" 등

        // ▼ Animator 우선
        if (animator != null)
        {
            int hash = Animator.StringToHash(name);
            if (animator.HasState(BaseLayer, hash))
            {
                animator.Play(name);
                return; // 성공
            }
        }

        // ▼ Legacy 차선
        if (legacyAnim != null)
        {
            // 등록된 클립이 실제로 존재할 때만 재생
            var clip = legacyAnim.GetClip(name);
            if (clip != null)
            {
                legacyAnim.Play(name);
                return; // 성공
            }
        }

        // ▼ 여기까지 왔다면 재생 자원이 없음(이름 불일치). 에러 대신 1회만 정보 로그.
        // 필요 시 아래 로그를 주석 처리해도 된다.
        Debug.Log($"[PlayerState] 애니메이션 '{name}' 를 찾지 못해 재생 생략.");
    }




}
