// Assets/Scenes/Sc/우라노스/Ouranos_SniperReticle.cs
// 역할: 조준경 1개만 사용, 타깃 추적. 부스트 시간 동안에는 직진 추적만 사용하고,
//       부스트 종료 후에만 SmoothDamp(부드러운 추적)를 활성화한다.
using UnityEngine;

namespace Ouranos_Boss
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SpriteRenderer))]
	public class Ouranos_SniperReticle : MonoBehaviour
	{
		[Header("표시")]
		[Tooltip("색상 변경 대상 SpriteRenderer")]
		public SpriteRenderer sr;

		// ▼ 패턴(페이즈)에서 런타임 주입하는 추적 파라미터. 인스펙터에 노출하지 않음.
		[HideInInspector] public bool Ouranos_useInitialBoost = true;   // 부스트 사용 여부
		[HideInInspector] public float Ouranos_initialBoostSeconds = 1f;   // 부스트 지속 시간(초)
		[HideInInspector] public float Ouranos_initialBoostSpeed = 100f;   // 부스트 속도
		[HideInInspector] public float Ouranos_followSpeed = 15f;    // 일반 추적 속도
		[HideInInspector] public bool Ouranos_useSmoothDamp = false;  // 부스트 종료 후 스무딩 사용 여부
		[HideInInspector] public float Ouranos_smoothTime = 0.02f;  // 스무딩 시간 상수(초)
		[HideInInspector] public float Ouranos_stopDistance = 0.05f;  // 목표와 최소 거리

		Transform _target;                // 추적 대상(플레이어)
		Vector3 _vel;                   // SmoothDamp 내부 속도
		float _boostTimer;            // 남은 부스트 시간
		bool _frozen;                // 잠금(정지) 여부

		void Awake()
		{
			if (!sr) sr = GetComponent<SpriteRenderer>();

			// ★ 프리팹 중복 장착 방지: 같은 컴포넌트가 2개 이상이면 정리
			var dups = GetComponents<Ouranos_SniperReticle>();
			if (dups.Length > 1)
			{
				for (int i = 1; i < dups.Length; i++)
					DestroyImmediate(dups[i]); // 자신만 남기고 제거
				Debug.LogWarning("[Reticle] 중복 구성요소를 정리했다. 프리팹에는 1개만 남겨라.", this);
			}
		}

		// ===== 패턴에서 호출하는 API =====
		/// <summary>타깃(플레이어 Transform) 지정</summary>
		public void SetTarget(Transform t) { _target = t; }

		/// <summary>조준경 색상 지정</summary>
		public void SetColor(Color c) { if (sr) sr.color = c; }

		/// <summary>잠금(정지) 시작</summary>
		public void FreezeOn() { _frozen = true; _vel = Vector3.zero; }

		/// <summary>잠금 해제</summary>
		public void FreezeOff() { _frozen = false; }

		/// <summary>즉시 제거</summary>
		public void KillNow() { Destroy(gameObject); }

		void OnEnable()
		{
			// 부스트 타이머 설정. 부스트 시간 동안에는 SmoothDamp를 절대 쓰지 않는다.
			_boostTimer = Ouranos_useInitialBoost ? Mathf.Max(0f, Ouranos_initialBoostSeconds) : 0f;
		}

		void Update()
		{
			if (_frozen || !_target) return;

			Vector3 pos = transform.position;
			Vector3 tpos = _target.position;
			Vector3 to = tpos - pos;
			float dist = to.magnitude;

			// 도달 임계치(0도 허용) 보정
			float arrive = Mathf.Max(0f, Ouranos_stopDistance);
			if (dist <= arrive) return;

			// 현재 프레임 모드 결정
			bool inBoost = (_boostTimer > 0f);                 // 부스트 중?
			float speed = inBoost ? Ouranos_initialBoostSpeed
								   : Ouranos_followSpeed;

			// 목표점(정지거리만큼 앞에서 멈춤)
			Vector3 desired = tpos - to.normalized * arrive;

			// 1) 기본 이동: 직진
			Vector3 next = Vector3.MoveTowards(pos, desired, speed * Time.deltaTime);

			// 2) 스무딩 적용 조건: 부스트 종료 '후' + 옵션 On
			bool smoothingActive = (!inBoost) && Ouranos_useSmoothDamp;
			if (smoothingActive)
				next = Vector3.SmoothDamp(pos, next, ref _vel, Mathf.Max(0f, Ouranos_smoothTime));

			transform.position = next;

			// 타이머 감소는 마지막에 (이번 프레임은 inBoost 판정 그대로 유지)
			if (_boostTimer > 0f)
			{
				_boostTimer -= Time.deltaTime;
				if (_boostTimer < 0f) _boostTimer = 0f;        // 음수 클램프
			}
		}
	}
}
