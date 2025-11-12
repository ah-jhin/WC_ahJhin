// Assets/Boss/Ouranos_SniperProjectile.cs
// ※ 기존 파일 전체 교체. 주석으로 변경점 표시.
using System.Collections.Generic;
using UnityEngine;

namespace Ouranos_Boss
{
	[RequireComponent(typeof(Collider2D))]
	[RequireComponent(typeof(Rigidbody2D))]
	public class Ouranos_SniperProjectile : MonoBehaviour
	{
		[System.Flags]
		public enum AbilityFlags
		{
			None = 0,
			FlameTrail = 1 << 0, // 이동 중 화염 존 생성
			Explosive = 1 << 1, // 충돌 시 폭발
			Large = 1 << 2, // 대형: 스프라이트/히트박스 스케일 업
			SpeedUp = 1 << 3, // 발사 속도 가산
			InstantExplode = 1 << 4, // 발사 즉시 소폭발
			ElectricTrail = 1 << 5  // 이동 중 전기(둔화) 존 생성
		}

		[Header("기본 데이터")]
		[Tooltip("최소 피해량")]
		public int minDamage = 20;
		[Tooltip("최대 피해량")]
		public int maxDamage = 30;
		[Tooltip("기본 비행 속도")]
		public float speed = 20f;
		[Tooltip("블록에 부딪히면 폭발/소멸할지 여부")]
		public bool collideWithBlocks = false;
		[Tooltip("자연 소멸 시간(초)")]
		public float lifeSeconds = 5f;
		[Tooltip("탄 특성 플래그(복수 선택 가능)")]
		public AbilityFlags ability;

		[Header("SFX/VFX")]
		public AudioClip onShotSfx;
		public AudioClip onHitSfx;
		[Range(0f, 1f)] public float sfxVolume = 1f;
		[Range(0.1f, 3f)] public float sfxPitch = 1f;

		[Tooltip("플레이어를 스칠 때 재생할 SFX 후보(여러 개)")]
		public List<AudioClip> nearMissSfxList = new();
		[Tooltip("근접 스침 SFX를 무작위로 고를지(해제=순차)")]
		public bool nearMissRandom = true;
		private int _nearIndex = 0;
		[Tooltip("근접 스침 반경(미터)")]
		public float nearMissRadius = 5f;
		private bool _nearPlayed = false;

		[Header("환경 충돌")]
		[Tooltip("환경으로 간주할 레이어(체크된 레이어와 충돌 시 처리)")]
		public LayerMask envMask; // ★ 현실 적용: 인스펙터에서 Block/Tile 등 체크

		[Header("폭발 파라미터")]
		[Tooltip("즉발 소폭발 반경(InstantExplode)")]
		public float instantExplosionRadius = 1.2f;
		[Tooltip("일반 폭발 반경(Explosive)")]
		public float explosionRadius = 1.6f;
		[Tooltip("폭발 VFX 프리팹")]
		public GameObject vfxExplosion;

		[Header("트레일/존 프리팹")]
		[Tooltip("프레임마다 드문드문 생성될 화염 존 프리팹")]
		public GameObject flameZonePrefab;   // 1초 존 권장
		[Tooltip("프레임마다 드문드문 생성될 전기/얼음 둔화 존 프리팹")]
		public GameObject electricZonePrefab;// 3초 존 권장

		[Header("스프라이트 회전")]
		public SpriteRenderer sprite;
		public enum FacingAxis { Right, Up }
		public FacingAxis facingAxis = FacingAxis.Right;
		[Tooltip("회전 보정 각도(도)")]
		public float rotationOffsetDeg = 0f;

		[Header("플레이어 관통")]
		[Tooltip("플레이어를 관통할지 여부(피해는 최초 1회)")]
		public bool penetratePlayer = true;

		private bool _hitPlayerOnce;
		private Rigidbody2D _rb;
		private Transform _player;
		private int _finalDamage;

		void Awake()
		{
			_rb = GetComponent<Rigidbody2D>();

#if UNITY_2023_1_OR_NEWER
			var ph = FindFirstObjectByType<PlayerHealth>();
#else
            var ph = FindObjectOfType<PlayerHealth>();
#endif
			_player = ph ? ph.transform : null;

			// ★ 대형(Large) 특성 즉시 반영
			if ((ability & AbilityFlags.Large) != 0)
			{
				// 스프라이트와 콜라이더 스케일 업
				float scale = 1.35f;
				transform.localScale *= scale;
				explosionRadius *= 1.25f;   // 폭발 반경 약간 상향
				nearMissRadius *= 1.2f;   // 근접 판정도 상향
			}

			if (lifeSeconds > 0f) Invoke(nameof(SelfDestruct), lifeSeconds);
		}

		public void Fire(Vector2 dir, float extraSpeed = 0f)
		{
			dir = dir.normalized;
			float s = speed + extraSpeed + ((ability & AbilityFlags.SpeedUp) != 0 ? 8f : 0f);
#if UNITY_6000_0_OR_NEWER
			_rb.linearVelocity = dir * s;
#else
            _rb.velocity = dir * s;
#endif
			_finalDamage = Random.Range(Mathf.Min(minDamage, maxDamage),
										Mathf.Max(minDamage, maxDamage) + 1);

			if (onShotSfx) Audio2D(onShotSfx, sfxVolume, sfxPitch);

			if ((ability & AbilityFlags.InstantExplode) != 0)
				DoExplosion(transform.position, instantExplosionRadius);
		}

		void Update()
		{
#if UNITY_6000_0_OR_NEWER
			var v2 = _rb.linearVelocity;
#else
            var v2 = _rb.velocity;
#endif
			// 이동 중 트레일/존 생성
			if (v2.sqrMagnitude > 0.01f)
			{
				if ((ability & AbilityFlags.FlameTrail) != 0 && flameZonePrefab && Time.frameCount % 5 == 0)
					Instantiate(flameZonePrefab, transform.position, Quaternion.identity);

				if ((ability & AbilityFlags.ElectricTrail) != 0 && electricZonePrefab && Time.frameCount % 10 == 0)
					Instantiate(electricZonePrefab, transform.position, Quaternion.identity);
			}

			// 근접 스침 SFX 1회
			if (!_nearPlayed && _player && Vector2.Distance(transform.position, _player.position) <= nearMissRadius)
			{
				PlayNear();
				_nearPlayed = true;
			}
		}

		void OnTriggerEnter2D(Collider2D other)
		{
			// 1) 플레이어(IDamageable) 체크
			var dmg = other.GetComponentInParent<IDamageable>();
			if (dmg != null)
			{
				if (penetratePlayer)
				{
					if (!_hitPlayerOnce)
					{
						dmg.TakeDamage(_finalDamage, false, 0);
						_hitPlayerOnce = true; // 관통은 계속, 피해는 1회
					}
				}
				else
				{
					dmg.TakeDamage(_finalDamage, false, 0);
					HitAndExplode(other.ClosestPoint(transform.position));
				}
				return;
			}

			// 2) 환경 충돌 처리(태그/레이어 마스크)
			if (collideWithBlocks && ((envMask.value & (1 << other.gameObject.layer)) != 0))
			{
				HitAndExplode(other.ClosestPoint(transform.position));
				return;
			}
		}

		void OnCollisionEnter2D(Collision2D col)
		{
			if (!collideWithBlocks) return;
			if ((envMask.value & (1 << col.collider.gameObject.layer)) != 0)
			{
				HitAndExplode(col.GetContact(0).point);
			}
		}

		void HitAndExplode(Vector2 pos)
		{
			if (onHitSfx) Audio2D(onHitSfx, sfxVolume, sfxPitch);
			if ((ability & AbilityFlags.Explosive) != 0) DoExplosion(pos, explosionRadius);
			SelfDestruct();
		}

		void DoExplosion(Vector2 pos, float radius)
		{
			if (vfxExplosion) Instantiate(vfxExplosion, pos, Quaternion.identity);

#if UNITY_2023_1_OR_NEWER
			var ph = FindFirstObjectByType<PlayerHealth>();
#else
            var ph = FindObjectOfType<PlayerHealth>();
#endif
			if (ph && Vector2.Distance(ph.transform.position, pos) <= radius)
				ph.TakeDamage(_finalDamage, false, 0);
		}

		void LateUpdate()
		{
#if UNITY_6000_0_OR_NEWER
			Vector2 v = _rb.linearVelocity;
#else
            Vector2 v = _rb.velocity;
#endif
			if (!sprite || v.sqrMagnitude < 0.0001f) return;

			Vector3 dir = ((Vector3)v).normalized;
			if (facingAxis == FacingAxis.Right) sprite.transform.right = dir;
			else sprite.transform.up = dir;

			if (Mathf.Abs(rotationOffsetDeg) > 0.01f)
				sprite.transform.Rotate(0f, 0f, rotationOffsetDeg, Space.Self);
		}

		void PlayNear()
		{
			if (nearMissSfxList == null || nearMissSfxList.Count == 0) return;
			AudioClip clip;
			if (nearMissRandom) clip = nearMissSfxList[Random.Range(0, nearMissSfxList.Count)];
			else { clip = nearMissSfxList[_nearIndex % nearMissSfxList.Count]; _nearIndex++; }
			Audio2D(clip, sfxVolume, sfxPitch);
		}

		void Audio2D(AudioClip clip, float vol, float pitch)
		{
			if (!clip) return;
			var go = new GameObject("[Ouranos_SFX]");
			var a = go.AddComponent<AudioSource>();
			a.playOnAwake = false; a.spatialBlend = 0f; a.volume = Mathf.Clamp01(vol); a.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
			a.clip = clip; a.Play();
			Destroy(go, clip.length / Mathf.Max(0.1f, a.pitch));
		}

		void SelfDestruct() => Destroy(gameObject);
	}
}
