using UnityEngine;
using System.Collections;

/// <summary>
/// 스테이지1 보스(패턴 전용)
/// - 체력 우선순위: 아래 overrideMaxHP > BossBase.maxHP
///   필요 없으면 0으로 두면 BossBase 값 사용.
/// </summary>
public class BossStage1 : BossBase
{
	[Header("HP Override(선택)")]
	public int overrideMaxHP = 0; // 0이면 무시, 0보다 크면 이 값으로 덮어씀

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
	public float skillInterval = 6f;
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

	private bool _inSkySkill = false;
	private Vector2 _groundPos;
	private Coroutine _skillLoopCo;

	protected override void Start()
	{
		// 1) 체력 우선권 결정: 이 스테이지 전용 값이 있으면 덮어쓰고 시작
		if (overrideMaxHP > 0) maxHP = overrideMaxHP;

		// 2) 공통 초기화(HP/UI)
		base.Start();

		// 3) 플레이어 참조 캐시
		var pObj = GameObject.FindGameObjectWithTag("Player");
		if (pObj)
		{
			_player = pObj.transform;
			_playerRb = pObj.GetComponent<Rigidbody2D>();
		}

		// 4) 점프 곡선 기본값(없을 때만 생성)
		if (popCurve == null)
			popCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.4f, 1), new Keyframe(1, 0));

		// 5) 스킬 루프 시작
		_skillLoopCo = StartCoroutine(SkillLoop());
	}

	void Update()
	{
		if (_inSkySkill) return;
		if (!_player || !firePoint || !normalBulletPrefab) return;

		Vector2 center = firePoint.position;
		float dist = Vector2.Distance(center, _player.position);
		if (dist > attackRange + rangeMargin) return;
		if (Time.time < _nextAttackTime) return;

		_nextAttackTime = Time.time + attackCooldown;
		ShootNormal();
	}

	/// <summary>지상 일반 사격</summary>
	private void ShootNormal()
	{
		Vector2 origin = firePoint.position;
		Vector2 target = _player ? (Vector2)_player.position : origin;
		Vector2 dir = (target - origin).normalized;

		// 리드샷(선행 조준) 옵션
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

	/// <summary>스카이 스킬 루프</summary>
	private IEnumerator SkillLoop()
	{
		while (true)
		{
			yield return new WaitForSeconds(skillInterval);
			if (skyPoints == null || skyPoints.Length == 0) continue;
			yield return StartCoroutine(DoSkySkill());
		}
	}

	/// <summary>스카이 스킬 연출+탄막</summary>
	private IEnumerator DoSkySkill()
	{
		_inSkySkill = true;
		_groundPos = transform.position;

		Transform t = skyPoints[Random.Range(0, skyPoints.Length)];
		Vector2 skyPos = t.position;
		var sr = GetComponentInChildren<SpriteRenderer>();

		if (sr) sr.enabled = false;
		yield return new WaitForSeconds(0.05f);
		transform.position = skyPos;
		if (sr) sr.enabled = true;
		yield return StartCoroutine(PopJump());

		if (skillFlashPrefab && _skillFlash == null)
		{
			_skillFlash = Instantiate(skillFlashPrefab, transform.position, Quaternion.identity, transform);
			_skillFlash.transform.localPosition = Vector3.zero;

			var fx = _skillFlash.GetComponent<FlashFX>();
			if (fx) fx.Play();
		}

		yield return StartCoroutine(SkyShootBurst(skyShotCount));

		if (_skillFlash)
		{
			var fx = _skillFlash.GetComponent<FlashFX>();
			if (fx) yield return StartCoroutine(fx.StopAndFade());
			else Destroy(_skillFlash);
			_skillFlash = null;
		}

		if (returnToGround)
		{
			if (sr) sr.enabled = false;
			yield return new WaitForSeconds(0.05f);
			transform.position = _groundPos;
			if (sr) sr.enabled = true;
		}

		_inSkySkill = false;
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
}
