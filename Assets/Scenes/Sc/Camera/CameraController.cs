using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 암전용 CanvasGroup 사용 시

/// <summary>
/// 카메라 연출 총괄 컨트롤러 (단일 파일)
/// - 패턴/스테이지/디버그 어디서든 쉽게 호출할 수 있도록 public 메서드 제공
/// - 기능: Follow, Zoom, Shake, Rotate, Fade(암전) + Spotlight(옵션 프리팹)
/// - 보스/플레이어 사망 시 ResetAll()로 즉시 초기화
/// - UI(Canvas)는 Screen Space - Overlay를 권장 (연출 영향 X)
/// </summary>
[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
	// ===== Singleton(선택) : 패턴 코드에서 쉽게 찾도록 제공 =====
	public static CameraController Instance { get; private set; }

	// ===== 공통 =====
	[Header("Common")]
	[Tooltip("제어할 카메라(비워두면 MainCamera 자동 할당)")]
	[SerializeField] private Camera cam;
	private float _defaultOrtho;               // 기본 ortho size
	private Vector3 _defaultPos;               // 시작 위치 (복원용)
	private Quaternion _defaultRot;            // 시작 회전 (복원용)

	// ===== Follow =====
	[Header("Follow")]
	[Tooltip("따라갈 대상(플레이어, 보스 등). null이면 고정")]
	[SerializeField] private Transform followTarget;
	[Tooltip("카메라 오프셋(Z는 -10 권장)")]
	[SerializeField] private Vector3 followOffset = new Vector3(0, 0, -10f);
	[Tooltip("따라가기 보간 속도")]
	[Range(0f, 20f)][SerializeField] private float followSmooth = 8f;

	// ===== Zoom =====
	Coroutine _zoomCR;

	// ===== Shake =====
	Coroutine _shakeCR;
	Vector3 _shakeOriginLocal;
	bool _isShaking;

	// ===== Rotate =====
	Coroutine _rotateCR;

	// ===== Fade / Spotlight =====
	[Header("Fade / Spotlight (옵션)")]
	[Tooltip("전체 암전용 CanvasGroup (UI 오버레이). 없으면 Fade 비활성")]
	[SerializeField] private CanvasGroup fadeCanvas; // 전체 검정 패널의 CanvasGroup
	Coroutine _fadeCR;

	[Tooltip("스포트라이트(구멍) 오버레이 프리팹(선택). 원형 마스크 UI 등")]
	[SerializeField] private GameObject spotlightOverlayPrefab;
	private GameObject _spotlightOverlay;
	private Transform _spotlightFollow;
	[Tooltip("스포트라이트가 대상 따라갈 때 보간 속도")]
	[SerializeField] private float spotlightFollowSmooth = 12f;
	RectTransform _spotlightRT; // 스포트라이트 UI 위치 제어(선택 구현)

	// ===== 기본 설정 =====
	void Awake()
	{
		if (Instance == null) Instance = this;
		else if (Instance != this) Destroy(gameObject);

		if (cam == null) cam = Camera.main;
		if (cam == null) Debug.LogError("[CameraController] Camera가 없습니다.");

		_defaultPos = transform.position;
		_defaultRot = transform.rotation;
		if (cam != null) _defaultOrtho = cam.orthographicSize;
	}

	void LateUpdate()
	{
		// Follow
		if (followTarget != null)
		{
			Vector3 targetPos = followTarget.position + followOffset;
			transform.position = Vector3.Lerp(transform.position, targetPos, followSmooth * Time.deltaTime);
		}

		// Spotlight(선택): 오버레이가 대상 따라다니게
		if (_spotlightOverlay != null && _spotlightFollow != null && _spotlightRT != null)
		{
			// 월드 → 스크린 변환 후 UI 위치 보간 (Canvas가 Overlay일 때)
			Vector3 screen = cam.WorldToScreenPoint(_spotlightFollow.position);
			Vector3 ui = Vector3.Lerp(_spotlightRT.position, screen, spotlightFollowSmooth * Time.deltaTime);
			_spotlightRT.position = ui;
		}
	}

	// ===================================================================
	// 외부 공개 API (패턴/스테이지 등에서 호출)
	// ===================================================================

	// ---------- Follow ----------
	/// <summary> 카메라가 대상을 따라가도록 설정 </summary>
	public void SetFollowTarget(Transform target, Vector3? offset = null, float? smooth = null)
	{
		followTarget = target;
		if (offset.HasValue) followOffset = offset.Value;
		if (smooth.HasValue) followSmooth = Mathf.Max(0f, smooth.Value);
	}
	/// <summary> 따라가기 해제(현재 위치에 고정) </summary>
	public void ClearFollowTarget() => followTarget = null;

	// ---------- Zoom ----------
	/// <summary> 특정 orthographicSize로 duration 동안 부드럽게 줌 </summary>
	public void ZoomTo(float targetOrtho, float duration = 0.4f)
	{
		if (cam == null) return;
		if (_zoomCR != null) StopCoroutine(_zoomCR);
		_zoomCR = StartCoroutine(CoZoomTo(targetOrtho, Mathf.Max(0.01f, duration)));
	}
	/// <summary> 기본 크기(_defaultOrtho)로 복귀 </summary>
	public void ZoomReset(float duration = 0.25f) => ZoomTo(_defaultOrtho, duration);

	// ---------- Shake ----------
	/// <summary> 화면 흔들림(Perlin 기반): duration초 동안 magnitude 강도로 </summary>
	public void Shake(float duration, float magnitude = 0.2f, float frequency = 25f)
	{
		// 이미 흔들리는 중이면 기존 코루틴을 멈추고 새로 시작한다 (중복 진동 제어)
		if (_isShaking && _shakeCR != null)
			StopCoroutine(_shakeCR);

		_shakeCR = StartCoroutine(CoShake(duration, magnitude, frequency));
	}


	// ---------- Rotate ----------
	/// <summary>
	/// 회전 시작: angle(양수=반시계), speed(도/초). 
	/// infinite=true면 계속 회전, false면 angle만큼 회전.
	/// </summary>
	public void StartRotate(float angle, float speed = 90f, bool infinite = false)
	{
		if (_rotateCR != null) StopCoroutine(_rotateCR);
		_rotateCR = StartCoroutine(CoRotate(angle, Mathf.Max(0f, speed), infinite));
	}
	/// <summary> 회전 중단 및 기본 회전 복원 </summary>
	public void StopRotate(bool reset = true)
	{
		if (_rotateCR != null) StopCoroutine(_rotateCR);
		if (reset) transform.rotation = _defaultRot;
	}

	// ---------- Fade (암전) ----------
	/// <summary> 화면 암전: duration 동안 알파를 0→1 </summary>
	public void FadeOut(float duration = 0.5f)
	{
		if (fadeCanvas == null) return;
		if (_fadeCR != null) StopCoroutine(_fadeCR);
		_fadeCR = StartCoroutine(CoFade(0f, 1f, Mathf.Max(0.01f, duration)));
	}
	/// <summary> 화면 밝게: duration 동안 알파를 1→0 </summary>
	public void FadeIn(float duration = 0.5f)
	{
		if (fadeCanvas == null) return;
		if (_fadeCR != null) StopCoroutine(_fadeCR);
		_fadeCR = StartCoroutine(CoFade(1f, 0f, Mathf.Max(0.01f, duration)));
	}

	// ---------- Spotlight (옵션: 원형 구멍) ----------
	/// <summary>
	/// 스포트라이트 연출 시작(옵션 프리팹 필요). 
	/// - overlayPrefab은 화면 전체 검정 위 '구멍' 마스크가 달린 UI 프리팹을 가정.
	/// - target을 따라다니는 원형 구멍 연출.
	/// </summary>
	public void SpotlightOn(Transform target)
	{
		if (spotlightOverlayPrefab == null || cam == null) return;
		if (_spotlightOverlay == null)
		{
			_spotlightOverlay = Instantiate(spotlightOverlayPrefab);
			_spotlightRT = _spotlightOverlay.GetComponent<RectTransform>();
		}
		_spotlightOverlay.SetActive(true);
		_spotlightFollow = target;
	}
	/// <summary> 스포트라이트 연출 종료 </summary>
	public void SpotlightOff()
	{
		if (_spotlightOverlay != null) _spotlightOverlay.SetActive(false);
		_spotlightFollow = null;
	}

	// ---------- Reset ----------
	/// <summary> 모든 연출/코루틴 중단 및 카메라 상태 초기화 </summary>
	public void ResetAll(bool resetZoom = true, bool resetRotation = true, bool clearFollow = false)
	{
		// 코루틴 정리
		if (_zoomCR != null) StopCoroutine(_zoomCR);
		if (_shakeCR != null) StopCoroutine(_shakeCR);
		if (_rotateCR != null) StopCoroutine(_rotateCR);
		if (_fadeCR != null) StopCoroutine(_fadeCR);
		_zoomCR = _shakeCR = _rotateCR = _fadeCR = null;
		_isShaking = false;

		// 위치/회전/줌
		transform.position = _defaultPos;
		if (resetRotation) transform.rotation = _defaultRot;
		if (resetZoom && cam != null) cam.orthographicSize = _defaultOrtho;

		// 흔들림 원위치
		transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, _defaultPos.z);

		// 암전 해제
		if (fadeCanvas != null) fadeCanvas.alpha = 0f;

		// 스포트라이트 해제
		SpotlightOff();

		// 따라가기 해제(선택)
		if (clearFollow) followTarget = null;
	}

	// ===================================================================
	// 내부 코루틴
	// ===================================================================
	IEnumerator CoZoomTo(float target, float duration)
	{
		float start = cam.orthographicSize;
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			cam.orthographicSize = Mathf.Lerp(start, target, t / duration);
			yield return null;
		}
		cam.orthographicSize = target;
	}

	IEnumerator CoShake(float duration, float magnitude, float frequency)
	{
		_isShaking = true;
		_shakeOriginLocal = transform.localPosition;
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			// Perlin 기반 부드러운 랜덤
			float x = (Mathf.PerlinNoise(0f, Time.time * frequency) * 2f - 1f) * magnitude;
			float y = (Mathf.PerlinNoise(1f, Time.time * frequency) * 2f - 1f) * magnitude;
			transform.localPosition = _shakeOriginLocal + new Vector3(x, y, 0f);
			yield return null;
		}
		transform.localPosition = _shakeOriginLocal;
		_isShaking = false;
	}

	IEnumerator CoRotate(float angle, float speed, bool infinite)
	{
		if (infinite)
		{
			while (true)
			{
				transform.Rotate(0f, 0f, speed * Time.deltaTime);
				yield return null;
			}
		}
		else
		{
			float remaining = Mathf.Abs(angle);
			float dir = Mathf.Sign(angle); // +반시계 / -시계
			while (remaining > 0f)
			{
				float step = Mathf.Min(remaining, speed * Time.deltaTime);
				transform.Rotate(0f, 0f, step * dir);
				remaining -= step;
				yield return null;
			}
		}
	}

	IEnumerator CoFade(float from, float to, float duration)
	{
		float t = 0f;
		fadeCanvas.alpha = from;
		while (t < duration)
		{
			t += Time.deltaTime;
			fadeCanvas.alpha = Mathf.Lerp(from, to, t / duration);
			yield return null;
		}
		fadeCanvas.alpha = to;
	}
}
