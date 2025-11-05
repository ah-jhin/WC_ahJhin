using System.Collections;
using UnityEngine;

/// <summary>
/// [단일 패턴용 컴포넌트]
/// - 경고 프리팹을 잠깐 표시하고(SFX 포함) → 추가 지연 후 → 공격 프리팹을 생성한다.
/// - 생성 위치 모드 3가지:
///   1) UseWarningPosition        : 경고가 표시된 "그 좌표"에 공격 생성
///   2) SnapshotSpawnPointOnWarn  : 경고가 뜬 순간의 spawnPoint 좌표를 스냅샷해 그 자리에서 생성
///   3) LiveSpawnPoint            : 생성 순간의 spawnPoint 좌표(실시간 추적)에서 생성
/// - 기존 시스템과 충돌하지 않음. Boss 오브젝트나 빈 오브젝트 아무데나 붙여서 쓰면 된다.
/// </summary>
[DisallowMultipleComponent]
public class TelegraphAttack : MonoBehaviour
{
	// ───────────────────────────────────────────────────────────
	// 실행 제어
	// ───────────────────────────────────────────────────────────
	[Header("실행 제어")]
	[Tooltip("씬 시작 시 자동 1회 실행")]
	public bool autoRun = true;

	[Tooltip("자동 실행 시 시작 지연(초)")]
	public float startDelay = 0f;

	[Tooltip("루프 실행. 매 실행 사이에 다음 딜레이를 기다린다")]
	public bool loop = false;

	[Tooltip("루프일 때 다음 실행까지 대기 시간(최소~최대, 초)")]
	public Vector2 nextDelayRange = new Vector2(0.8f, 1.2f);

	// ───────────────────────────────────────────────────────────
	// 경고(텔레그래프)
	// ───────────────────────────────────────────────────────────
	[Header("경고(텔레그래프)")]
	[Tooltip("경고 표시용 프리팹(화살표/타겟 등). 비우면 경고 생략")]
	public GameObject warningPrefab;

	[Tooltip("경고를 찍을 위치. 비우면 spawnPoint → 이 컴포넌트의 Transform 순으로 사용")]
	public Transform warningPoint;

	[Tooltip("경고 유지 시간(초). 0 이하면 즉시 삭제 안함")]
	public float warningDuration = 0.5f;

	[Tooltip("경고가 나타날 때 재생할 SFX(선택)")]
	public AudioClip warningSfx;

	[Range(0f, 1f)]
	public float warningSfxVolume = 0.8f;

	[Tooltip("경고 후 실제 공격까지 추가 지연(초)")]
	public float attackDelayAfterWarning = 0.2f;

	[Tooltip("낚시 경고 확률(0~1). Random.value < 이 값이면 공격을 스킵")]
	[Range(0f, 1f)]
	public float fakeWarningChance = 0f;

	// ───────────────────────────────────────────────────────────
	// 공격(실제 생성)
	// ───────────────────────────────────────────────────────────
	public enum SpawnMode
	{
		UseWarningPosition,        // 경고가 뜬 "그 좌표"에서 생성
		SnapshotSpawnPointOnWarn,  // 경고 시점의 spawnPoint 좌표 스냅샷에서 생성
		LiveSpawnPoint             // 생성 시점의 spawnPoint 좌표에서 생성
	}

	[Header("공격 프리팹")]
	[Tooltip("실제 생성할 공격 프리팹(탄/레이저 등)")]
	public GameObject attackPrefab;

	[Tooltip("공격 생성 기준이 되는 Transform(플레이어 등). 씬 오브젝트여야 한다.")]
	public Transform spawnPoint;

	[Tooltip("생성 위치 모드 선택")]
	public SpawnMode spawnPosMode = SpawnMode.UseWarningPosition;

	[Tooltip("생성 좌표에 더할 오프셋(월드 좌표 기준)")]
	public Vector3 spawnOffset = Vector3.zero;

	[Tooltip("회전은 경고(또는 스폰포인트)의 회전을 그대로 쓸지 여부")]
	public bool inheritRotation = true;

	// ───────────────────────────────────────────────────────────
	// 내부 캐시(스냅샷)
	// ───────────────────────────────────────────────────────────
	private bool _hasSnapshot;
	private Vector3 _snapshotPos;
	private Quaternion _snapshotRot;
	private Coroutine _loopCo;

	// ───────────────────────────────────────────────────────────
	// 수명주기
	// ───────────────────────────────────────────────────────────
	private void Start()
	{
		if (autoRun)
		{
			if (_loopCo != null) StopCoroutine(_loopCo);
			_loopCo = StartCoroutine(RunLoop());
		}
	}

	/// <summary>
	/// 외부에서 수동으로 1회 실행하고 싶을 때 호출
	/// </summary>
	public void TriggerOnce()
	{
		StartCoroutine(RunOnce());
	}

	// 루프 실행
	private IEnumerator RunLoop()
	{
		if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

		do
		{
			yield return RunOnce();

			if (!loop) yield break;

			float wait = Mathf.Clamp(Random.Range(nextDelayRange.x, nextDelayRange.y),
									 0f, Mathf.Max(nextDelayRange.x, nextDelayRange.y));
			if (wait > 0f) yield return new WaitForSeconds(wait);

		} while (loop);
	}

	// 핵심 로직: 경고 → 지연 → 공격
	private IEnumerator RunOnce()
	{
		// 0) 스냅샷 초기화
		_hasSnapshot = false;

		// 1) 경고 위치 산출
		Vector3 warnPos;
		Quaternion warnRot;

		// 경고가 없을 수도 있으니 우선 위치만 정한다.
		if (warningPoint != null && warningPoint.gameObject.scene.IsValid())
		{
			warnPos = warningPoint.position;
			warnRot = warningPoint.rotation;
		}
		else if (spawnPoint != null && spawnPoint.gameObject.scene.IsValid())
		{
			warnPos = spawnPoint.position;
			warnRot = spawnPoint.rotation;
		}
		else
		{
			warnPos = transform.position;
			warnRot = transform.rotation;
		}

		// 2) 경고 프리팹 표시 + SFX
		if (warningPrefab != null)
		{
			var warn = Instantiate(warningPrefab, warnPos, warnRot);

			if (warningDuration > 0f)
				Destroy(warn, warningDuration);
		}

		if (warningSfx != null)
		{
			// 2D 재생. 감쇠 없음.
			var go = new GameObject("SFX_Telegraph_2D");
			var src = go.AddComponent<AudioSource>();
			src.clip = warningSfx;
			src.spatialBlend = 0f; // 0=2D
			src.volume = Mathf.Clamp01(warningSfxVolume);
			src.Play();
			Destroy(go, warningSfx.length + 0.05f);
		}

		// 3) 스폰 좌표 스냅샷(모드에 따라)
		if (spawnPosMode == SpawnMode.UseWarningPosition)
		{
			_snapshotPos = warnPos + spawnOffset;
			_snapshotRot = warnRot;
			_hasSnapshot = true;
		}
		else if (spawnPosMode == SpawnMode.SnapshotSpawnPointOnWarn)
		{
			// 경고가 뜬 "그 시점"의 spawnPoint를 스냅샷
			Vector3 basePos;
			Quaternion baseRot;

			if (spawnPoint != null && spawnPoint.gameObject.scene.IsValid())
			{
				basePos = spawnPoint.position;
				baseRot = spawnPoint.rotation;
			}
			else
			{
				basePos = transform.position;
				baseRot = transform.rotation;
			}

			_snapshotPos = basePos + spawnOffset;
			_snapshotRot = baseRot;
			_hasSnapshot = true;
		}
		// LiveSpawnPoint는 여기서 스냅샷을 잡지 않는다.

		// 4) 경고 후 추가 지연
		if (attackDelayAfterWarning > 0f)
			yield return new WaitForSeconds(attackDelayAfterWarning);

		// 5) 낚시 경고 처리
		if (fakeWarningChance > 0f && Random.value < fakeWarningChance)
			yield break; // 공격 스킵

		// 6) 공격 생성
		if (attackPrefab != null)
		{
			Vector3 pos;
			Quaternion rot;

			if (_hasSnapshot)
			{
				pos = _snapshotPos;
				rot = _snapshotRot;
			}
			else
			{
				// 실시간 스폰포인트 사용
				if (spawnPoint != null && spawnPoint.gameObject.scene.IsValid())
				{
					pos = spawnPoint.position + spawnOffset;
					rot = spawnPoint.rotation;
				}
				else
				{
					pos = transform.position + spawnOffset;
					rot = transform.rotation;
				}
			}

			if (!inheritRotation) rot = Quaternion.identity;

			Instantiate(attackPrefab, pos, rot);
		}
	}
}
