using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("좌표")]
    [SerializeField] private float targetX = 0f;    // 텔레포트할 목표 X 좌표
    [SerializeField] private float targetY = 0f;    // 텔레포트할 목표 Y 좌표

    [Header("설정")]
    [SerializeField] private bool disappearOnUse = false;  // 사용 시 포탈 오브젝트 사라지는지 여부
    [SerializeField] private float respawnDelay = 5f;      // 포탈 재활성화 딜레이 (초)

    [Header("효과")]
    [SerializeField] private GameObject teleportEffectPrefab; // 텔레포트 순간에 표시할 VFX 프리팹
    [SerializeField] private AudioClip teleportSound;         // 텔레포트 순간에 재생할 사운드

    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private AudioSource audioSource;

    void Awake()
    {
        // SpriteRenderer, Collider2D, AudioSource 초기 참조 설정
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && teleportSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어가 포탈에 닿았을 때
        if (other.CompareTag("Player"))
        {
            // 지정된 좌표로 플레이어 위치 이동 (순간이동)
            Vector3 targetPosition = new Vector3(targetX, targetY, other.transform.position.z);
            other.transform.position = targetPosition;

            // 이펙트 재생: 프리팹이 지정된 경우 새 위치에 생성
            if (teleportEffectPrefab != null)
            {
                Instantiate(teleportEffectPrefab, other.transform.position, Quaternion.identity);
            }

            // 사운드 재생: 클립이 지정된 경우 한 번 재생
            if (teleportSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(teleportSound);
            }

            // 포탈 오브젝트 사라지게 하기 (일회용 포탈 처리)
            if (disappearOnUse)
            {
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (col != null) col.enabled = false;
                // respawnDelay 후에 포탈 재활성화
                if (respawnDelay >= 0f)
                {
                    Invoke(nameof(Respawn), respawnDelay);
                }
            }
        }
    }

    void Respawn()
    {
        // 포탈을 다시 보이게 하고 충돌도 가능하게 복원
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (col != null) col.enabled = true;
    }
}
