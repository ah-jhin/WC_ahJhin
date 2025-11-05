using UnityEngine;

public class MoreJump : MonoBehaviour
{
    [Header("강도")]
    [Tooltip("플레이어가 오브 접촉 시 도달해야 하는 '목표 변위(미터)'. 양수=상승, 음수=하강")]
    [SerializeField] private float bounceStrength = 15f;    // 목표 상승/하강 거리(미터)

    [Header("설정")]
    [SerializeField] private bool disappearOnHit = false;   // 충돌 시 오브젝트 사라짐 여부
    [SerializeField] private float respawnDelay = 5f;       // 재생성 딜레이(초)

    [Header("효과")]
    [SerializeField] private GameObject bounceEffectPrefab; // VFX 프리팹
    [SerializeField] private AudioClip bounceSound;         // SFX 클립

    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private AudioSource audioSource;

    void Awake()
    {
        // 같은 오브젝트 내 컴포넌트 참조
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        // 오디오소스 준비(없으면 추가)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && bounceSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Player 태그만 반응
        if (!other.CompareTag("Player")) return;

        Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            // === 정확히 'bounceStrength' 미터 만큼 위/아래로 이동하도록 초기속도를 역산하여 부여 ===
            float g = Mathf.Abs(Physics2D.gravity.y * playerRb.gravityScale); // 유효 중력가속도(>0)
            float targetDeltaY = bounceStrength;                               // 목표 변위(미터)

            // 기존 수직 관성 제거(중력/관성 영향 배제 목적)
            Vector2 v = playerRb.linearVelocity;
            v.y = 0f;
            playerRb.linearVelocity = v;

            if (g < 1e-4f)
            {
                // 중력이 사실상 0이면 등가속도 공식이 성립하지 않으므로 위치로 직접 보정
                playerRb.position += new Vector2(0f, targetDeltaY);
            }
            else
            {
                // v0 = sqrt(2 * g * |Δy|): 이 초기속도를 주면 정확히 Δy만큼 상승/하강 후 정지점 도달
                float v0 = Mathf.Sqrt(2f * g * Mathf.Abs(targetDeltaY));
                float newVy = (targetDeltaY >= 0f) ? v0 : -v0;

                Vector2 outV = playerRb.linearVelocity;
                outV.y = newVy;
                playerRb.linearVelocity = outV;
            }
            // === 여기까지 ===
        }

        // VFX
        if (bounceEffectPrefab != null)
        {
            Instantiate(bounceEffectPrefab, transform.position, Quaternion.identity);
        }

        // SFX
        if (bounceSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(bounceSound);
        }

        // 일시적 비활성화 처리
        if (disappearOnHit)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (col != null) col.enabled = false;

            if (respawnDelay >= 0f)
            {
                Invoke(nameof(Respawn), respawnDelay);
            }
        }
    }

    void Respawn()
    {
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (col != null) col.enabled = true;
    }
}
