using UnityEngine;

public class ScrollPressure : MonoBehaviour
{
	[Header("스크롤 대상")]
	[Tooltip("왼쪽으로 끌려갈 플레이어 Transform. 비워두면 자동으로 탐색한다.")]
	public Transform playerTransform;

	[Tooltip("플레이어 체력 컴포넌트. 비워두면 자동 탐색.")]
	public PlayerHealth playerHealth;

	[Tooltip("보스 HP를 읽어올 BossBase. 비워두면 자동 탐색.")]
	public BossBase boss;

	[Tooltip("보스 소환 상태를 알기 위한 BossSequenceController. 비워두면 자동 탐색.")]
	public BossSequenceController bossSeq;

	[Header("스크롤 속도")]
	public float scrollSpeedNormal = 1f;
	public float scrollSpeedMid = 2f;
	public float scrollSpeedHard = 3f;

	[Header("HP 임계값")]
	public int secondHpThreshold = 1250;
	public int thirdHpThreshold = 500;

	[Header("시작 딜레이")]
	public float startDelay = 3f;

	bool _started = false;
	float _startTime = 0f;

	void Start()
	{
		TryFindAll();
	}

	void TryFindAll()
	{
		if (!playerTransform)
		{
			var mv = FindFirstObjectByType<PlayerMovement>();
			if (mv) playerTransform = mv.transform;
		}

		if (!playerHealth && playerTransform)
			playerHealth = playerTransform.GetComponent<PlayerHealth>();

		if (!boss)
			boss = FindFirstObjectByType<BossBase>();

		if (!bossSeq)
			bossSeq = FindFirstObjectByType<BossSequenceController>();
	}

	void Update()
	{
		TryFindAll();

		// 1) 아직 필수 레퍼런스가 없다 → 아무것도 하지 않음
		if (!playerTransform || !playerHealth || !bossSeq || !boss)
			return;

		// 2) 아직 보스전이 시작되지 않았다 → 압박 중단
		//    BossSequenceController.spawnMode == SceneObject 방식만 사용 중
		if (bossSeq.spawnMode == BossSequenceController.SpawnMode.SceneObject)
		{
			if (!bossSeq.sceneBossActor ||
				!bossSeq.sceneBossActor.gameObject.activeInHierarchy)
				return;
		}

		// 3) 보스 HP 충전 전(=0) → 압박 시작하면 안 됨
		if (boss.CurrentHP <= 0)
			return;

		// 4) 시작 시간 설정 (단 1회)
		if (!_started)
		{
			_started = true;
			_startTime = Time.time + startDelay;
			return;
		}

		// 5) startDelay 시간이 아직 안 됨
		if (Time.time < _startTime)
			return;

		// 6) 플레이어 또는 보스가 죽으면 압박 즉시 중단
		if (playerHealth.IsDead || boss.IsDead)
			return;

		// 7) 보스 HP에 따른 압박 속도 계산
		float speed = GetScrollSpeed();

		// 8) 실제로 플레이어를 왼쪽으로 끌어당김
		Vector3 p = playerTransform.position;
		p.x -= speed * Time.deltaTime;
		playerTransform.position = p;
	}

	float GetScrollSpeed()
	{
		int hp = boss.CurrentHP;

		if (hp <= thirdHpThreshold) return scrollSpeedHard;
		if (hp <= secondHpThreshold) return scrollSpeedMid;
		return scrollSpeedNormal;
	}
}
