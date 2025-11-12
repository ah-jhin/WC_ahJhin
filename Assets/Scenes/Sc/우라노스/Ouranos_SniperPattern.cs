// Assets/Scenes/Sc/우라노스/Ouranos_SniperPattern.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ouranos_Boss
{
	[DisallowMultipleComponent]
	public class Ouranos_SniperPattern : MonoBehaviour
	{
		// ===== 기존 필드들(생략 없이 유지했음) =====
		public BossBase Ouranos_boss;
		public Transform Ouranos_player;
		public Transform Ouranos_fireOrigin;
		public Ouranos_SniperReticle Ouranos_reticlePrefab;
		public Ouranos_SniperProjectile Ouranos_bulletPrefab;

		public float Ouranos_startDelaySeconds = 3f;

		public enum Ouranos_StartAnchor { Boss, Player }
		public Ouranos_StartAnchor Ouranos_startAnchor = Ouranos_StartAnchor.Boss;
		public Vector2 Ouranos_reticleOffsetBoss = new Vector2(1.5f, 0.8f);
		public Vector2 Ouranos_reticleOffsetPlayer = new Vector2(6f, 3f);

		public bool Ouranos_cumulativeAbilities = true;

		[System.Serializable]
		public class Ouranos_PhaseConfig
		{
			public float Ouranos_thresholdPercent = 80f;
			// 탄/발사
			public Ouranos_SniperProjectile.AbilityFlags Ouranos_ability;
			public float Ouranos_extraSpeed = 0f;
			public int Ouranos_burstCount = 1;
			public float Ouranos_burstInterval = 0.12f;
			public bool Ouranos_penetratePlayer = true;
			// 타이밍
			public float Ouranos_aimSeconds = 0.9f;
			public float Ouranos_lockSeconds = 0.25f;
			public Vector2 Ouranos_cooldownRange = new Vector2(1.2f, 1.6f);
			// 조준경 추적(주입)
			public bool Ouranos_useInitialBoost = true;
			public float Ouranos_initialBoostSeconds = 1f;
			public float Ouranos_initialBoostSpeed = 100f;
			public float Ouranos_followSpeed = 15f;
			public bool Ouranos_useSmoothDamp = false;
			public float Ouranos_smoothTime = 0.02f;
			public float Ouranos_stopDistance = 0.05f;
			public float Ouranos_reticleLifeAfterShot = 0f;
			// 탄 프리팹(선택)
			public Ouranos_SniperProjectile Ouranos_phaseBulletPrefab;
			// 조준경 색
			public bool Ouranos_overrideReticleColor = false;
			public Color Ouranos_reticleColor = Color.white;
		}
		public List<Ouranos_PhaseConfig> Ouranos_phases = new();

		// ===== 디버그/안전장치 =====
		[Header("Debug")]
		[SerializeField] bool Ouranos_verboseDebug = true;

		static Ouranos_SniperReticle Ouranos_activeReticle;
		Coroutine _loop;

		void Awake()
		{
			// 페이즈를 큰→작으로 정렬(예: 80,60,40…)
			if (Ouranos_phases != null && Ouranos_phases.Count > 1)
				Ouranos_phases.Sort((a, b) => b.Ouranos_thresholdPercent.CompareTo(a.Ouranos_thresholdPercent));
		}

		void OnEnable()
		{
#if UNITY_2023_1_OR_NEWER
			if (!Ouranos_player) Ouranos_player = FindFirstObjectByType<PlayerHealth>()?.transform;
			if (!Ouranos_boss) Ouranos_boss = FindFirstObjectByType<BossBase>();
#else
            if (!Ouranos_player) Ouranos_player = FindObjectOfType<PlayerHealth>()?.transform;
            if (!Ouranos_boss)   Ouranos_boss   = FindObjectOfType<BossBase>();
#endif
			if (!Ouranos_fireOrigin) Ouranos_fireOrigin = transform;

			if (_loop != null) StopCoroutine(_loop);
			_loop = StartCoroutine(MainLoop());
		}

		void OnDisable()
		{
			if (_loop != null) StopCoroutine(_loop);
			if (Ouranos_activeReticle)
			{
				if (Ouranos_activeReticle.gameObject) Destroy(Ouranos_activeReticle.gameObject);
				Ouranos_activeReticle = null;
			}
		}

		// ===== 강제 시험 발사(인스펙터 버튼으로 호출 가능) =====
		[ContextMenu("DEBUG Fire Now")]
		public void Ouranos_DebugFireNow()
		{
			if (gameObject.activeInHierarchy)
				StartCoroutine(FireOnce());
		}

		IEnumerator MainLoop()
		{
			// 시작 지연
			if (Ouranos_startDelaySeconds > 0f)
				yield return new WaitForSeconds(Ouranos_startDelaySeconds);

			// 참조 준비 대기(중요: 종료하지 말고 기다린다)
			yield return StartCoroutine(WaitUntilReady());

			while (true)
			{
				if (Ouranos_boss && Ouranos_boss.IsDead)
				{
					if (Ouranos_verboseDebug) DBG("Stop loop: boss dead");
					yield break;
				}

				yield return StartCoroutine(FireOnce());
			}
		}

		IEnumerator WaitUntilReady()
		{
			// 필요한 참조가 연결될 때까지 대기 재시도
			while (!Ouranos_player || !Ouranos_reticlePrefab || !Ouranos_bulletPrefab)
			{
				if (Ouranos_verboseDebug)
				{
					DBG($"Waiting refs. player:{(Ouranos_player ? "ok" : "null")} " +
						$"reticle:{(Ouranos_reticlePrefab ? "ok" : "null")} " +
						$"bullet:{(Ouranos_bulletPrefab ? "ok" : "null")}");
				}

#if UNITY_2023_1_OR_NEWER
				if (!Ouranos_player) Ouranos_player = FindFirstObjectByType<PlayerHealth>()?.transform;
				if (!Ouranos_boss) Ouranos_boss = FindFirstObjectByType<BossBase>();
#else
                if (!Ouranos_player) Ouranos_player = FindObjectOfType<PlayerHealth>()?.transform;
                if (!Ouranos_boss)   Ouranos_boss   = FindObjectOfType<BossBase>();
#endif
				if (!Ouranos_fireOrigin) Ouranos_fireOrigin = transform;

				yield return new WaitForSeconds(0.25f);
			}
		}

		IEnumerator FireOnce()
		{
			// 페이즈 선택
			var cfg = SelectPhaseSafe();
			if (cfg == null)
			{
				if (Ouranos_verboseDebug) DBG("No phase config. Wait 0.5s");
				yield return new WaitForSeconds(0.5f);
				yield break;
			}

			// 레퍼런스 검증(발사 필수)
			if (!Ouranos_player || !Ouranos_reticlePrefab)
			{
				if (Ouranos_verboseDebug) DBG("Missing player/reticle. Re-wait.");
				yield return StartCoroutine(WaitUntilReady());
				yield break;
			}

			// 기존 조준경 제거 → 항상 1개 유지
			if (Ouranos_activeReticle)
			{
				if (Ouranos_activeReticle.gameObject) Destroy(Ouranos_activeReticle.gameObject);
				Ouranos_activeReticle = null;
			}

			// 시작 위치
			Vector3 startPos;
			if (Ouranos_startAnchor == Ouranos_StartAnchor.Boss)
			{
				var origin = Ouranos_fireOrigin ? Ouranos_fireOrigin : transform;
				startPos = origin.position + (Vector3)Ouranos_reticleOffsetBoss;
			}
			else
			{
				startPos = Ouranos_player.position + (Vector3)Ouranos_reticleOffsetPlayer;
			}

			// 조준경 생성 및 주입
			var ret = Instantiate(Ouranos_reticlePrefab, startPos, Quaternion.identity);
			ret.transform.position = startPos;
			Ouranos_activeReticle = ret;

			ret.SetTarget(Ouranos_player);
			ret.SetColor(cfg.Ouranos_overrideReticleColor ? cfg.Ouranos_reticleColor : Color.white);

			ret.Ouranos_useInitialBoost = cfg.Ouranos_useInitialBoost;
			ret.Ouranos_initialBoostSeconds = cfg.Ouranos_initialBoostSeconds;
			ret.Ouranos_initialBoostSpeed = cfg.Ouranos_initialBoostSpeed;
			ret.Ouranos_followSpeed = cfg.Ouranos_followSpeed;
			ret.Ouranos_useSmoothDamp = cfg.Ouranos_useSmoothDamp;
			ret.Ouranos_smoothTime = cfg.Ouranos_smoothTime;
			ret.Ouranos_stopDistance = Mathf.Max(0f, cfg.Ouranos_stopDistance);

			if (Ouranos_verboseDebug) DBG($"AIM {cfg.Ouranos_aimSeconds}s, LOCK {cfg.Ouranos_lockSeconds}s");

			// 조준
			yield return new WaitForSeconds(Mathf.Max(0.01f, cfg.Ouranos_aimSeconds));

			// 잠금
			ret.FreezeOn();
			yield return new WaitForSeconds(Mathf.Max(0.01f, cfg.Ouranos_lockSeconds));

			// 발사 준비
			if (!Ouranos_bulletPrefab && !cfg.Ouranos_phaseBulletPrefab)
			{
				if (Ouranos_verboseDebug) DBG("No bullet prefab. Skip shot.");
				SafeKillReticle(ret, 0f);
				yield return new WaitForSeconds(0.5f);
				yield break;
			}

			Vector3 lockPos = ret ? ret.transform.position : Ouranos_player.position;
			Vector2 dir = (lockPos - (Ouranos_fireOrigin ? Ouranos_fireOrigin.position : transform.position)).normalized;

			var prefabToUse = cfg.Ouranos_phaseBulletPrefab ? cfg.Ouranos_phaseBulletPrefab : Ouranos_bulletPrefab;
			int bursts = Mathf.Max(1, cfg.Ouranos_burstCount);
			var ability = EffectiveAbility(cfg);

			// 연발
			for (int i = 0; i < bursts; i++)
			{
				var bullet = Instantiate(prefabToUse,
					Ouranos_fireOrigin ? Ouranos_fireOrigin.position : transform.position,
					Quaternion.identity);

				bullet.ability = ability;
				bullet.penetratePlayer = cfg.Ouranos_penetratePlayer;
				bullet.Fire(dir, cfg.Ouranos_extraSpeed);

				if (Ouranos_verboseDebug) DBG($"Shot {i + 1}/{bursts}, ability={ability}");

				if (i < bursts - 1 && cfg.Ouranos_burstInterval > 0f)
					yield return new WaitForSeconds(cfg.Ouranos_burstInterval);
			}

			// 조준경 생존 시간
			SafeKillReticle(ret, cfg.Ouranos_reticleLifeAfterShot);

			// 쿨다운
			float cd = Random.Range(cfg.Ouranos_cooldownRange.x, cfg.Ouranos_cooldownRange.y);
			yield return new WaitForSeconds(Mathf.Max(0.01f, cd));
		}

		void SafeKillReticle(Ouranos_SniperReticle r, float keepSec)
		{
			if (!r) return;
			if (keepSec > 0f) StartCoroutine(ReticleAutoKill(r, keepSec));
			else r.KillNow();
			if (Ouranos_activeReticle == r) Ouranos_activeReticle = null;
		}

		IEnumerator ReticleAutoKill(Ouranos_SniperReticle r, float delay)
		{
			yield return new WaitForSeconds(delay);
			if (r) r.KillNow();
		}

		Ouranos_PhaseConfig SelectPhaseSafe()
		{
			if (Ouranos_phases == null || Ouranos_phases.Count == 0)
			{
				// 안전 기본값
				return new Ouranos_PhaseConfig
				{
					Ouranos_thresholdPercent = 999f,
					Ouranos_ability = 0,
					Ouranos_burstCount = 1,
					Ouranos_aimSeconds = 0.8f,
					Ouranos_lockSeconds = 0.2f,
					Ouranos_cooldownRange = new Vector2(1.0f, 1.3f),
					Ouranos_useInitialBoost = true,
					Ouranos_initialBoostSeconds = 1f,
					Ouranos_initialBoostSpeed = 120f,
					Ouranos_followSpeed = 20f,
					Ouranos_useSmoothDamp = false,
					Ouranos_stopDistance = 0.05f
				};
			}

			float pct = 100f;
			if (Ouranos_boss && Ouranos_boss.MaxHP > 0)
				pct = (Ouranos_boss.CurrentHP * 100f) / Ouranos_boss.MaxHP;

			foreach (var p in Ouranos_phases)
				if (pct <= p.Ouranos_thresholdPercent) return p;

			return Ouranos_phases[Ouranos_phases.Count - 1];
		}

		Ouranos_SniperProjectile.AbilityFlags EffectiveAbility(Ouranos_PhaseConfig cfg)
		{
			if (!Ouranos_cumulativeAbilities) return cfg.Ouranos_ability;

			Ouranos_SniperProjectile.AbilityFlags eff = 0;
			for (int i = 0; i < Ouranos_phases.Count; i++)
				if (Ouranos_phases[i].Ouranos_thresholdPercent >= cfg.Ouranos_thresholdPercent)
					eff |= Ouranos_phases[i].Ouranos_ability;
			return eff;
		}

		void DBG(string msg)
		{
			Debug.Log($"[Ouranos_Pattern] {msg}", this);
		}
	}
}
