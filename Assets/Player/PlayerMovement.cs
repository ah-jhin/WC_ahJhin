﻿using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// PlayerMovement 스크(전체 교체본)
/// - 좌우 이동 + 점프(버퍼/가변 높이)
/// - 회피(Dash): 위/아래/좌/우 지원, 떠버림 최소화
/// - 회피 트레일, 쿨타임 바, 회피 사운드 훅
/// - FirePoint 좌우 반전 유지
/// - 외부용 프로퍼티: IsGrounded, GetAimDir()
/// </summary>
public class PlayerMovement : MonoBehaviour
{
	[Header("이동 및 점프")]
	public float moveSpeed = 5f;         // 좌우 이동 속도
	public float jumpForce = 10f;        // 기본 점프 힘
	public float highJumpForce = 16f;    // 강화 점프 힘(오브 획득 시)
	public float jumpTime = 0.3f;        // 점프 키 유지 시 추가 가속 시간
	public float jumpBufferTime = 0.15f; // 점프 입력 버퍼
	public float fallMultiplier = 0.5f;  // 물속 낙하 감속 계수

	[Header("Dash(회피) 설정")]
	public KeyCode dashKey = KeyCode.C;  // 회피 키
	public float dashCooldown = 1.0f;    // 회피 쿨타임(초)
	public float dashDuration = 0.12f;   // 회피 잠금 시간(이 동안 이동 입력 무시)
	public float dashPowerH = 14f;       // 좌/우 대시 힘
	public float dashPowerV = 4f;        // 위 대시 힘(떠버림 방지 약간 낮게)
	public float dashPowerDown = 4f;     // 아래 대시 힘(공중일 때)
	public float dashPowerDiagonal = 0f; // 대각선 전용 강도(0이면 H/V 조합 사용)  ← [추가]

	[Header("Dash 시각화")]
	public TrailRenderer dashTrail;      // 회피 시 트레일(선택)
	public Transform cooldownBarRoot;    // 발밑 쿨타임 바 부모(보이지 않으면 비워둬도 됨)
	public SpriteRenderer cooldownBarFill; // 파란 바(Scale.x 0~1로 채움)

	[Header("사운드(SFX)")]
	public AudioSource sfx;              // 같은 오브젝트의 AudioSource(선택)
	public AudioClip sfxDash;            // 회피 효과음(선택)

	[Header("상태")]
	public bool isGrounded = false;      // 바닥 접지 여부(충돌 이벤트로 관리)
	public bool isInWater = false;       // 물 영역 여부

	[Header("총구")]
	public Transform firePoint;          // 총구 위치(좌우 반전용)

	// === 내부 상태 ===
	Rigidbody2D rb;
	SpriteRenderer sr;
	float moveInput;                     // -1,0,1
	bool isJumping = false;              // 점프 가속 중
	bool hasAirJumped = false;           // 이중 점프 사용 여부
	bool hasExtraJump = false;           // 오브로 추가 점프 가능 여부
	bool useHighJump = false;            // 강화 점프 사용 여부
	float jumpBufferCounter;             // 점프 버퍼 타이머
	float jumpTimeCounter;               // 점프 가속 남은 시간

	public bool isDashing = false;              // 회피 중 여부
	float nextDashTime = 0f;             // 회피 가능 시각

    // 외부에서 참고할 수 있도록 읽기 전용 프로퍼티 제공
    public bool IsGrounded => isGrounded;
    // [백샷] 시선(오른쪽 바라봄 여부) 분리
    private bool facingRight = true;
    private int lastHDir = 0; // [백샷] 최근 수평 입력(-1/0/1)

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		//sr = GetComponent<SpriteRenderer>();
		sr = GetComponentInChildren<SpriteRenderer>();   // 자식에 있어도 잡힘

		if (!dashTrail) dashTrail = GetComponentInChildren<TrailRenderer>();
		if (cooldownBarRoot) cooldownBarRoot.gameObject.SetActive(false); // 기본 숨김
        if (dashTrail) { dashTrail.emitting = false; dashTrail.Clear(); }  // 시작 시 숨김
        facingRight = !(sr && sr.flipX);
	}

	void Update()
	{
        // 1) 이동 입력
        // [백샷] 수평 입력 해석: 양쪽 동시 입력 시 멈춤 방지
        bool leftHeld  = Input.GetKey(KeyCode.LeftArrow);
        bool rightHeld = Input.GetKey(KeyCode.RightArrow);

        // 최근에 누른 키 추적
        if (Input.GetKeyDown(KeyCode.LeftArrow))  lastHDir = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow)) lastHDir = +1;

        // 이동 방향 결정
        if (leftHeld && rightHeld)      moveInput = lastHDir; // 최신 키 기준으로 이동 지속
        else if (leftHeld)              moveInput = -1f;
        else if (rightHeld)             moveInput = +1f;
        else                            moveInput = 0f;

        
        // 2) 좌우 Flip(시선) — 백샷 규칙 적용
        // - 이동은 입력대로 한다.
        // - 반대 방향을 누르면 "시선은 유지"하고 뒤로 걸음.
        // - 정확히 한 쪽만 누르고 있을 때에만 시선을 그쪽으로 전환.
        bool leftPressed  = Input.GetKey(KeyCode.LeftArrow);
        bool rightPressed = Input.GetKey(KeyCode.RightArrow);

        // 스프라이트는 시선을 따름
        sr.flipX = !facingRight;

		// 3) 점프 입력 버퍼 등록/소모(X 키)
		if (Input.GetKeyDown(KeyCode.X)) jumpBufferCounter = jumpBufferTime;
		else jumpBufferCounter -= Time.deltaTime;

		// 4) 점프 시작 조건(버퍼가 남아있고, 지상/추가점프 가능 등)
		if (jumpBufferCounter > 0)
		{
			if (isGrounded || !hasAirJumped || hasExtraJump || isInWater)
			{
				float force = useHighJump ? highJumpForce : jumpForce;
				rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
				isJumping = true;
				jumpTimeCounter = jumpTime;
				jumpBufferCounter = 0;

				if (!isGrounded && !isInWater) // 공중에서 시작한 점프면 이중 점프 소모
				{
					if (hasExtraJump) { hasExtraJump = false; useHighJump = false; }
					hasAirJumped = true;
				}
			}
		}

		// 5) 점프 가변 높이: X 유지 동안 위로 추가 가속
		if (Input.GetKey(KeyCode.X) && isJumping && jumpTimeCounter > 0)
		{
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, useHighJump ? highJumpForce : jumpForce);
			jumpTimeCounter -= Time.deltaTime;
		}
		if (Input.GetKeyUp(KeyCode.X)) isJumping = false;

		// 6) 회피 입력 처리(C 키)
		HandleDashInput();

		// 7) FirePoint 좌우 위치 오프셋 유지
		if (firePoint != null)
		{
			float offsetX = 0.3f; // 몸 기준 오른쪽(+)/왼쪽(-) 오프셋
			firePoint.localPosition = new Vector3(sr.flipX ? -offsetX : +offsetX, firePoint.localPosition.y, 0f);
		}

		// 8) 회피 쿨타임 바 갱신
		UpdateDashCooldownUI();

		// [백샷] 최종 시선 강제 적용(Up/Down과 무관, 다른 Flip 로직 덮어씀)
		// 한쪽만 누르면 그쪽을 바라봄, 양쪽/무입력이면 기존 시선 유지
		if (rightPressed && !leftPressed)  facingRight = true;
		else if (leftPressed && !rightPressed) facingRight = false;

		// 스프라이트는 시선을 따름
		sr.flipX = !facingRight;
	}

	void FixedUpdate()
	{
		// 회피 중에는 이동 입력을 잠깐 무시(짧고 단단한 대시 느낌)
		if (!isDashing)
		{
			rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
		}

		// 물 속 낙하 감속
		if (isInWater && rb.linearVelocity.y < 0)
		{
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * fallMultiplier);
		}
	}

	// -----------------------
	// 대시 입력 및 실행
	// -----------------------
	// -----------------------
	void HandleDashInput()
	{
		// 입력/쿨타임/상태 가드
		if (!Input.GetKeyDown(dashKey) || Time.time < nextDashTime || isDashing) return;
		// 대각선 입력을 동시 반영
		int h = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
		int v = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);

		// 무입력일 때만 보정: 이동 중인 방향 또는 시선
		if (h == 0 && v == 0)
		{
			if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
				h = (rb.linearVelocity.x > 0) ? 1 : -1;
			else
				h = (sr.flipX ? -1 : 1);
		}

		// 방향별 파워 조합
		float vx = (h == 0) ? 0f : Mathf.Sign(h) * dashPowerH;
		float vy = 0f;
		if (v > 0) vy = dashPowerV;
		else if (v < 0) vy = -dashPowerDown;

		// 대각선 전용 강도 우선
		if (h != 0 && v != 0 && dashPowerDiagonal > 0f)
		{
			Vector2 ndir = new Vector2(h, v).normalized;
			StartCoroutine(DashRoutine(ndir * dashPowerDiagonal));
		}
		else
		{
			StartCoroutine(DashRoutine(new Vector2(vx, vy)));
		}


		// 완성된 속도 벡터를 받아 즉시 속도로 적용
		IEnumerator DashRoutine(Vector2 dashVelocity)
		{
			isDashing = true;
			nextDashTime = Time.time + dashCooldown;

			// 회피 사운드(선택)
			if (sfx && sfxDash) sfx.PlayOneShot(sfxDash, AudioBus.SFX);

			// 트레일 ON
			if (dashTrail) dashTrail.emitting = true;

			// 기존 속도를 조금 줄인 다음 지정 속도로 고정
			rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y * 0.2f);
			rb.linearVelocity = dashVelocity;

			// 잠금 시간
			float end = Time.time + dashDuration;
			while (Time.time < end) yield return null;

			// 트레일 OFF
			if (dashTrail) dashTrail.emitting = false;

			isDashing = false;
		}
	}

	// ▼ 추가: 방향별 파워를 적용해 최종 대시 속도를 만든다.
	private Vector2 CalcDashVelocity(int h, int v, bool flipped)
	{
		// 무입력이면 시선 기준 수평 대시
		if (h == 0 && v == 0)
			return new Vector2(flipped ? -dashPowerH : dashPowerH, 0f);

		// 대각선
		if (h != 0 && v != 0)
		{
			if (dashPowerDiagonal > 0f)
			{
				// 대각선 전용 강도 사용(정규화 후 곱)
				Vector2 dir = new Vector2(h, v).normalized;
				return dir * dashPowerDiagonal;
			}
			// 전용 강도 없으면 각 축 강도 독립 적용
			float vx = Mathf.Sign(h) * dashPowerH;
			float vy = Mathf.Sign(v) * (v > 0 ? dashPowerV : dashPowerDown);
			return new Vector2(vx, vy);
		}

		// 순수 좌우
		if (h != 0)
			return new Vector2(Mathf.Sign(h) * dashPowerH, 0f);

		// 순수 위/아래
		if (v > 0)
			return new Vector2(0f, dashPowerV);

		// v < 0
		return new Vector2(0f, -dashPowerDown);
	}

	void UpdateDashCooldownUI()
	{
		if (!cooldownBarRoot || !cooldownBarFill) return;

		float remain = Mathf.Clamp01((nextDashTime - Time.time) / dashCooldown);
		if (remain <= 0f)
		{
			cooldownBarRoot.gameObject.SetActive(false);
			return;
		}

		cooldownBarRoot.gameObject.SetActive(true);
		// 바 채우기: 0→1
		var s = cooldownBarFill.transform.localScale;
		s.x = remain;
		cooldownBarFill.transform.localScale = s;
	}

	// -----------------------
	// 충돌/트리거로 상태 업데이트
	// -----------------------
	void OnCollisionEnter2D(Collision2D collision)
	{
		// contactCount 로 개수 확인 후 GetContact(0) 사용
		if (collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.7f)
		{
			isGrounded = true;
			hasAirJumped = false;
			isJumping = false;
		}
	}

	void OnCollisionExit2D(Collision2D collision)
	{
		// 바닥에서 떨어짐
		isGrounded = false;
	}

	void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Water")) isInWater = true;

		if (other.CompareTag("BlueJumpOrb")) // 추가 점프(기본)
		{
			hasExtraJump = true;
			useHighJump = false;
			other.gameObject.SetActive(false);
			Invoke(nameof(ReactivateOrb), 3f);
		}

		if (other.CompareTag("RedJumpOrb")) // 추가 점프(강화)
		{
			hasExtraJump = true;
			useHighJump = true;
			other.gameObject.SetActive(false);
			Invoke(nameof(ReactivateOrb), 3f);
		}
	}

	void OnTriggerExit2D(Collider2D other)
	{
		if (other.CompareTag("Water")) isInWater = false;
	}

	void ReactivateOrb()
	{
		// TODO: OrbManager로 교체 가능. 여기서는 비워둠.
	}

	// -----------------------
	// 외부에서 쓰기 쉬운 조준 방향 도우미
	// - 위 입력: 위 발사
	// - 공중+아래 입력: 아래 발사
	// - 그 외: 시선 기준 좌/우
	// -----------------------
	public Vector2 GetAimDir()
	{
		float v = Input.GetAxisRaw("Vertical");
		if (v > 0.1f) return Vector2.up;
		if (!isGrounded && v < -0.1f) return Vector2.down;
		return facingRight ? Vector2.right : Vector2.left;
	}
}