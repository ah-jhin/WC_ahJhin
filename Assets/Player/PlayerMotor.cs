using UnityEngine;

/// <summary>
/// 간단 이동 + 지면/더블점프 + SFX + 애니메이션 전환
/// 초보자용: 인스펙터에 클립·SFX만 넣으면 동작
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animation), typeof(AudioSource))]
public class PlayerMotor : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 6f;          // 수평 이동 속도
    public float jumpPower = 11f;         // 점프 힘
    public int maxJumps = 2;              // 점프 가능 횟수(2면 더블점프)

    [Header("지면 판정")]
    public Transform groundCheck;         // 발끝 기준점
    public LayerMask groundMask;          // 지면 레이어
    public float groundRadius = 0.06f;    // 판정 반경

    [Header("애니메이션 클립")]
    public AnimationClip idleClip;        // Idle.anim
    public AnimationClip moveClip;        // Move.anim

    [Header("SFX")]
    public AudioClip moveSfx;             // 이동 발소리(선택)
    public AudioClip jumpSfx;             // 1단 점프
    public AudioClip doubleJumpSfx;       // 2단 점프
    public float footstepInterval = 0.45f;// 발소리 주기

    Rigidbody2D rb;
    Animation anim;
    AudioSource au;
    int jumpCount = 0;
    float stepTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animation>();
        au = GetComponent<AudioSource>();

        // 애니메이션 등록
        if (idleClip) anim.AddClip(idleClip, idleClip.name);
        if (moveClip) anim.AddClip(moveClip, moveClip.name);
        if (idleClip) anim.Play(idleClip.name); // 시작은 Idle
    }

    void Update()
    {
        // 1) 입력
        float x = Input.GetAxisRaw("Horizontal"); // A/D 또는 ←/→
        bool jumpPressed = Input.GetKeyDown(KeyCode.X); // 점프 키: X

        // 2) 이동
        rb.linearVelocity = new Vector2(x * moveSpeed, rb.linearVelocity.y);

        // 3) 좌우 바라보기(스프라이트 뒤집기)
        if (x != 0) transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);

        // 4) 지면 체크
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundMask);

        if (grounded) jumpCount = 0; // 땅에 닿으면 점프 횟수 초기화

        // 5) 점프 처리: 가능한 경우에만 SFX 재생
        if (jumpPressed && (grounded || jumpCount < maxJumps - 1))
        {
            jumpCount++;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // 수직속도 리셋 후
            rb.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);

            // 1단/2단 구분 재생
            PlayOneShotSafe(jumpCount == 1 ? jumpSfx : doubleJumpSfx);
        }

        // 6) 애니메이션 전환
        string target = (grounded && Mathf.Abs(rb.linearVelocity.x) > 0.05f) ? moveClip?.name : idleClip?.name;
        if (!string.IsNullOrEmpty(target) && (anim.clip == null || anim.clip.name != target))
            anim.CrossFade(target, 0.08f);

        // 7) 발소리 타이머(지면 + 이동 중일 때만)
        if (grounded && Mathf.Abs(rb.linearVelocity.x) > 0.05f && moveSfx)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= footstepInterval)
            {
                PlayOneShotSafe(moveSfx);
                stepTimer = 0f;
            }
        }
        else stepTimer = 0f;
    }

    // 널이면 재생하지 않는 안전 함수
    void PlayOneShotSafe(AudioClip clip)
    {
        if (clip) au.PlayOneShot(clip);
    }

    // Scene에서 판정 원 보이기
    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
