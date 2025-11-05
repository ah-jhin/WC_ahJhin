using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 체력 스크
/// - IDamageable 구현으로 보스/몹/함정 등에서 통일 호출
/// - 무적 시간, 죽음 처리 포함
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
	Rigidbody2D rb; // 넉백에 필요
	[Header("체력")]
	public int maxHP = 100;          // 최대 체력
	public int currentHP;            // 현재 체력
	public UIHUD hud;				 // 인스펙터 연결

	[Header("피격 무적")]
    // 피해 충돌 시 플레이어 무적시간을 무시할지 여부(인스펙터에서 설정)
    public float invincibleTime = 0.6f; // 피격 후 무적 시간
	private float lastHitTime = -999f;  // 마지막 피격 시각
    [Tooltip("깜빡임용 렌더러")]
	public SpriteRenderer sr;           // 깜빡이용 

    [Header("사망")]           	
    public GameObject bloodEffectPrefab;   // 피격 시 생성할 피 효과 프리팹
    public AudioClip gameOverMusic;        // 게임오버 배경음악 클립
    public GameObject gameOverUI;          // 게임오버 화면 UI 오브젝트
    public GameObject bossDialogueObject;  // 보스 대사 출력용 UI 오브젝트
    public AudioSource bgmAudioSource;     // 배경음악 AudioSource (음악 교체용)
    [SerializeField] private bool isDead = false; // ← 사망 여부 저장. 기본값 false

    public bool IsDead => isDead;                 // ← 외부 읽기 전용 접근자
    [Header("효과음(SFX)")]
    public AudioSource sfx;
    public AudioClip hurtSFX;

	[Header("Pain 데미지 설정")]           // 'pain' 피해 값 설정
    public int painMin = 5;               // 'pain' 최소 피해량 
    public int painMax = 10;              // 'pain' 최대 피해량 
    public float painKnockback = 10f;     // 'pain' 넉백(force) 값
	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
														// 시작 체력 초기화
		currentHP = maxHP;
		hud?.SetHP(currentHP, maxHP);   // UI 갱신

		// sr 자동 할당(없으면 자식에서 탐색)
		if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
	}

    public void TakeDamage(int amount, bool weak, float weakMultiplier)
	{
		// 1) 무적/사망 상태면 무시
		if (Time.time - lastHitTime < invincibleTime || isDead) return;
		lastHitTime = Time.time;

		// 2) 최종 데미지 계산(약점 적용이 필요 없으면 아래 한 줄로 대체: int final = amount;)
		int final = amount + (weak ? Mathf.RoundToInt(weakMultiplier) : 0);

		// 3) 체력 감소
		currentHP = Mathf.Max(0, currentHP - final);

		if (sfx && hurtSFX) sfx.PlayOneShot(hurtSFX);

		// 6) 사망 처리
		if (currentHP <= 0)
		{
			isDead = true;
			OnDead();
		}
		hud?.ShowDamage(transform.position, final, Color.red);	// 피격 피해 표시
		hud?.SetHP(currentHP, maxHP);	// UI 갱신
	}

	/// <summary>레거시 호환. 과거 코드가 TakeDamage(int)만 호출해도 동작.</summary>
	public void TakeDamage(int amount) { TakeDamage(amount, false, 1f); }

	/// <summary>체력 회복</summary>
	public void Heal(int amount)
	{
		if (isDead) return;
		currentHP = Mathf.Min(maxHP, currentHP + Mathf.Max(0, amount));
		// UIHUD 갱신 필요 시 호출
	}

	/// <summary>깜빡임 연출(선택)</summary>
	System.Collections.IEnumerator Blink()
	{
		float end = Time.time + 0.25f;
		while (Time.time < end && !isDead)
		{
			if (sr) sr.enabled = !sr.enabled;
			yield return new WaitForSeconds(0.05f);
		}
		if (sr) sr.enabled = true;
	}
    // 2) 외부 호출용 Pain 처리 함수
    /// <summary>
    /// 가시/함정/보스 패턴 등 'pain' 태그 피격 처리
    /// min~max: 난수 피해 범위, knockback: 밀리는 힘, srcPos: 공격원점(지금은 미사용)
    /// </summary>
    // ▼ 무적시간을 '무시'하고 즉시 Pain 데미지를 적용하는 오버로드
    public void ApplyPain(int min, int max, float knockback, Vector3 srcPos, bool ignoreIFrames)
    {
        // 사망 상태면 무시
        if (isDead) return;

        // 무적 무시가 아니라면 기존 무적 시간 체크
        if (!ignoreIFrames)
        {
            // lastHitTime, invincibleTime은 기존 필드 사용
            if (Time.time - lastHitTime < invincibleTime) return;
        }

        // 타격 타임스탬프 갱신
        lastHitTime = Time.time;

        // 피해량 계산(최소~최대 범위)
        int dmg = Random.Range(Mathf.Min(min, max), Mathf.Max(min, max) + 1);
        currentHP = Mathf.Max(0, currentHP - dmg);
        if (GameScore.I) GameScore.I.OnPlayerDamaged(dmg);

        // 피격 사운드
        if (sfx && hurtSFX) sfx.PlayOneShot(hurtSFX);

        // 넉백: 좌/우 랜덤 방향 + 약간의 상향 힘
        float dir = Random.value < 0.5f ? -1f : 1f;
        if (rb) rb.AddForce(new Vector2(dir * knockback, knockback * 0.5f), ForceMode2D.Impulse);

        // HUD 반영
        hud?.ShowDamage(transform.position, dmg, Color.red);
        hud?.SetHP(currentHP, maxHP);

        // 사망 처리
        if (currentHP <= 0 && !isDead)
        {
            isDead = true;
            OnDead();
        }
    }

    /// <summary>사망 로직</summary>
    void OnDead()
    {
        Debug.Log("[PlayerHealth] Player Dead");
        
        // 이동 및 공격 스크립트 비활성화
        var move = GetComponent<PlayerMovement>(); 
        if (move) move.enabled = false;
        var pistol = GetComponent<WP_Pistol>(); 
        if (pistol) pistol.enabled = false;
        // 물리력 정지
        var rb = GetComponent<Rigidbody2D>();
        if (rb) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
        
        // if (GameOverManager.Instance) GameOverManager.Instance.GameOver();
        // Invoke(nameof(RestartScene), restartDelay);
        // ↑ 제거: 자동 재시작을 중단하고 아래 직접 연출을 수행
        
        // 1. 피격 효과 생성 (피가 터지는 이펙트)
        if (bloodEffectPrefab)
            Instantiate(bloodEffectPrefab, transform.position, Quaternion.identity);
        // 2. 플레이어 캐릭터 제거 (스프라이트 비활성화로 화면에서 숨김)
        if (sr) sr.enabled = false;
        // 3. 배경음 정지 및 게임오버 음악 재생
        if (gameOverMusic)
        {
            Vector3 p = Camera.main ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(gameOverMusic, p, 1f); // 2D 재생
        }
        if (bgmAudioSource && gameOverMusic)
        {
            bgmAudioSource.loop = false;
            bgmAudioSource.clip = gameOverMusic;
            bgmAudioSource.Play();
        }
        // 4. 게임오버 UI 표시
        if (gameOverUI) 
            gameOverUI.SetActive(true);
        // 5. 보스 대사 
        if (bossDialogueObject)
            bossDialogueObject.SetActive(true);
            
        Invoke(nameof(HideBossDialogue), 5f);
		Invoke(nameof(RemovePlayerObject), 5.1f);
		GameScore.I?.OnPlayerDeath();  // 여기서 점수 서버 전송
    }
    
    private void HideBossDialogue()
    {
        // 보스 대사 숨기기
        if (bossDialogueObject) 
            bossDialogueObject.SetActive(false);
    }
    private void RemovePlayerObject()
    {
        // 사망 연출 완료 후 플레이어 오브젝트 제거
        Destroy(gameObject);
    }


	void RestartScene()
	{
		Scene current = SceneManager.GetActiveScene();
		SceneManager.LoadScene(current.buildIndex);
	}
}
