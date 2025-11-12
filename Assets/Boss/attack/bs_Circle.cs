using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원형(360도)으로 동그란 탄막을 퍼뜨리는 '발사기'.
/// - count, speed, repeat, collision on/off
/// - 각도: 균등 / 일정 간격(step) / 랜덤
/// - 추적: 대상 각도에 맞춰 1발 정조준(명중률 %, 100=완전 정확)
/// - 유틸: 수명(lifeTime), VFX/SFX, 잔상, 크기 변화, 중력
/// </summary>
public class bs_Circle : MonoBehaviour
{
	// ───────────────────────────────── 스탯
	[Header("스탯")]
	[Min(1)] public int circle_count = 10;        // 갯수(기본 10)
	[Min(0f)] public float circle_speed = 5f;     // 속도(기본 5)
	[Min(0)] public int circle_repeat = 0;        // 반복 횟수(0=한 번)
	[Min(0f)] public float circle_repeatInterval = 0.35f; // 반복 간격(초)
	public bool circle_enableCollision = false;   // 충돌 사용 여부(기본 꺼짐)

	// ───────────────────────────────── 각도
	[Header("각도")]
	public bool angle_random = false;             // 랜덤 각도 사용
	public float angle_step = 0f;                 // 0=360/count 균등, >0이면 고정 간격(도수)
	public float angle_startOffset = 0f;          // 전체 회전 오프셋(도수)

	// ───────────────────────────────── 추적
	[Header("추적")]
	public bool track_enabled = false;            // 활성화
	public Transform track_target;                // 미지정시 Player 자동 탐색
	[Range(0f, 100f)] public float track_accuracy = 100f; // 명중률(퍼센트)
	[Tooltip("명중률이 100% 미만일 때 최대 빗나감 각도")]
	public float track_maxMissAngle = 45f;        // 예: 45도

	// ───────────────────────────────── 유틸
	[Header("유틸")]
	[Min(0.1f)] public float bullet_lifeTime = 10f; // 제거 시간(초)
	[Tooltip("필요하면 최대 3개까지 등록")] public GameObject[] vfx_onShoot = new GameObject[0]; // 발사 시 이펙트
	[Tooltip("필요하면 최대 3개까지 등록")] public AudioClip[] sfx_onShoot = new AudioClip[0];   // 발사 시 사운드
	public bool bullet_trail = false;             // 잔상(트레일) 사용
	[Min(0f)] public float trail_time = 0.25f;    // 트레일 지속
	[Min(0f)] public float trail_width = 0.08f;   // 트레일 두께
	public float bullet_scaleDeltaPerSec = 0f;    // 크기 변화(+커짐, -작아짐)
	public float bullet_gravityScale = 0f;        // 2D 중력(0=없음)

	// ───────────────────────────────── 기타
	[Header("발사 제어")]
	public bool autoFireOnStart = true;           // 시작 시 자동 발사
	public bool debugFireOnKey = false;           // 키로 발사(디버깅)
	public KeyCode fireKey = KeyCode.Q;

	[Header("필수 프리팹")]
	public GameObject bulletPrefab;               // 둥근 탄알 프리팹(필수)
	public AudioSource audioSource;               // 재생용(없으면 자동 생성)

	// 내부
	Transform _playerCache;

	void Awake()
	{
		// AudioSource 준비
		if (!audioSource)
		{
			audioSource = gameObject.GetComponent<AudioSource>();
			if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 0f;
		}
	}

	void Start()
	{
		if (autoFireOnStart) StartCoroutine(CoFireLoop());
	}

	void Update()
	{
		if (debugFireOnKey && Input.GetKeyDown(fireKey))
			StartCoroutine(CoFireLoop());
	}

	IEnumerator CoFireLoop()
	{
		int loops = Mathf.Max(0, circle_repeat);
		// 총 (1 + repeat) 번 발사
		for (int i = 0; i <= loops; i++)
		{
			FireOnce();
			if (i < loops) yield return new WaitForSeconds(circle_repeatInterval);
		}
	}

	/// <summary>한 번 발사(360도 or 규칙 각도)</summary>
	public void FireOnce()
	{
		if (!bulletPrefab) { Debug.LogWarning("[bs_Circle] bulletPrefab 없음"); return; }

		// VFX/SFX 실행
		PlayShootSfx();
		SpawnShootVfx();

		// 기준 각도 계산
		float baseAngle = angle_startOffset;
		if (track_enabled)
		{
			var t = GetTarget();
			if (t)
			{
				Vector2 dir = (t.position - transform.position).normalized;
				float aim = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // 우=0 기준
				float miss = Mathf.Lerp(track_maxMissAngle, 0f, track_accuracy / 100f);
				baseAngle = aim + (miss > 0f ? Random.Range(-miss, miss) : 0f);
			}
		}

		// 스텝 결정
		float step = angle_random
			? 0f
			: (angle_step > 0f ? angle_step : 360f / Mathf.Max(1, circle_count));

		// N발 생성
		for (int i = 0; i < circle_count; i++)
		{
			float ang = angle_random ? Random.Range(0f, 360f) : baseAngle + i * step;
			SpawnBullet(ang);
		}
	}

	// 탄알 생성
	void SpawnBullet(float angleDeg)
	{
		GameObject b = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
		var bullet = b.GetComponent<bs_CircleBullet>();
		if (!bullet) bullet = b.AddComponent<bs_CircleBullet>();

		// 방향
		Vector2 dir = AngleToDir(angleDeg).normalized;

		// 설정 주입
		bullet.Init(new bs_CircleBullet.Params
		{
			startDir = dir,
			speed = circle_speed,
			lifeTime = bullet_lifeTime,
			enableCollision = circle_enableCollision,
			useTrail = bullet_trail,
			trailTime = trail_time,
			trailWidth = trail_width,
			scaleDeltaPerSec = bullet_scaleDeltaPerSec,
			gravityScale = bullet_gravityScale
		});
	}

	Transform GetTarget()
	{
		if (track_target) return track_target;
		// Player 자동 탐색(한 번 캐시)
		if (!_playerCache)
		{
			var p = GameObject.FindWithTag("Player");
			if (p) _playerCache = p.transform;
		}
		return _playerCache;
	}

	static Vector2 AngleToDir(float deg)
	{
		float r = deg * Mathf.Deg2Rad;
		return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
	}

	void PlayShootSfx()
	{
		if (sfx_onShoot == null || sfx_onShoot.Length == 0) return;
		var clip = sfx_onShoot[Random.Range(0, sfx_onShoot.Length)];
		if (clip) audioSource.PlayOneShot(clip);
	}

	void SpawnShootVfx()
	{
		if (vfx_onShoot == null) return;
		for (int i = 0; i < vfx_onShoot.Length && i < 3; i++)
		{
			var v = vfx_onShoot[i];
			if (v) Instantiate(v, transform.position, Quaternion.identity);
		}
	}
}
