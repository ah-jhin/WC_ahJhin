using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// BossBase: 보스바 색 연출(변화시간, 그라데이션, 깜빡임) + 흔들림 제거판
/// - 새 영역 생성 없음. ⑤ 섹션(기본색)과 Threshold 구조체 내부에만 옵션 추가/사용.
/// - 검정색 초기화 방지: _currentFillColor 를 소스 오브 트루스로 사용.
/// </summary>
public class BossBase : MonoBehaviour, IDamageable
{
	// ─────────────────────────────────────────────────────────
	[Header("① 보스 스탯")]
	public int maxHP = 100;
	protected int currentHP;
	public string bossName = "보스";

	// ─────────────────────────────────────────────────────────
	[Header("② 보스 액터(프리팹 인스턴스 Transform)")]
	[Tooltip("스폰된 보스 모델의 Transform. BossSequenceController가 스폰 직후 BindActor로 설정")]
	public Transform actor;                 // 데미지 숫자 위치 기준점

	// ─────────────────────────────────────────────────────────
	[Header("③ 보스바 UI(Screen Space - Camera 캔버스 자식)")]
	public RectTransform bossBarRoot;       // 보스바 패널(RectTransform)
	public Slider hpSlider;                 // 체력 슬라이더
	public TextMeshProUGUI hpText;          // 숫자 영역(현재 HP만 표기)
	public Image hpFill;                    // 슬라이더 Fill(색 변경 대상)
	public TextMeshProUGUI nameTextTarget;  // 보스 이름 표기 대상

	// ─────────────────────────────────────────────────────────
	[Header("④ 보스바 연출(슬라이드/이징)")]
	public float barAnimTime = 0.35f;                   // 슬라이드 시간
	public Vector2 barOnscreenPos = new Vector2(0, -40);// 화면 상단 기준 내부 위치
	public Vector2 barOffscreenPos = new Vector2(0, 120);// 화면 위 바깥
	public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
	public float colorFadeTime = 0.25f;                 // 기본 색 전환 시간
	public AudioClip sfxAppear, sfxDisappear;           // 바 등장/퇴장 SFX
	public ParticleSystem fxAppear, fxDisappear;        // 바 등장/퇴장 FX
	public AudioSource audioSrc;                        // 보스 전용 오디오(없으면 생성)
	int _lastTier = -1;                                 // 마지막 임계치 인덱스
	AudioClip _lastBgmClip = null;                      // 마지막 요청한 BGM
	public bool introInvincible = true;                 // 인트로 무적
	public bool introSuppressThreshold = true;          // 인트로 동안 임계(색/BGM) 억제
	bool _isChargingIntro = false;
	public bool IsIntroNoScore => _isChargingIntro && introInvincible;

	// ▼ Fill 현재색. 코루틴 간 일관성 유지 + 검정 초기화 방지
	Color _currentFillColor;

	// ─────────────────────────────────────────────────────────
	[Serializable]
	public struct HpColorThreshold
	{
		[Tooltip("이 값 '이하'가 되면 발동")]
		public int hpLessEqual;

		[Header("기본")]
		public Color color;         // 바 색
		public AudioClip sfx;       // 효과음(선택)
		public ParticleSystem fx;   // 이펙트(선택)
		public AudioClip bgmClip;   // BGM 교체(선택)
		public bool bgmLoop;

		[Header("색 연출(임계 구간)")]
		[Tooltip("현재 색 → 임계색으로 바뀌는 시간(초). 0이면 즉시")]
		public float transitionTime;

		[Tooltip("그라데이션 사용")]
		public bool useGradient;
		[Tooltip("그라데이션 색상 리스트(2개 이상 권장)")]
		public Color[] gradientColors;
		[Tooltip("그라데이션 한 바퀴 시간(초)")]
		public float gradientCycleSeconds;

		[Tooltip("깜빡임 사용(그라데이션과 동시에 켜면 그라데이션 우선)")]
		public bool useBlink;
		[Tooltip("깜빡임 색상 리스트(1개 이상)")]
		public Color[] blinkColors;
		[Tooltip("깜빡임 전환 간격(초)")]
		public float blinkInterval;
	}

	[Header("⑤ 색상 임계치(색·SFX·FX·BGM)")]
	public Color defaultColor = new Color(0.2f, 0.9f, 0.2f, 1f);

	// ▼ ‘초반 보스바’ 색 연출 옵션(같은 ⑤ 섹션 내부)
	[Tooltip("기본색으로 변환하는 시간. 0이면 즉시 변경")]
	public float defaultTransitionTime = 0.25f;

	[Tooltip("기본 상태: 그라데이션 사용")]
	public bool defaultUseGradient = false;
	[Tooltip("기본 상태 그라데이션 색상 리스트(2개 이상 권장)")]
	public Color[] defaultGradientColors;
	[Tooltip("기본 상태 그라데이션 한 바퀴 시간(초)")]
	public float defaultGradientCycleSeconds = 2.0f;

	[Tooltip("기본 상태: 깜빡임 사용(그라데이션 우선)")]
	public bool defaultUseBlink = false;
	[Tooltip("기본 상태 깜빡임 색상 리스트(1개 이상)")]
	public Color[] defaultBlinkColors;
	[Tooltip("기본 상태 깜빡임 간격(초)")]
	public float defaultBlinkInterval = 0.12f;

	public HpColorThreshold[] thresholds = new HpColorThreshold[4];

	/// <summary>임계치 도달 시 BGM 교체 요청(clip, loop)</summary>
	public event Action<AudioClip, bool> OnBgmSwapRequest;

	// ─────────────────────────────────────────────────────────
	[Header("⑥ 사망 처리")]
	public float deathDelay = 0f;

	// 배경/카메라 HP 임계 연출(원본 유지)
	[System.Serializable] public struct Tisiphone_BgTween { public BackgroundUVScroller target; public Vector2 uvSpeed; public float rotationSpeed; public float lerpTime; public float holdTime; public bool revert; public AnimationCurve ease; }
	[System.Serializable] public struct Tisiphone_CamFx { public CameraEffects cam; public bool shake; public float shakeDuration, shakeAmplitude, shakeFrequency; public bool zoom; public float zoomSize, zoomTime; public bool rotate; public float rotateZ, rotateTime; public bool autoReset; public float autoResetDelay, resetEaseTime; }
	[System.Serializable] public class HpFxEvent { public int hpLessEqual = 50; public Tisiphone_BgTween[] backgrounds; public Tisiphone_CamFx[] cameras; public bool fireOnce = true; [NonSerialized] public bool _fired; }
	[Header("⑦ HP 임계 배경/카메라 연출")]
	public HpFxEvent[] hpFxEvents;

	// 콜백
	public event Action<int, int> OnHpChanged;
	public event Action<BossBase> OnBossDie;

	DamageNumberPool _dmgPool;

	// ===== 공용 바인딩 API =====
	public void BindActor(Transform t) { actor = t; }

	void Awake()
	{
		if (!audioSrc)
		{
			audioSrc = gameObject.AddComponent<AudioSource>();
			audioSrc.playOnAwake = false;
		}
#if UNITY_2023_1_OR_NEWER
		_dmgPool = FindFirstObjectByType<DamageNumberPool>();
#else
#pragma warning disable CS0618
        _dmgPool = FindObjectOfType<DamageNumberPool>();
#pragma warning restore CS0618
#endif
	}

	protected virtual void Start()
	{
		currentHP = maxHP;

		// hpFill 자동 배선 보정
		if (!hpFill && hpSlider && hpSlider.fillRect)
			hpFill = hpSlider.fillRect.GetComponent<Image>();

		if (nameTextTarget) nameTextTarget.text = bossName;
		UpdateUI();

		// 텍스트 겹침 방지
		if (hpText) { hpText.textWrappingMode = TMPro.TextWrappingModes.NoWrap; hpText.ForceMeshUpdate(true); }

		// 처음엔 숨김
		if (bossBarRoot) { bossBarRoot.anchoredPosition = barOffscreenPos; bossBarRoot.gameObject.SetActive(false); }

		// 시작 색을 명시적으로 지정 → 검정 초기화 차단
		_currentFillColor = defaultColor;
		if (hpFill) hpFill.color = defaultColor;

		ApplyBarColor();
		_lastTier = -1;
		_lastBgmClip = null;
	}

	// ===== 외부 호출: 보스바 표시 + HP 0→최대 충전 =====
	public void ShowBarWithCharge(float seconds)
	{
		_isChargingIntro = true;
		if (bossBarRoot)
		{
			bossBarRoot.gameObject.SetActive(true);
			StopAllCoroutines();
			StartCoroutine(BarSlide(true));
		}
		StopCoroutine(nameof(CoChargeHP));
		StartCoroutine(CoChargeHP(Mathf.Max(0.05f, seconds)));
	}

	public void HideBar()
	{
		StopColorFx();
		if (!bossBarRoot) return;
		StopAllCoroutines();
		StartCoroutine(BarSlide(false));
	}

	public void SetUI(Slider slider, TextMeshProUGUI text) { hpSlider = slider; hpText = text; UpdateUI(); }

	public void InitHP(int newMaxHP, int? newCurrentHP = null, bool clamp = true)
	{
		maxHP = Mathf.Max(1, newMaxHP);
		currentHP = newCurrentHP.HasValue ? newCurrentHP.Value : maxHP;
		if (clamp) currentHP = Mathf.Clamp(currentHP, 0, maxHP);
		UpdateUI();
		OnHpChanged?.Invoke(currentHP, maxHP);
	}

	public void TakeDamage(int amount, bool weak, float weakBonus)
	{
		if (_isChargingIntro && introInvincible) return;

		int final = amount + (weak ? Mathf.RoundToInt(weakBonus) : 0);
		final = Mathf.Max(0, final);

		GameScore.I?.AddDamage(final);

		currentHP = Mathf.Max(0, currentHP - final);

		UpdateUI();
		OnHpChanged?.Invoke(currentHP, maxHP);

		if (_dmgPool)
		{
			Vector3 wp = (actor ? actor.position : transform.position) + Vector3.up * 0.6f;
			_dmgPool.Spawn(wp, final, weak ? Color.blue : Color.white);
		}

		if (currentHP == 0) Die();
		else { ApplyBarColor(); EvaluateHpFxEvents(); }
	}

	protected virtual void Die()
	{
		StopColorFx();
		if (bossBarRoot) StartCoroutine(BarSlide(false));
		if (fxDisappear) Instantiate(fxDisappear, (actor ? actor.position : transform.position), Quaternion.identity);
		if (audioSrc && sfxDisappear) audioSrc.PlayOneShot(sfxDisappear);
		OnBossDie?.Invoke(this);
		if (actor) Destroy(actor.gameObject, Mathf.Max(0f, deathDelay));
	}

	protected void UpdateUI()
	{
		if (hpSlider) { hpSlider.maxValue = maxHP; hpSlider.value = currentHP; }
		if (hpText) { hpText.text = currentHP.ToString("D0"); hpText.ForceMeshUpdate(); }
		if (nameTextTarget) nameTextTarget.text = bossName;
	}

	public int GetCurrentHP() { return currentHP; }
	public int GetMaxHP() { return maxHP; }
	public int CurrentHP => currentHP;
	public int MaxHP => maxHP;
	public bool IsDead => currentHP <= 0;

	IEnumerator CoChargeHP(float dur)
	{
		int from = 0, to = maxHP;
		currentHP = from; UpdateUI();
		float t = 0f;
		while (t < dur)
		{
			t += Time.unscaledDeltaTime;
			currentHP = Mathf.RoundToInt(Mathf.Lerp(from, to, t / dur));
			UpdateUI();
			yield return null;
		}
		currentHP = to; UpdateUI();
		_isChargingIntro = false;
		ApplyBarColor();        // 종료 시 1회 임계 평가
		EvaluateHpFxEvents();
	}

	void ApplyBarColor()
	{
		if (!hpFill) return;

		// 인트로 충전 중엔 임계 억제 옵션
		if (_isChargingIntro && introSuppressThreshold)
		{
			StopColorFx();
			float tt = (defaultTransitionTime > 0f) ? defaultTransitionTime : colorFadeTime;
			StopCoroutine(nameof(CoFadeFill));
			StartCoroutine(CoFadeFill(_currentFillColor, defaultColor, tt));
			return;
		}

		int cur = currentHP;
		int tier = -1;
		Color target = defaultColor;

		// 낮은 HP 임계가 우선되도록 선택
		for (int i = 0; i < thresholds.Length; i++)
		{
			var th = thresholds[i];
			if (th.hpLessEqual <= 0) continue;
			if (cur <= th.hpLessEqual) { tier = i; target = th.color; }
		}

		// 변화시간: 임계 지정 > 기본 지정 > colorFadeTime
		float trans = colorFadeTime;
		if (tier >= 0 && thresholds[tier].transitionTime > 0f) trans = thresholds[tier].transitionTime;
		else if (defaultTransitionTime > 0f) trans = defaultTransitionTime;

		StopCoroutine(nameof(CoFadeFill));
		StartCoroutine(CoFadeFill(_currentFillColor, target, trans));

		// 임계 변경 시 1회성 효과
		if (tier != _lastTier)
		{
			if (tier >= 0)
			{
				var th = thresholds[tier];
				if (audioSrc && th.sfx) audioSrc.PlayOneShot(th.sfx);
				if (th.fx) Instantiate(th.fx, (actor ? actor.position : transform.position), Quaternion.identity);
				if (th.bgmClip && th.bgmClip != _lastBgmClip)
				{
					OnBgmSwapRequest?.Invoke(th.bgmClip, th.bgmLoop);
					_lastBgmClip = th.bgmClip;
				}
			}
			_lastTier = tier;
		}

		// 상시 색 연출(그라데이션 우선 → 깜빡임)
		if (tier >= 0) StartColorFx_FromThreshold(thresholds[tier]);
		else StartColorFx_Default();
	}

	// ───────── 색 연출 보조 ─────────
	Coroutine _coGradient, _coBlink;

	void StartColorFx_Default()
	{
		StopColorFx();

		if (defaultUseGradient && IsValidGradient(defaultGradientColors, defaultGradientCycleSeconds))
			_coGradient = StartCoroutine(CoGradient(defaultGradientColors, defaultGradientCycleSeconds));
		else if (defaultUseBlink && IsValidBlink(defaultBlinkColors, defaultBlinkInterval))
			_coBlink = StartCoroutine(CoBlink(defaultBlinkColors, defaultBlinkInterval));
	}

	void StartColorFx_FromThreshold(HpColorThreshold th)
	{
		StopColorFx();

		if (th.useGradient && IsValidGradient(th.gradientColors, th.gradientCycleSeconds))
			_coGradient = StartCoroutine(CoGradient(th.gradientColors, th.gradientCycleSeconds));
		else if (th.useBlink && IsValidBlink(th.blinkColors, th.blinkInterval))
			_coBlink = StartCoroutine(CoBlink(th.blinkColors, th.blinkInterval));
	}

	bool IsValidGradient(Color[] arr, float loopSeconds)
	{
		if (arr == null || arr.Length < 2) return false;
		if (loopSeconds <= 0f) return false;

		// 전부 (0,0,0,0)인 배열은 무시 → 검정으로 끌리는 현상 방지
		int meaningful = 0;
		for (int i = 0; i < arr.Length; i++)
			if (arr[i].a > 0f || arr[i].r > 0f || arr[i].g > 0f || arr[i].b > 0f)
				meaningful++;
		return meaningful >= 2;
	}

	bool IsValidBlink(Color[] arr, float interval)
	{
		if (arr == null || arr.Length < 1) return false;
		if (interval <= 0f) return false;
		return true;
	}

	// 부드러운 색 순환(그라데이션)
	IEnumerator CoGradient(Color[] arr, float secondsPerLoop)
	{
		int n = arr.Length;
		float seg = Mathf.Max(0.0001f, secondsPerLoop / n);

		int i = 0;
		while (true)
		{
			Color a = arr[i];
			Color b = arr[(i + 1) % n];
			float t = 0f;
			while (t < seg)
			{
				t += Time.unscaledDeltaTime;
				float k = Mathf.Clamp01(t / seg);
				_currentFillColor = Color.LerpUnclamped(a, b, k); // ★ 현재색 갱신
				if (hpFill) hpFill.color = _currentFillColor;
				yield return null;
			}
			i = (i + 1) % n;
		}
	}

	// 단계 즉시 전환(깜빡임)
	IEnumerator CoBlink(Color[] arr, float interval)
	{
		int i = 0;
		float wait = Mathf.Max(0.0001f, interval);
		while (true)
		{
			_currentFillColor = arr[i];      // ★ 현재색 갱신
			if (hpFill) hpFill.color = _currentFillColor;
			i = (i + 1) % arr.Length;
			yield return new WaitForSecondsRealtime(wait);
		}
	}

	void StopColorFx()
	{
		if (_coGradient != null) { StopCoroutine(_coGradient); _coGradient = null; }
		if (_coBlink != null) { StopCoroutine(_coBlink); _coBlink = null; }
	}

	IEnumerator CoFadeFill(Color from, Color to, float t)
	{
		if (!hpFill) yield break;

		// NaN/검정 방지: 시작색이 비어 있으면 현재값 또는 defaultColor로 보정
		if (from == default(Color))
			from = (_currentFillColor == default(Color)) ? defaultColor : _currentFillColor;

		if (t <= 0f) { _currentFillColor = to; hpFill.color = to; yield break; }
		float e = 0f;
		while (e < t)
		{
			e += Time.unscaledDeltaTime;
			_currentFillColor = Color.LerpUnclamped(from, to, e / t);
			hpFill.color = _currentFillColor;
			yield return null;
		}
		_currentFillColor = to;
		hpFill.color = to;
	}

	IEnumerator BarSlide(bool show)
	{
		StopColorFx();
		if (!bossBarRoot) yield break;

		Vector2 from = show ? barOffscreenPos : barOnscreenPos;
		Vector2 to = show ? barOnscreenPos : barOffscreenPos;

		float dur = Mathf.Max(0.05f, barAnimTime);
		float t = 0f;

		// SFX/FX
		if (audioSrc && (show ? sfxAppear : sfxDisappear))
			audioSrc.PlayOneShot(show ? sfxAppear : sfxDisappear);
		if (show && fxAppear)
			Instantiate(fxAppear, Camera.main ? Camera.main.transform.position : transform.position, Quaternion.identity);
		if (!show && fxDisappear)
			Instantiate(fxDisappear, (actor ? actor.position : transform.position), Quaternion.identity);

		while (t < dur)
		{
			t += Time.unscaledDeltaTime;
			float k = Mathf.Clamp01(t / dur);
			float e = ease != null ? ease.Evaluate(k) : k;
			bossBarRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
			yield return null;
		}

		bossBarRoot.anchoredPosition = to;
		if (!show) bossBarRoot.gameObject.SetActive(false);
	}

	// ───────── 배경/카메라 HP 임계 연출(원본 유지) ─────────
	System.Collections.IEnumerator CoApplyBg(Tisiphone_BgTween t)
	{
		if (!t.target) yield break;
		Vector2 fromSpeed = t.target.uvSpeed;
		float fromRot = t.target.rotationSpeed;

		float dur = Mathf.Max(0f, t.lerpTime);
		if (dur <= 0f)
		{
			t.target.uvSpeed = t.uvSpeed;
			t.target.rotationSpeed = t.rotationSpeed;
		}
		else
		{
			float e = 0f;
			while (e < dur)
			{
				e += Time.unscaledDeltaTime;
				float k = Mathf.Clamp01(e / dur);
				if (t.ease != null) k = t.ease.Evaluate(k);
				t.target.uvSpeed = Vector2.LerpUnclamped(fromSpeed, t.uvSpeed, k);
				t.target.rotationSpeed = Mathf.LerpUnclamped(fromRot, t.rotationSpeed, k);
				yield return null;
			}
			t.target.uvSpeed = t.uvSpeed;
			t.target.rotationSpeed = t.rotationSpeed;
		}
		if (t.holdTime > 0f) yield return new WaitForSecondsRealtime(t.holdTime);
		if (t.revert)
		{
			float r = Mathf.Max(0f, t.lerpTime);
			if (r <= 0f)
			{ t.target.uvSpeed = fromSpeed; t.target.rotationSpeed = fromRot; }
			else
			{
				float e = 0f;
				while (e < r)
				{
					e += Time.unscaledDeltaTime;
					float k = Mathf.Clamp01(e / r);
					if (t.ease != null) k = t.ease.Evaluate(k);
					t.target.uvSpeed = Vector2.LerpUnclamped(t.uvSpeed, fromSpeed, k);
					t.target.rotationSpeed = Mathf.LerpUnclamped(t.rotationSpeed, fromRot, k);
					yield return null;
				}
				t.target.uvSpeed = fromSpeed; t.target.rotationSpeed = fromRot;
			}
		}
	}

	CameraEffects GetCam(CameraEffects prefer)
	{
#if UNITY_2023_1_OR_NEWER
		return prefer ? prefer : UnityEngine.Object.FindFirstObjectByType<CameraEffects>(FindObjectsInactive.Include);
#else
#pragma warning disable CS0618
        return prefer ? prefer : UnityEngine.Object.FindObjectOfType<CameraEffects>();
#pragma warning restore CS0618
#endif
	}
	void PlayCamFx(Tisiphone_CamFx fx)
	{
		var cam = GetCam(fx.cam);
		if (!cam) return;
		if (fx.shake) cam.Shake(fx.shakeDuration, fx.shakeAmplitude, fx.shakeFrequency);
		if (fx.zoom) cam.ZoomTo(fx.zoomSize, fx.zoomTime);
		if (fx.rotate) cam.RotateTo(fx.rotateZ, fx.rotateTime);
		if (fx.autoReset) StartCoroutine(CoCamAutoReset(cam, fx.autoResetDelay, fx.resetEaseTime));
	}
	System.Collections.IEnumerator CoCamAutoReset(CameraEffects cam, float delay, float ease)
	{
		if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
		cam.ResetAll(ease);
	}

	void EvaluateHpFxEvents()
	{
		if (hpFxEvents == null || hpFxEvents.Length == 0) return;
		if (_isChargingIntro && introSuppressThreshold) return;

		for (int i = 0; i < hpFxEvents.Length; i++)
		{
			var e = hpFxEvents[i];
			if (e == null) continue;
			if (CurrentHP <= e.hpLessEqual)
			{
				if (e.fireOnce && e._fired) continue;
				if (e.backgrounds != null) foreach (var t in e.backgrounds) StartCoroutine(CoApplyBg(t));
				if (e.cameras != null) foreach (var c in e.cameras) PlayCamFx(c);
				if (e.fireOnce) e._fired = true;
			}
		}
	}
}

