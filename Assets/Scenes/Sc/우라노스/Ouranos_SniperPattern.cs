// Assets/Scenes/Sc/우라노스/Ouranos_SniperPattern.cs
// Ouranos: 스나이퍼 패턴(조준 → 잠금 → 발사).
// - 선택 모드 추가: 누적(All) 또는 랜덤 1종(RandomOne)
// - RandomOne 모드에서는 조건 통과 탄들 중 "종류"를 균등 확률로 1개 뽑아 "정확히 1발"만 발사
// - 즉발/반복 금지 옵션(같은 탄 연속 금지, 소진 전 재사용 금지) 지원
// - 조준경은 항상 1개만 유지, 페이즈 값 런타임 주입

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ouranos_Boss
{
	[DisallowMultipleComponent]
	public class Ouranos_SniperPattern : MonoBehaviour
	{
		// ===== 참조 =====
		[Header("Ouranos_참조")]
		[Tooltip("보스 본체(BossBase). HP/사망 판정 조회")]
		public BossBase Ouranos_boss;
		[Tooltip("플레이어 Transform")]
		public Transform Ouranos_player;
		[Tooltip("발사 원점(없으면 이 오브젝트)")]
		public Transform Ouranos_fireOrigin;
		[Tooltip("조준경 프리팹")]
		public Ouranos_SniperReticle Ouranos_reticlePrefab;
		[Tooltip("기본 탄 프리팹")]
		public Ouranos_SniperProjectile Ouranos_bulletPrefab;

		// ===== 공통 규칙 =====
		[Header("Ouranos_공통")]
		[Tooltip("패턴 시작 지연(초)")]
		public float Ouranos_startDelaySeconds = 3f;

		public enum Ouranos_StartAnchor { Boss, Player }

		[Tooltip("조준 시작 기준")]
		public Ouranos_StartAnchor Ouranos_startAnchor = Ouranos_StartAnchor.Boss;

		[Tooltip("조준 시작 오프셋(보스 기준)")]
		public Vector2 Ouranos_reticleOffsetBoss = new Vector2(0f, 1.5f);

		[Tooltip("조준 시작 오프셋(플레이어 기준)")]
		public Vector2 Ouranos_reticleOffsetPlayer = new Vector2(30f, 10f);

		// ===== 선택 모드/누적 옵션 =====
		public enum Ouranos_BulletSelectionMode
		{
			CumulativeAll, // 조건 통과 탄들 전부 동시 발사(이전 동작)
			RandomOne      // 조건 통과 탄들 중 "종류"를 균등 랜덤 1개만 발사(요구 사항)
		}

		[Header("Ouranos_선택 모드")]
		[Tooltip("CumulativeAll=통과한 모든 탄을 발사, RandomOne=통과한 탄 중 랜덤 1종만 발사")]
		public Ouranos_BulletSelectionMode Ouranos_selectionMode = Ouranos_BulletSelectionMode.RandomOne;

		[Header("Ouranos_누적 관련(선택 모드가 CumulativeAll일 때 의미가 큼)")]
		[Tooltip("HP% 누적 모드. (Flags는 OR 누적, 프리팹은 수집 규칙에 사용)")]
		public bool Ouranos_cumulativeAbilities = true;

		[Tooltip("누적 모드에서 기본탄을 항상 포함")]
		public bool Ouranos_includeDefaultBulletAlways = true;

		[Tooltip("누적 모드가 아니어도(단일) 기본탄을 함께 포함")]
		public bool Ouranos_includeDefaultBulletEvenInSinglePhase = false;

		// ===== 반복 제어(랜덤 1종 전용 품질 옵션) =====
		[Header("Ouranos_반복 제어(RandomOne 전용)")]
		[Tooltip("같은 탄을 연속으로 다시 뽑지 않기")]
		public bool Ouranos_noImmediateRepeat = true;

		[Tooltip("모든 후보를 한 번씩 소진하기 전까지는 중복 선택 금지(셔플 백)")]
		public bool Ouranos_noReuseUntilExhausted = false;

		// ===== 가시성(겹침 방지; CumulativeAll에서만 체감) =====
		[Header("Ouranos_가시성(겹침 방지)")]
		[Tooltip("여러 탄을 동시에 쏠 때 각도 간격(도)")]
		public float Ouranos_fanDegrees = 0f;
		[Tooltip("여러 탄을 동시에 쏠 때 좌/우 오프셋 거리")]
		public float Ouranos_sideOffset = 0f;

		// ===== SFX(선택) =====
		[Header("Ouranos_SFX(선택)")]
		[Tooltip("패턴에서 발사 SFX를 1회만 재생(프리팹 SFX 중첩이 거슬릴 때)")]
		public bool Ouranos_playShotSfxHere = false;
		[Tooltip("패턴 발사 SFX")]
		public AudioClip Ouranos_shotSfx;
		[Range(0f, 1f)]
		[Tooltip("패턴 발사 SFX 볼륨")]
		public float Ouranos_shotSfxVolume = 0.8f;

		// ===== 페이즈 =====
		[System.Serializable]
		public class Ouranos_PhaseConfig
		{
			[Header("임계(HP%)")]
			[Tooltip("현재 HP%가 이 값 이하로 내려오면 통과(활성)")]
			public float Ouranos_thresholdPercent = 80f;

			[Header("탄/발사")]
			[Tooltip("이 페이즈에서 추가될 능력 플래그(누적 OR)")]
			public Ouranos_SniperProjectile.AbilityFlags Ouranos_ability;
			[Tooltip("추가 속도(+m/s)")]
			public float Ouranos_extraSpeed = 0f;
			[Tooltip("연발 횟수(RandomOne 모드에서도 적용됨)")]
			public int Ouranos_burstCount = 1;
			[Tooltip("연발 간격(초)")]
			public float Ouranos_burstInterval = 0.12f;
			[Tooltip("플레이어 관통 여부")]
			public bool Ouranos_penetratePlayer = true;

			[Header("타이밍")]
			[Tooltip("조준 시간(초)")]
			public float Ouranos_aimSeconds = 0.9f;
			[Tooltip("잠금(정지) 시간(초)")]
			public float Ouranos_lockSeconds = 0.25f;
			[Tooltip("발사 후 쿨다운 범위(초)")]
			public Vector2 Ouranos_cooldownRange = new Vector2(1.2f, 1.6f);

			[Header("조준경 추적 파라미터(이 페이즈에 주입)")]
			public bool Ouranos_useInitialBoost = true;
			public float Ouranos_initialBoostSeconds = 1f;
			public float Ouranos_initialBoostSpeed = 120f;
			public float Ouranos_followSpeed = 20f;
			public bool Ouranos_useSmoothDamp = false;
			public float Ouranos_smoothTime = 0.02f;
			public float Ouranos_stopDistance = 0.05f;
			[Tooltip("발사 후 조준경 유지(초). 0=즉시 제거")]
			public float Ouranos_reticleLifeAfterShot = 0f;

			[Header("탄 프리팹(선택: 이 페이즈에서 '추가'될 탄 종류)")]
			public Ouranos_SniperProjectile Ouranos_phaseBulletPrefab;

			[Header("조준경 색상(선택)")]
			public bool Ouranos_overrideReticleColor = false;
			public Color Ouranos_reticleColor = Color.white;
		}

		[Header("Ouranos_페이즈")]
		public List<Ouranos_PhaseConfig> Ouranos_phases = new();

		// ===== 디버그 =====
		[Header("Debug")]
		[SerializeField] bool Ouranos_verboseDebug = true;

		// 런타임 상태
		static Ouranos_SniperReticle Ouranos_activeReticle;
		Coroutine _loop;

		// RandomOne 셔플백 상태
		Ouranos_SniperProjectile _lastChosenPrefab = null;
		List<Ouranos_SniperProjectile> _shuffleBag = new List<Ouranos_SniperProjectile>(8);
		int _bagIndex = 0;
		string _bagKey = "";

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

		[ContextMenu("DEBUG Fire Now")]
		public void Ouranos_DebugFireNow()
		{
			if (gameObject.activeInHierarchy)
				StartCoroutine(FireOnce());
		}

		IEnumerator MainLoop()
		{
			if (Ouranos_startDelaySeconds > 0f)
				yield return new WaitForSeconds(Ouranos_startDelaySeconds);

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
			while (!Ouranos_player || !Ouranos_reticlePrefab || !Ouranos_bulletPrefab)
			{
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
			// 1) 페이즈 선택 + 현재 HP%
			var cfg = SelectPhaseSafe(out float currentPct);
			if (cfg == null)
			{
				if (Ouranos_verboseDebug) DBG("No phase config. Wait 0.5s");
				yield return new WaitForSeconds(0.5f);
				yield break;
			}

			// 2) 조준경 1개만 유지
			if (Ouranos_activeReticle)
			{
				if (Ouranos_activeReticle.gameObject) Destroy(Ouranos_activeReticle.gameObject);
				Ouranos_activeReticle = null;
			}

			// 3) 조준 시작 위치
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

			// 4) 조준경 생성/주입
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

			if (Ouranos_verboseDebug) DBG($"HP%={currentPct:0.0}, AIM {cfg.Ouranos_aimSeconds}s, LOCK {cfg.Ouranos_lockSeconds}s");

			// 5) 조준 → 잠금
			yield return new WaitForSeconds(Mathf.Max(0.01f, cfg.Ouranos_aimSeconds));
			ret.FreezeOn();
			yield return new WaitForSeconds(Mathf.Max(0.01f, cfg.Ouranos_lockSeconds));

			// 6) 능력 플래그(HP% 기준 누적 OR)
			var ability = EffectiveAbility_ByCurrentHP(currentPct);

			// 7) 후보 프리팹 수집
			var candidates = Ouranos_GetEligibleBulletPrefabs_ByCurrentHP(currentPct);

			// 8) 발사
			Vector3 muzzle = Ouranos_fireOrigin ? Ouranos_fireOrigin.position : transform.position;
			Vector3 lockPos = ret ? ret.transform.position : Ouranos_player.position;
			Vector2 baseDir = (lockPos - muzzle).normalized;

			if (Ouranos_playShotSfxHere && Ouranos_shotSfx)
			{
				var src = GetComponent<AudioSource>();
				if (src) src.PlayOneShot(Ouranos_shotSfx, Ouranos_shotSfxVolume);
			}

			if (Ouranos_selectionMode == Ouranos_BulletSelectionMode.RandomOne)
			{
				// 1) 후보에서 1종 선택(중복 방지 옵션 적용됨)
				var chosen = Ouranos_SelectOnePrefab(candidates);

				// 2) 페이즈의 연발/간격을 그대로 적용
				int bursts = Mathf.Max(1, cfg.Ouranos_burstCount);
				for (int i = 0; i < bursts; i++)
				{
					// 총구 위치/방향은 고정(조준락 위치 기준)
					var bullet = Instantiate(chosen, muzzle, Quaternion.identity);

					// 능력 플래그(누적 OR 또는 단일) 및 관통 여부 주입
					bullet.ability = ability;
					bullet.penetratePlayer = cfg.Ouranos_penetratePlayer;

					// 실제 발사(추가 속도 포함)
					bullet.Fire(baseDir, cfg.Ouranos_extraSpeed);

					// 연발 간격
					if (i < bursts - 1 && cfg.Ouranos_burstInterval > 0f)
						yield return new WaitForSeconds(cfg.Ouranos_burstInterval);
				}
			}
			else
			{
				int n = candidates.Count;
				float fanDeg = Ouranos_fanDegrees;
				float side = Ouranos_sideOffset;

				for (int i = 0; i < n; i++)
				{
					var prefab = candidates[i];
					if (!prefab) continue;

					float angle = (n <= 1 || fanDeg == 0f) ? 0f : ((i - (n - 1) * 0.5f) * fanDeg);
					Vector2 dir = Rotate(baseDir, angle);
					Vector2 right = new Vector2(-baseDir.y, baseDir.x);
					Vector3 spawn = muzzle + (Vector3)(right.normalized * side * (n <= 1 ? 0f : (i - (n - 1) * 0.5f)));

					var bullet = Instantiate(prefab, spawn, Quaternion.identity);
					bullet.ability = ability;
					bullet.penetratePlayer = cfg.Ouranos_penetratePlayer;
					bullet.Fire(dir, cfg.Ouranos_extraSpeed);
				}
			}

			// 9) 조준경 제거
			SafeKillReticle(ret, cfg.Ouranos_reticleLifeAfterShot);

			// 10) 쿨다운
			float cd = Random.Range(cfg.Ouranos_cooldownRange.x, cfg.Ouranos_cooldownRange.y);
			yield return new WaitForSeconds(Mathf.Max(0.01f, cd));
		}

		// ===== 선택/누적 계산(HP% 기준) =====
		Ouranos_PhaseConfig SelectPhaseSafe(out float currentPct)
		{
			currentPct = 100f;
			if (Ouranos_boss && Ouranos_boss.MaxHP > 0)
				currentPct = (Ouranos_boss.CurrentHP * 100f) / Ouranos_boss.MaxHP;

			if (Ouranos_phases == null || Ouranos_phases.Count == 0) return null;

			Ouranos_PhaseConfig best = null;
			float bestTh = float.PositiveInfinity;
			float minTh = float.PositiveInfinity;
			Ouranos_PhaseConfig minCfg = null;

			for (int i = 0; i < Ouranos_phases.Count; i++)
			{
				var p = Ouranos_phases[i];
				float th = p.Ouranos_thresholdPercent;

				if (th < minTh) { minTh = th; minCfg = p; }
				if (currentPct <= th && th < bestTh) { bestTh = th; best = p; }
			}
			return best ?? minCfg ?? Ouranos_phases[0];
		}

		List<Ouranos_SniperProjectile> Ouranos_GetEligibleBulletPrefabs_ByCurrentHP(float currentPct)
		{
			// 후보: 기본탄(+옵션) + 임계 통과 페이즈별 프리팹
			var list = new List<Ouranos_SniperProjectile>(8);

			bool addDefault =
				(Ouranos_cumulativeAbilities && Ouranos_includeDefaultBulletAlways) ||
				(!Ouranos_cumulativeAbilities && Ouranos_includeDefaultBulletEvenInSinglePhase);

			if (addDefault && Ouranos_bulletPrefab) list.Add(Ouranos_bulletPrefab);

			if (Ouranos_phases != null)
			{
				for (int i = 0; i < Ouranos_phases.Count; i++)
				{
					var p = Ouranos_phases[i];
					if (currentPct <= p.Ouranos_thresholdPercent && p.Ouranos_phaseBulletPrefab)
						list.Add(p.Ouranos_phaseBulletPrefab);
				}
			}

			// 비어 있으면 기본탄 보장
			if (list.Count == 0 && Ouranos_bulletPrefab)
				list.Add(Ouranos_bulletPrefab);

			if (Ouranos_verboseDebug) DBG(ListNames(list, "Candidates"));
			return list;
		}

		Ouranos_SniperProjectile.AbilityFlags EffectiveAbility_ByCurrentHP(float currentPct)
		{
			if (!Ouranos_cumulativeAbilities)
			{
				Ouranos_PhaseConfig cur = null;
				float bestTh = float.PositiveInfinity;
				for (int i = 0; i < Ouranos_phases.Count; i++)
				{
					var p = Ouranos_phases[i];
					if (currentPct <= p.Ouranos_thresholdPercent && p.Ouranos_thresholdPercent < bestTh)
					{
						bestTh = p.Ouranos_thresholdPercent; cur = p;
					}
				}
				return cur != null ? cur.Ouranos_ability : 0;
			}

			Ouranos_SniperProjectile.AbilityFlags eff = 0;
			for (int i = 0; i < Ouranos_phases.Count; i++)
			{
				var p = Ouranos_phases[i];
				if (currentPct <= p.Ouranos_thresholdPercent)
					eff |= p.Ouranos_ability;
			}
			return eff;
		}

		// ===== RandomOne 전용 선택기 =====
		Ouranos_SniperProjectile Ouranos_SelectOnePrefab(List<Ouranos_SniperProjectile> candidates)
		{
			// 1) "종류" 기준 균등 확률을 위해 고유화
			var seen = new HashSet<Ouranos_SniperProjectile>();
			var unique = new List<Ouranos_SniperProjectile>(candidates.Count);
			for (int i = 0; i < candidates.Count; i++)
				if (candidates[i] && seen.Add(candidates[i])) unique.Add(candidates[i]);

			if (unique.Count == 0) return Ouranos_bulletPrefab;

			if (Ouranos_noReuseUntilExhausted)
			{
				// 셔플백: 후보 구성이 바뀌면 새로 채움
				string key = BuildKey(unique);
				if (key != _bagKey || _shuffleBag.Count == 0)
				{
					_bagKey = key;
					_shuffleBag.Clear();
					_shuffleBag.AddRange(unique);
					Shuffle(_shuffleBag);
					_bagIndex = 0;
				}

				var pick = _shuffleBag[_bagIndex % _shuffleBag.Count];
				_bagIndex++;

				if (Ouranos_noImmediateRepeat && _shuffleBag.Count > 1 && pick == _lastChosenPrefab)
				{
					// 다음 것으로 교체
					pick = _shuffleBag[_bagIndex % _shuffleBag.Count];
					_bagIndex++;
				}

				_lastChosenPrefab = pick;
				if (Ouranos_verboseDebug) DBG($"Chosen(RandomOne-BAG): {pick.name}");
				return pick;
			}
			else
			{
				// 단순 균등 랜덤(즉시 반복 금지 옵션 적용)
				Ouranos_SniperProjectile pick = null;
				for (int t = 0; t < 4; t++)
				{
					pick = unique[Random.Range(0, unique.Count)];
					if (!(Ouranos_noImmediateRepeat && unique.Count > 1 && pick == _lastChosenPrefab))
						break;
				}
				_lastChosenPrefab = pick;
				if (Ouranos_verboseDebug) DBG($"Chosen(RandomOne): {pick.name}");
				return pick;
			}
		}

		// ===== 유틸 =====
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

		static Vector2 Rotate(Vector2 v, float deg)
		{
			if (deg == 0f) return v;
			float rad = deg * Mathf.Deg2Rad;
			float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
			return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
		}

		void Shuffle<T>(List<T> list)
		{
			for (int i = list.Count - 1; i > 0; i--)
			{
				int j = Random.Range(0, i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}

		string BuildKey(List<Ouranos_SniperProjectile> list)
		{
			var ids = new List<int>(list.Count);
			for (int i = 0; i < list.Count; i++) if (list[i]) ids.Add(list[i].GetInstanceID());
			ids.Sort();
			return string.Join(",", ids);
		}

		string ListNames<T>(List<T> list, string label)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append($"{label}[{(list != null ? list.Count : 0)}]: ");
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
					sb.Append(list[i] != null ? list[i].ToString() : "null")
					  .Append(i == list.Count - 1 ? "" : ", ");
			}
			return sb.ToString();
		}

		void DBG(string msg) => Debug.Log($"[Ouranos_Pattern] {msg}", this);
	}
}
