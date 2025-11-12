// Assets/Stage/Ouranos_Chaser.cs
using UnityEngine;

namespace Ouranos_Boss
{
	/// <summary>
	/// 추적 엔티티
	/// - Player 추적
	/// - 접촉 시 피해 1회 후 즉시 사망
	/// - GROUND 접촉 시 즉시 사망
	/// - 보스 사망 전역 신호로 즉시 사망
	/// - 사망 시 VFX, SFX 재생
	/// - ★ 총알 연동용 TakeDamage(int) 제공
	/// </summary>
	public class Ouranos_Chaser : MonoBehaviour
	{
		[Header("공격 데이터")]
		public int minDamage = 10, maxDamage = 15;   // 플레이어에게 줄 피해
		public float speed = 3f;                     // 추적 속도

		[Header("생명")]
		public int hp = 30;                          // ★ 총알이 깎을 체력

		[Header("사망 연출")]
		public AudioClip deathSfx;                   // 사망 SFX
		public GameObject deathVfx;                  // 사망 VFX

		[Header("환경 충돌")]
		[SerializeField] string blockTag = "block";   // 바닥/블록 태그 이름

		Transform _player;                           // 플레이어 Transform
		bool _dead;                                  // 중복 사망 방지

		void OnEnable()
		{
			Ouranos_GlobalSignals.BossDied += Die;  // 보스 사망 시 즉시 사망
		}

		void OnDisable()
		{
			Ouranos_GlobalSignals.BossDied -= Die;
		}

		void Start()
		{
#if UNITY_2023_1_OR_NEWER
			var ph = FindFirstObjectByType<PlayerHealth>();
#else
            var ph = FindObjectOfType<PlayerHealth>();
#endif
			_player = ph ? ph.transform : null;
		}

		void Update()
		{
			if (_dead || !_player) return;
			// 플레이어를 향해 관통 추적
			transform.position = Vector3.MoveTowards(transform.position, _player.position, speed * Time.deltaTime);
		}

		void OnTriggerEnter2D(Collider2D other)
		{
			if (_dead) return;

			// 플레이어 접촉 시 피해 1회 후 사망
			if (_player && other.transform == _player)
			{
				var d = other.GetComponent<IDamageable>(); // 프로젝트의 플레이어가 이 인터페이스 구현
				if (d != null)
				{
					int dmg = Random.Range(minDamage, maxDamage + 1);
					d.TakeDamage(dmg, false, 0);
				}
				Die();
				return;
			}

			// 블럭 접촉 시 즉시 사망
			if (other.CompareTag(blockTag))
				Die();
		}

		/// <summary>
		/// ★ 총알에 의해 호출되는 공개 메서드
		/// - 체력을 amt만큼 감소
		/// - 0 이하가 되면 Die()
		/// </summary>
		public void TakeDamage(int amt)
		{
			if (_dead) return;
			hp = Mathf.Max(0, hp - Mathf.Max(0, amt));
			if (hp == 0) Die();
		}

		/// <summary>즉시 사망 처리 + VFX/SFX</summary>
		public void Die()
		{
			if (_dead) return;
			_dead = true;

			if (deathVfx) Instantiate(deathVfx, transform.position, Quaternion.identity);
			if (deathSfx) AudioSource.PlayClipAtPoint(deathSfx, transform.position, 1f);

			Destroy(gameObject);
		}
	}
}
