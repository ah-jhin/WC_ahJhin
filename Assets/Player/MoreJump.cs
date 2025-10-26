using UnityEngine;

public class MoreJump : MonoBehaviour
{
    [Header("강도")]
    [SerializeField] private float bounceStrength = 15f;    // 플레이어를 튕겨올리는 힘의 크기

    [Header("설정")]
    [SerializeField] private bool disappearOnHit = false;   // 충돌 시 오브젝트 사라지는지 여부
    [SerializeField] private float respawnDelay = 5f;       // 재생성 딜레이 (초, disappearOnHit이 true일 때만 사용)

    [Header("효과")]
    [SerializeField] private GameObject bounceEffectPrefab; // 튕길 때 생성할 VFX 프리팹
    [SerializeField] private AudioClip bounceSound;         // 튕길 때 재생할 SFX 클립

    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private AudioSource audioSource;

    void Awake()
    {
        // 동일 오브젝트의 SpriteRenderer와 Collider2D 컴포넌트 참조 저장
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        // 오디오소스 세팅: 기존 AudioSource가 없으면 추가
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && bounceSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어와 충돌했을 때만 작동
        if (other.CompareTag("Player"))
        {
            // 충돌한 객체가 플레이어라면 해당 Rigidbody2D를 위로 튕겨냄
            Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // 현재 x속도 유지한 채, bounceStrength 만큼 위쪽 방향 속도를 부여
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, bounceStrength);
            }

            // 이펙트 재생: 프리팹이 지정되어 있으면 현재 위치에 생성
            if (bounceEffectPrefab != null)
            {
                Instantiate(bounceEffectPrefab, transform.position, Quaternion.identity);
            }

            // 사운드 재생: 오디오 클립이 지정되어 있으면 1회 재생
            if (bounceSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(bounceSound);
            }

            // 오브젝트 사라짐 처리
            if (disappearOnHit)
            {
                // 스프라이트와 콜라이더를 비활성화하여 사라진 것처럼 만든다
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (col != null) col.enabled = false;
                // respawnDelay 후에 Respawn 함수 호출하여 재활성화
                if (respawnDelay >= 0f)
                {
                    Invoke(nameof(Respawn), respawnDelay);
                }
            }
        }
    }

    void Respawn()
    {
        // 오브젝트를 다시 보이게 하고 충돌도 활성화
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (col != null) col.enabled = true;
    }
}
