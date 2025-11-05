using UnityEngine;

/// <summary>
/// 타일 가능한 텍스처 1장을 UV 오프셋으로 스크롤하는 배경 전용 스크립트
/// - Quad + URP Unlit 머티리얼(텍스처 Wrap=Repeat) 전제
/// - 카메라 화면비 변화에 맞춰 스케일을 재조정
/// - 선택: 카메라 중심을 지속적으로 따라감(잘림 방지)
/// </summary>
[RequireComponent(typeof(Renderer))]
public class BackgroundUVScroller : MonoBehaviour
{
	[Header("Scroll")]
	[Tooltip("초당 UV 이동량. X=가로, Y=세로. 음수면 반대 방향")]
	public Vector2 uvSpeed = new Vector2(-0.1f, 0f);
	[Tooltip("일시정지 중에도 움직이게 하려면 체크")]
	public bool useUnscaledTime = false;

	[Header("Tiling")]
	[Tooltip("Quad 위에서 텍스처 반복 횟수(가로,세로)")]
	public Vector2 tiling = Vector2.one;

	[Header("Optional: 회전")]
	[Tooltip("배경 자체 회전 속도(도/초). 0이면 회전 없음")]
	public float rotationSpeed = 0f;

	[Header("화면 맞춤")]
	[Tooltip("사용할 카메라. 비우면 Main Camera 탐색")]
	public Camera targetCamera;
	[Tooltip("시작 시 카메라 뷰를 가득 채우도록 스케일 1회 조정")]
	public bool fitToCameraOnStart = true;
	[Tooltip("카메라 화면비/크기 변경 시 자동 재맞춤")]
	public bool fitContinuously = true;

	[Header("위치 추적")]
	[Tooltip("배경을 항상 카메라 중심(X,Y)으로 이동시킴")]
	public bool followCamera = true;

	// 내부
	Renderer _rend;
	Material _mat;           // 인스턴스 머티리얼(배경만 영향)
	int _mainTexId;          // "_BaseMap"(URP) 또는 "_MainTex"(Built-in)
	Vector2 _uv;             // 누적 오프셋
	float _lastAspect, _lastOrtho, _lastFov, _depth;

	void Awake()
	{
		_rend = GetComponent<Renderer>();
		_mat = _rend.material; // sharedMaterial 사용 금지(다른 오브젝트 오염)

		int baseMap = Shader.PropertyToID("_BaseMap");
		int mainTex = Shader.PropertyToID("_MainTex");
		_mainTexId = _mat.HasProperty(baseMap) ? baseMap : mainTex;

		_mat.SetTextureScale(_mainTexId, tiling);

		if (targetCamera == null) targetCamera = Camera.main;
		if (fitToCameraOnStart) FitQuadToCamera();
		CacheCamState();
	}

	void LateUpdate()
	{
		float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

		// 회전
		if (rotationSpeed != 0f)
			transform.Rotate(0f, 0f, rotationSpeed * dt);

		// UV 스크롤
		_uv += uvSpeed * dt;
		_uv.x = Mathf.Repeat(_uv.x, 1f);
		_uv.y = Mathf.Repeat(_uv.y, 1f);
		_mat.SetTextureOffset(_mainTexId, _uv);

		// 화면비, 사이즈 변화에 따른 재맞춤
		if (fitContinuously && CameraChanged())
		{
			FitQuadToCamera();
			CacheCamState();
		}

		// 카메라 중심 추적(잘림 방지의 핵심)
		if (followCamera && targetCamera != null)
		{
			Vector3 cp = targetCamera.transform.position;
			// 깊이 유지: 카메라 앞쪽에 배치되도록 기존 거리(_depth) 보존
			transform.position = new Vector3(cp.x, cp.y, cp.z + _depth);
		}
	}

	void FitQuadToCamera()
	{
		if (targetCamera == null) return;

		if (targetCamera.orthographic)
		{
			float h = targetCamera.orthographicSize * 2f; // 월드 높이
			float w = h * targetCamera.aspect;            // 월드 너비
			transform.localScale = new Vector3(w, h, 1f);
		}
		else
		{
			float d = Mathf.Abs((targetCamera.transform.position - transform.position).z);
			float h = 2f * d * Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
			float w = h * targetCamera.aspect;
			transform.localScale = new Vector3(w, h, 1f);
		}
	}

	void CacheCamState()
	{
		if (targetCamera == null) return;
		_lastAspect = targetCamera.aspect;
		_lastOrtho = targetCamera.orthographic ? targetCamera.orthographicSize : 0f;
		_lastFov = targetCamera.orthographic ? 0f : targetCamera.fieldOfView;

		// 현재 카메라와의 Z거리 보존(배경이 카메라와 함께 움직여도 깊이 유지)
		_depth = transform.position.z - targetCamera.transform.position.z;
	}

	bool CameraChanged()
	{
		if (targetCamera == null) return false;
		if (Mathf.Abs(targetCamera.aspect - _lastAspect) > 0.001f) return true;
		if (targetCamera.orthographic)
			return Mathf.Abs(targetCamera.orthographicSize - _lastOrtho) > 0.001f;
		return Mathf.Abs(targetCamera.fieldOfView - _lastFov) > 0.001f;
	}

	void OnDestroy()
	{
		if (_mat != null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) DestroyImmediate(_mat);
			else Destroy(_mat);
#else
            Destroy(_mat);
#endif
		}
	}
}
