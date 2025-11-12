// Assets/Scenes/Sc/우라노스/Ouranos_SniperBoss.cs
// 목적: HP% 하락(예: 80/60/40…) 임계 교차 시 보스를 "랜덤 스폰 포인트"로 순간이동.
// 변경: 20% 이하 고정 스폰(삭제). 모든 임계치는 동일하게 "랜덤 스폰" 처리.
// 보호: 시작 연출(0→Max 충전)로 인한 오작동 방지(하향 교차일 때만 발동).

using System.Collections.Generic;
using UnityEngine;

namespace Ouranos_Boss
{
	[DisallowMultipleComponent]
	public class Ouranos_SniperBoss : MonoBehaviour
	{
		[Header("Ouranos_참조")]
		[Tooltip("보스 체력/사망 상태 조회용")]
		public BossBase Ouranos_boss;

		[Header("Ouranos_스폰 포인트")]
		[Tooltip("임계 교차 시 이동할 랜덤 스폰 포인트들(Transform 배열)")]
		public Transform[] Ouranos_randomSpawnPoints;

		[Header("Ouranos_설정")]
		[Tooltip("임계값들(내림차순 권장). 예: 80, 60, 40")]
		public float[] Ouranos_phaseThresholds = new float[] { 80f, 60f, 40f };

		[Tooltip("직전 포인트를 바로 재사용 금지")]
		public bool Ouranos_noImmediateRepeat = true;

		[Tooltip("모든 포인트를 소진하기 전에는 재사용 금지")]
		public bool Ouranos_noReuseUntilExhausted = true;

		// 내부 상태
		float _lastHpPercent = 100f;                    // 시작 기준 100%로 고정 → 상승 구간 무시
		readonly HashSet<int> _usedIndices = new();     // 소진 풀 관리
		int _lastIndex = -1;                            // 직전 인덱스

		void OnEnable()
		{
#if UNITY_2023_1_OR_NEWER
			if (!Ouranos_boss) Ouranos_boss = FindFirstObjectByType<BossBase>();
#else
            if (!Ouranos_boss) Ouranos_boss = FindObjectOfType<BossBase>();
#endif
			_lastHpPercent = 100f;   // ★ 시작 연출 무시
			_usedIndices.Clear();
			_lastIndex = -1;
		}

		void Update()
		{
			if (!Ouranos_boss || Ouranos_boss.IsDead) return;

			float pct = (Ouranos_boss.MaxHP > 0)
				? (Ouranos_boss.CurrentHP * 100f / Ouranos_boss.MaxHP)
				: 0f;

			// ★ 하향 교차일 때만 검사
			if (pct < _lastHpPercent - 0.0001f)
				CheckThresholdCross(pct, _lastHpPercent);

			_lastHpPercent = pct;
		}

		/// <summary>
		/// 이전% → 현재%로 내려오며 임계(th)를 통과했는지 검사. 통과 시 1회 랜덤 텔레포트.
		/// </summary>
		void CheckThresholdCross(float currentPct, float prevPct)
		{
			if (Ouranos_phaseThresholds == null || Ouranos_phaseThresholds.Length == 0) return;

			for (int i = 0; i < Ouranos_phaseThresholds.Length; i++)
			{
				float th = Ouranos_phaseThresholds[i];

				// prev > th ≥ current  → th를 내려가며 통과
				if (prevPct > th && currentPct <= th)
				{
					TeleportToRandomPoint();
					break; // 한 프레임에 여러 임계 통과해도 연출상 1회만 이동
				}
			}
		}

		/// <summary>
		/// 랜덤 스폰 포인트로 순간이동. 비반복 규칙 적용.
		/// </summary>
		void TeleportToRandomPoint()
		{
			if (Ouranos_randomSpawnPoints == null || Ouranos_randomSpawnPoints.Length == 0) return;

			// 후보 풀 구성
			List<int> pool = new List<int>(Ouranos_randomSpawnPoints.Length);
			for (int i = 0; i < Ouranos_randomSpawnPoints.Length; i++)
			{
				if (Ouranos_noReuseUntilExhausted && _usedIndices.Contains(i)) continue;
				if (Ouranos_noImmediateRepeat && i == _lastIndex) continue;
				pool.Add(i);
			}

			// 풀 소진 시 초기화
			if (pool.Count == 0)
			{
				_usedIndices.Clear();
				for (int i = 0; i < Ouranos_randomSpawnPoints.Length; i++)
				{
					if (Ouranos_noImmediateRepeat && i == _lastIndex) continue;
					pool.Add(i);
				}
			}

			if (pool.Count == 0) return; // 포인트가 1개뿐인데 즉시 반복 금지인 경우 등

			int pick = pool[Random.Range(0, pool.Count)];
			_lastIndex = pick;
			_usedIndices.Add(pick);

			Transform t = Ouranos_randomSpawnPoints[pick];
			if (t) transform.position = t.position; // ★ 실제 이동
		}
	}
}
