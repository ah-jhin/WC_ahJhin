using UnityEngine;

public class WP_Pistol : MonoBehaviour
{
	// === 인스펙터 연결 ===
	public GameObject bulletPrefab;       // 총알 프리팹
	public Transform firePoint;              // 총구 트랜스폼
	public AudioSource sfx;               // 사운드 소스(선택)
	public AudioClip sfxShoot;            // 발사 효과음(선택)
	public PlayerMovement pm;             // PlayerMovement 참조(선택)
	public float spawnOffset = 0.6f;	// 0.25 → 0.6

	// === 무기 스탯(테스트 값) ===
	public int weaponDamage = 3;         // 피해값
	public float weaponWeakMul = 1.5f;    // 약점 배율
	public float bulletSpeed = 22f;       // 총알 속도

	void Awake()
	{
		// 자동 참조 보정
		if (pm == null) pm = GetComponentInParent<PlayerMovement>();
		if (sfx == null) sfx = GetComponent<AudioSource>();
	}

	void Shoot()
	{
		// 디버그용 FirePoint를 찾기
		if (!ResolveFirePoint())
		{
			Debug.LogError("[WP_Pistol] FirePoint를 찾지 못했습니다. Player 자식 이름을 'FirePoint'로 두세요.");
			return;
		}

		// 1) 발사 방향 결정: PlayerMovement가 있으면 그 함수 사용, 없으면 좌/우
		Vector2 dir = pm ? pm.GetAimDir() : (transform.localScale.x >= 0 ? Vector2.right : Vector2.left);

		// 2) 총알 생성
		GameObject go = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

		// 3) 총알 컴포넌트 세팅(rb 선언 후 사용)
		var rb = go.GetComponent<Rigidbody2D>();
		if (rb) rb.linearVelocity = dir * bulletSpeed;

		// 3) 스탯 주입(오류 해결 지점)
		var b = go.GetComponent<Bullet>();
		if (b) b.Inject(weaponDamage, weaponWeakMul);  // ← Bullet.cs에 만든 Inject(int,float) 호출


		// 4) 초기 자기충돌 무시(플레이어 콜라이더와 잠시 무시)
		var bulletCol = go.GetComponent<Collider2D>();
		if (bulletCol)
		{
			foreach (var pc in GetComponentsInChildren<Collider2D>())
				Physics2D.IgnoreCollision(bulletCol, pc, true);
		}

		// 5) 사운드(선택)
		if (sfx && sfxShoot) sfx.PlayOneShot(sfxShoot, AudioBus.SFX);

		// 6) 디버그
		Debug.Log($"[Pistol] 발사됨 / Damage={weaponDamage} WeakMultiplier={weaponWeakMul}");
	}
	// ★ 추가: FirePoint 자동 탐색 + 캐시
	Transform ResolveFirePoint()
	{
		if (firePoint) return firePoint;

		// 1) 부모들 아래에서 이름으로 탐색
		Transform p = transform;
		while (p != null)
		{
			var t = p.Find("FirePoint");   // Player 자식 "FirePoint" 옵젝
			if (t) { firePoint = t; return firePoint; }
			p = p.parent;
		}

		// 2) 태그로 최후 탐색(원하면 FirePoint에 태그 "FirePoint" 지정)
		var tagged = GameObject.FindWithTag("FirePoint");
		if (tagged) { firePoint = tagged.transform; return firePoint; }

		return null;
	}

}
