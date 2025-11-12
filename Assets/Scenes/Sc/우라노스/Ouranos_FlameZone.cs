// Assets/Boss/Ouranos_FlameZone.cs
using System.Collections.Generic;
using UnityEngine;

namespace Ouranos_Boss
{
	/// <summary>
	/// 화염 지속 피해 존
	/// - 존 내부에 있는 동안 일정 간격으로 DOT 적용
	/// - '대상별 무적시간(틱 간 보호시간)'을 필드로 노출
	/// - 존 수명 후 자동 파괴
	/// </summary>
	[DisallowMultipleComponent]
	public class Ouranos_FlameZone : MonoBehaviour
	{
		[Header("수명")]
		[Tooltip("이 존이 유지되는 시간(초)")]
		public float Ouranos_lifeSeconds = 1.0f;

		[Header("피해 설정")]
		[Tooltip("틱당 최소 피해")]
		public int Ouranos_minDamage = 5;
		[Tooltip("틱당 최대 피해")]
		public int Ouranos_maxDamage = 10;

		[Header("틱/무적(대상별)")]
		[Tooltip("피해를 가하는 주기(초)")]
		public float Ouranos_tickInterval = 0.25f;
		[Tooltip("같은 대상에게 다음 피해까지 면역 시간(초). 기본 1초")]
		public float Ouranos_perTargetIFrameSeconds = 1.0f;

		[Header("연출")]
		[Tooltip("틱이 발생할 때 2D SFX")]
		public AudioClip Ouranos_tickSfx;
		[Range(0f, 1f)] public float Ouranos_sfxVolume = 1f;

		// 대상별 마지막 피해 시각
		private readonly Dictionary<Transform, float> _lastHitAt = new();

		void OnEnable()
		{
			if (Ouranos_lifeSeconds > 0) Destroy(gameObject, Ouranos_lifeSeconds);
		}

		void OnTriggerStay2D(Collider2D other)
		{
			// 플레이어만 대상으로 삼는다면 태그/레이어 조건을 추가해도 된다.
			var ph = other.GetComponentInParent<PlayerHealth>();
			if (!ph) return;

			float now = Time.time;
			float last;
			_lastHitAt.TryGetValue(ph.transform, out last);

			// 틱 간격 + 대상별 I-Frame 모두 충족 시에만 피해
			if (now - last >= Mathf.Max(0.01f, Mathf.Max(Ouranos_tickInterval, Ouranos_perTargetIFrameSeconds)))
			{
				int dmg = Random.Range(Mathf.Min(Ouranos_minDamage, Ouranos_maxDamage),
									   Mathf.Max(Ouranos_minDamage, Ouranos_maxDamage) + 1);

				ph.TakeDamage(dmg, false, 0); // 프로젝트 표준 호출

				if (Ouranos_tickSfx) AudioSource.PlayClipAtPoint(Ouranos_tickSfx, ph.transform.position, Ouranos_sfxVolume);

				_lastHitAt[ph.transform] = now;
			}
		}
	}
}
