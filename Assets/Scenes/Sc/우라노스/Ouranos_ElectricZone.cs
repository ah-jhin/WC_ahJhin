// Assets/Boss/Ouranos_ElectricZone.cs
using UnityEngine;
using System.Collections.Generic;

namespace Ouranos_Boss
{
	/// <summary>
	/// 둔화 존(얼음/전기)
	/// - 진입 시 이동속도/점프높이 배수를 적용하고, 이탈 또는 만료 시 항상 복구
	/// - 동일 대상에게는 '존 안에 있는 동안' 주기적으로 만료 시간을 갱신하여 끊김 방지
	/// - 기본값: 이동/점프 20% 감소(0.8)
	/// </summary>
	[DisallowMultipleComponent]
	public class Ouranos_ElectricZone : MonoBehaviour
	{
		[Header("수명")]
		[Tooltip("존 지속 시간(초). 0이면 파괴되지 않음")]
		public float Ouranos_lifeSeconds = 3f;

		[Header("둔화 배수")]
		[Tooltip("이동 속도 배수(1이 기본). 0.8=20% 감소")]
		public float Ouranos_moveMul = 0.8f;   // 요구 2: 20% 낮춤
		[Tooltip("점프 높이 배수(1이 기본). 0.8=20% 감소")]
		public float Ouranos_jumpMul = 0.8f;   // 요구 2: 20% 낮춤

		[Header("재적용/만료")]
		[Tooltip("존 안에 있는 동안 이 주기로 만료 시간을 갱신(초)")]
		public float Ouranos_refreshInterval = 0.25f;
		[Tooltip("둔화 1회 적용 지속 시간(초). 존 밖에서도 이 시간 후 자동 복구")]
		public float Ouranos_slowDuration = 0.75f;

		// 대상별 리프레시 타이머
		private readonly Dictionary<Transform, float> _nextRefreshAt = new();
		// 대상별 고유 키
		private readonly Dictionary<Transform, string> _keys = new();

		void OnEnable()
		{
			if (Ouranos_lifeSeconds > 0) Destroy(gameObject, Ouranos_lifeSeconds);
		}

		void OnTriggerEnter2D(Collider2D other)
		{
			var pm = other.GetComponentInParent<PlayerMovement>();
			if (!pm) return;

			var mod = pm.GetComponent<Ouranos_ModStack>() ?? pm.gameObject.AddComponent<Ouranos_ModStack>();
			if (!_keys.TryGetValue(pm.transform, out var key))
			{
				key = "[Ouranos_Slow]#" + GetInstanceID() + "_" + pm.GetInstanceID();
				_keys[pm.transform] = key;
			}
			mod.Ouranos_ApplyOrRefresh(key, Ouranos_moveMul, Ouranos_jumpMul, Ouranos_slowDuration);
			_nextRefreshAt[pm.transform] = Time.time + Ouranos_refreshInterval;
		}

		void OnTriggerStay2D(Collider2D other)
		{
			var pm = other.GetComponentInParent<PlayerMovement>();
			if (!pm) return;

			if (!_keys.TryGetValue(pm.transform, out var key)) return;
			if (!_nextRefreshAt.TryGetValue(pm.transform, out var next)) next = 0f;

			if (Time.time >= next)
			{
				var mod = pm.GetComponent<Ouranos_ModStack>();
				if (mod) mod.Ouranos_ApplyOrRefresh(key, Ouranos_moveMul, Ouranos_jumpMul, Ouranos_slowDuration);
				_nextRefreshAt[pm.transform] = Time.time + Ouranos_refreshInterval;
			}
		}

		void OnTriggerExit2D(Collider2D other)
		{
			var pm = other.GetComponentInParent<PlayerMovement>();
			if (!pm) return;

			if (_keys.TryGetValue(pm.transform, out var key))
			{
				var mod = pm.GetComponent<Ouranos_ModStack>();
				if (mod) mod.Ouranos_Remove(key);            // 즉시 복구
			}
			_keys.Remove(pm.transform);
			_nextRefreshAt.Remove(pm.transform);
		}

		void OnDestroy()
		{
			// 혹시 파괴로 종료되더라도 모두 복구
			foreach (var kv in _keys)
			{
				var pm = kv.Key ? kv.Key.GetComponent<PlayerMovement>() : null;
				var mod = pm ? pm.GetComponent<Ouranos_ModStack>() : null;
				if (mod != null) mod.Ouranos_Remove(kv.Value);
			}
			_keys.Clear(); _nextRefreshAt.Clear();
		}
	}
}
