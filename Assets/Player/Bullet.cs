using UnityEngine;

/// <summary>
/// 발사체 전용 스크: 이동/수명/충돌만 담당한다.
/// - 피해 수치는 반드시 발사 시점에 Inject()로 주입한다.
/// - 주입이 없으면 테스트용 기본값을 1회 경고와 함께 사용한다.
/// - 약점 판정은 태그 "WeakPoint"로 처리한다.
/// </summary>
public class Bullet : MonoBehaviour
{
	[Header("수명/충돌")]
	public float lifetime = 3f;           // 자동 파괴 시간
	public string blockTag = "block";     // 벽 태그

	[Header("테스트 기본값(주입 누락 대비)")]
	public int defaultDamage = 1;         // 주입 없을 때만 사용
	public float defaultWeakMultiplier = 1f;

	// 런타임 주입될 값
	private int _damage;
	private float _weakMul = 1f;
	private bool _hasInjected;            // 주입 여부
	private static bool _warnedOnce;      // 콘솔 경고 1회만
	public bool debugLog = false;		  // 디버그: 충돌 확인

	void OnEnable()
	{
		// 수명 타이머 시작
		if (lifetime > 0f) Destroy(gameObject, lifetime);

		// 디버그
		if (debugLog) Debug.Log($"[Bullet] Spawn @ {transform.position} layer={gameObject.layer}");
	}

	/// <summary>
	/// 발사 직후 호출: 현재 무기의 피해/약점배율을 주입한다.
	/// </summary>
	public void Inject(int damage, float weakMultiplier)

	{
		_damage = Mathf.Max(0, damage);
		_weakMul = Mathf.Max(0f, weakMultiplier);
		_hasInjected = true;
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		// 약점 여부
		bool isWeak = other.CompareTag("WeakPoint");

		// 디버그
		if (debugLog) Debug.Log($"[Bullet] Hit {other.name} tag={other.tag} layer={other.gameObject.layer}");

		// 대상 찾기(IDamageable)
		var target = other.GetComponentInParent<IDamageable>();
		if (target != null)
		{
			// 주입 누락 시 기본값 사용 + 1회 경고
			if (!_hasInjected)
			{
				if (!_warnedOnce)
				{
					Debug.LogWarning("[Bullet] 스탯 주입(Inject)이 없어 defaultDamage를 사용합니다. 발사 코드에서 Inject()를 호출하세요.");
					_warnedOnce = true;
				}
				_damage = defaultDamage;
				_weakMul = defaultWeakMultiplier;
			}

			// 피해 전달
			target.TakeDamage(_damage, isWeak, _weakMul);

			// 한 번 맞으면 제거
			Destroy(gameObject);
			return;
		}

		// 벽 등과 충돌 시 제거
		if (other.CompareTag(blockTag))
		{
			if (debugLog) Debug.Log("[Bullet] Destroy: damage or block");
			Destroy(gameObject);
		}
	}
}
