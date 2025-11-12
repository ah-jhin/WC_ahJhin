// Assets/Stage/Ouranos_Soldier.cs
using System.Collections;
using UnityEngine;

namespace Ouranos_Boss
{
	/// <summary>
	/// 병사 엔티티
	/// - readyDelay 후 일정 간격으로 탄 발사
	/// - GROUND 충돌 시 즉시 사망
	/// - 보스 사망 전역 신호 수신 시 즉시 사망
	/// - 사망 시 VFX, SFX 재생
	/// </summary>
	public class Ouranos_Soldier : MonoBehaviour
	{
		[Header("생명")]
		public int hp = 50;                          // 현재 체력
		public AudioClip deathSfx;                   // 사망 SFX
		public GameObject deathVfx;                  // 사망 VFX

		[Header("발사")]
		public float readyDelay = 5f;                // 초기 대기 시간
		public float fireInterval = 3f;              // 발사 간격
		public float fireRange = 15f;				 // 사거리
		public Ouranos_SniperProjectile bulletPrefab;// 탄 프리팹(Ouranos_)
		public int minDamage = 5, maxDamage = 15;    // 탄 피해
		public float bulletSpeed = 12f;              // 탄 속도
		public bool bulletExplode = false;           // 폭발 여부
		public float explodeRadius = 1.5f;           // 폭발 반경
		public AudioClip fireSfx;                    // 발사 SFX
		[Header("환경 충돌")]
		[SerializeField] string blockTag = "block";   // 바닥/블록 태그 이름
		Transform _player;                           // 플레이어 Transform 캐시
		bool _dead;                                  // 중복 사망 방지

		void OnEnable()
		{
			// 전역: 보스 사망 신호 구독 → 즉시 사망
			Ouranos_GlobalSignals.BossDied += Die;
		}

		void OnDisable()
		{
			Ouranos_GlobalSignals.BossDied -= Die;
		}

		void Start()
		{
			// 플레이어 Transform 찾기
#if UNITY_2023_1_OR_NEWER
			var ph = FindFirstObjectByType<PlayerHealth>();
#else
            var ph = FindObjectOfType<PlayerHealth>();
#endif
			_player = ph ? ph.transform : null;

			// 발사 루프 시작
			StartCoroutine(Main());
		}

		IEnumerator Main()
		{
			// 스폰 직후 준비 시간
			yield return new WaitForSeconds(readyDelay);

			// 체력이 0이 될 때까지 주기 발사
			while (!_dead && hp > 0)
			{
				if (_player) FireOnce();
				yield return new WaitForSeconds(fireInterval);
			}
		}

		/// <summary>탄 1발 발사</summary>
		void FireOnce()
		{
			if (!_player) return;

			// ▼ 15m 거리 체크
			if (Vector2.Distance(_player.position, transform.position) > fireRange)
				return; // 범위 밖이면 사격/사운드 모두 스킵

			// 탄 프리팹 생성 및 데이터 주입
			var b = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
			b.minDamage = minDamage;
			b.maxDamage = maxDamage;
			if (bulletExplode) b.ability |= Ouranos_SniperProjectile.AbilityFlags.Explosive;
			b.explosionRadius = explodeRadius;
			if (fireSfx) b.onShotSfx = fireSfx;

			// 플레이어 방향으로 발사
			Vector2 dir = (_player.position - transform.position).normalized;
			b.speed = bulletSpeed;
			b.Fire(dir);
		}

		/// <summary>외부에서 피해를 입힐 때 호출</summary>
		public void TakeDamage(int amt)
		{
			if (_dead) return;
			hp = Mathf.Max(0, hp - amt);
			if (hp == 0) Die();
		}

		/// <summary>즉시 사망 처리 + VFX/SFX 재생</summary>
		public void Die()
		{
			if (_dead) return;
			_dead = true;

			// 사망 연출
			if (deathVfx) Instantiate(deathVfx, transform.position, Quaternion.identity);
			if (deathSfx) AudioSource.PlayClipAtPoint(deathSfx, transform.position, 1f);

			// 엔티티 제거
			Destroy(gameObject);
		}

		void OnCollisionEnter2D(Collision2D c)
		{
			// 규칙: GROUND 태그 접촉 시 즉시 사망
			if (c.collider.CompareTag(blockTag))
				Die();   // 즉시 사망
		}
	}
}
