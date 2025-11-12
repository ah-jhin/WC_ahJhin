// 상단 using 동일
using System.Collections.Generic;
using UnityEngine;

namespace Ouranos_Boss
{
	public class Ouranos_StageEntitySpawner : MonoBehaviour
	{
		[Header("참조")]
		public BossBase boss;
		public PlayerHealth player;

		[Header("시작 조건")]
		public bool waitBossActive = true;    // 보스 활성 전에는 스폰 금지  ← ② 스포너가 게임 시작 전에 작동하지 않게
		public float startDelay = 0f;         // 보스 활성 후 추가 지연

		[Header("스폰 주기/범위")]
		public float interval = 5f;
		public float radius = 6f;             // 플레이어 중심 원형 반경  ← ③ 플레이어 중심 랜덤
		public float minDistance = 2f;        // 플레이어와 최소 거리

		[System.Serializable] public class PrefabList { public GameObject prefab; public int count = 1; }

		[Header("HP 100% 풀")] public List<PrefabList> pool100 = new();
		[Header("HP 80% 이하")] public List<PrefabList> pool80 = new();
		[Header("HP 60% 이하")] public List<PrefabList> pool60 = new();
		[Header("HP 40% 이하")] public List<PrefabList> pool40 = new();
		[Header("HP 20% 이하")] public List<PrefabList> pool20 = new();

		bool _running;

		void OnEnable()
		{
			if (boss != null) boss.OnBossDie += HandleBossDie;  // 보스 사망 → 전역 신호 + 즉시 정리
		}
		void OnDisable()
		{
			if (boss != null) boss.OnBossDie -= HandleBossDie;
		}

		void Start()
		{
#if UNITY_2023_1_OR_NEWER
			if (!player) player = FindFirstObjectByType<PlayerHealth>();
#else
            if (!player) player = FindObjectOfType<PlayerHealth>();
#endif
			StartCoroutine(Main());
		}

		System.Collections.IEnumerator Main()
		{
			// ② 보스가 활성화될 때까지 대기
			if (waitBossActive)
			{
				while (!boss || !boss.gameObject.activeInHierarchy || boss.IsDead)
					yield return null;
			}
			if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

			_running = true;
			while (_running && boss && !boss.IsDead && player && !player.IsDead)
			{
				SpawnOnce();
				yield return new WaitForSeconds(interval);
			}
		}

		void SpawnOnce()
		{
			float r = (float)boss.CurrentHP / Mathf.Max(1, boss.MaxHP);
			var list = new List<PrefabList>();
			list.AddRange(pool100);
			if (r <= 0.80f) list.AddRange(pool80);
			if (r <= 0.60f) list.AddRange(pool60);
			if (r <= 0.40f) list.AddRange(pool40);
			if (r <= 0.20f) list.AddRange(pool20);

			foreach (var item in list)
			{
				for (int i = 0; i < Mathf.Max(1, item.count); i++)
				{
					// ③ 플레이어 중심 원형 랜덤 위치
					Vector2 pos;
					int guard = 0;
					do
					{
						var off = Random.insideUnitCircle * radius;
						pos = (Vector2)player.transform.position + off;
						guard++;
					} while (Vector2.Distance(pos, player.transform.position) < minDistance && guard < 16);

					var go = Instantiate(item.prefab, pos, Quaternion.identity);
					go.tag = "Enemy"; // ① 태그 통일
				}
			}
		}

		void HandleBossDie(BossBase _)
		{
			Ouranos_GlobalSignals.RaiseBossDied(); // 전역 신호 브로드캐스트
												   // ① Enemy 태그 전체 정리
			var all = GameObject.FindGameObjectsWithTag("Enemy");
			foreach (var go in all) Destroy(go);
			_running = false;
		}
	}
}
