using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// 모든 보스 공통 베이스(결합본)
/// - 기존: 체력/피해/HP UI 처리 유지
/// - 추가: 보스 이름, 보스바 연출(슬라이드/이징/흔들림), 색상 임계치(색·SFX·FX·BGM), BGM 교체 이벤트, 액터 바인딩
/// - 사용: 씬의 BossControl에 붙이고, BossSequenceController가 UI/액터를 주입한다.
/// </summary>
public class BossBase : MonoBehaviour, IDamageable
{
    // ─────────────────────────────────────────────────────────
    [Header("① 보스 스탯")]
    public int maxHP = 100;                 // 기본 체력(Inspector에서 설정)
    protected int currentHP;                // 현재 체력(내부 관리)
    public string bossName = "보스";         // 보스 이름(표시용)

    // ─────────────────────────────────────────────────────────
    [Header("② 보스 액터(프리팹 인스턴스 Transform)")]
    [Tooltip("스폰된 보스 모델의 Transform. BossSequenceController가 스폰 직후 BindActor로 설정")]
    public Transform actor;                 // 데미지 숫자 위치 기준점

    // ─────────────────────────────────────────────────────────
    [Header("③ 보스바 UI(Screen Space - Camera 캔버스 자식)")]
    public RectTransform bossBarRoot;       // 보스바 패널(RectTransform)
    public Slider hpSlider;                  // 체력 슬라이더
    public TextMeshProUGUI hpText;           // 숫자 영역(현재 HP만 표기)
    public Image hpFill;                      // 슬라이더 Fill(색 변경 대상)
    public TextMeshProUGUI nameTextTarget;    // 보스 이름 표기 대상

    // ─────────────────────────────────────────────────────────
    [Header("④ 보스바 연출(슬라이드/이징/흔들림)")]
    public float barAnimTime = 0.35f;                  // 슬라이드 시간
    public Vector2 barOnscreenPos = new Vector2(0,-40); // 화면 상단 기준 내부 위치
    public Vector2 barOffscreenPos = new Vector2(0,120); // 화면 위 바깥
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
    public float colorFadeTime = 0.25f;                // Fill 색 전환 시간
    public float shakeAmp = 6f;                        // 등장 순간 좌우 흔들림 픽셀
    public float shakeFreq = 30f;
    public bool  shakeOnAppear = true;
    public float shakeOnAppearTime = 0.2f;
    public AudioClip sfxAppear, sfxDisappear;          // 바 등장/퇴장 SFX
    public ParticleSystem fxAppear, fxDisappear;       // 바 등장/퇴장 FX
    public AudioSource audioSrc;                       // 보스 전용 오디오(없으면 생성)
    int _lastTier = -1;                 // 마지막으로 적용된 임계치 인덱스(-1=없음)
    AudioClip _lastBgmClip = null;      // 마지막으로 요청한 BGM
    public bool introInvincible = true;          // 충전 중 데미지 무시
    public bool introSuppressThreshold = true;   // 충전 중 임계치(색/BGM) 비활성
    bool _isChargingIntro = false;               // 내부 플래그

    // ─────────────────────────────────────────────────────────
    [Serializable]
    public struct HpColorThreshold
    {
        [Tooltip("이 값 '이하'가 되면 발동")]
        public int hpLessEqual;
        public Color color;         // 바 색
        public AudioClip sfx;       // 효과음(선택)
        public ParticleSystem fx;   // 이펙트(선택)
        public AudioClip bgmClip;   // BGM 교체(선택)
        public bool bgmLoop;        // BGM 루프
    }

    [Header("⑤ 색상 임계치(색·SFX·FX·BGM)")]
    public Color defaultColor = new Color(0.2f,0.9f,0.2f,1f);
    public HpColorThreshold[] thresholds = new HpColorThreshold[4];

    /// <summary>임계치 도달 시 BGM 교체 요청 이벤트(clip, loop)</summary>
    public event Action<AudioClip,bool> OnBgmSwapRequest;

    // ─────────────────────────────────────────────────────────
    [Header("⑥ 사망 처리")]
    public float deathDelay = 0f;           // 사망 지연 삭제 시간

    // 콜백(외부 구독 가능)
    public event Action<int,int> OnHpChanged;
    public event Action<BossBase> OnBossDie;

    // 내부
    DamageNumberPool _dmgPool;

    // ===== 공용 바인딩 API =====
    /// <summary>보스 액터(스폰된 프리팹 인스턴스) 바인딩</summary>
    public void BindActor(Transform t) { actor = t; }

    // ===== 수명 사이클 =====
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
        // 시작 시 체력 초기화
        currentHP = maxHP;

        // UI 초기
        if (nameTextTarget) nameTextTarget.text = bossName;
        UpdateUI();

        // 숫자 겹침 방지 기초 설정
        if (hpText)
        {
            hpText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            hpText.ForceMeshUpdate(true);
        }

        // 보스바는 처음에 숨김(Spawn 시 표시)
        if (bossBarRoot)
        {
            bossBarRoot.anchoredPosition = barOffscreenPos;
            bossBarRoot.gameObject.SetActive(false);
        }

        ApplyBarColor();
        _lastTier = -1;
        _lastBgmClip = null;

    }

    // ===== 외부 호출: 보스바 표시 + HP 충전 연출 =====
    /// <summary>보스바 슬라이드 인 + HP 0→최대 n초 충전</summary>
public void ShowBarWithCharge(float seconds)
{
    _isChargingIntro = true;                               // ★ 시작
    if (bossBarRoot)
    {
        bossBarRoot.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(BarSlide(true));
    }
    StopCoroutine(nameof(CoChargeHP));
    StartCoroutine(CoChargeHP(Mathf.Max(0.05f, seconds)));
}


    /// <summary>보스바 슬라이드 아웃</summary>
    public void HideBar()
    {
        if (!bossBarRoot) return;
        StopAllCoroutines();
        StartCoroutine(BarSlide(false));
    }

    // ===== 기존 공개 API 유지 =====
    public void SetUI(Slider slider, TextMeshProUGUI text) { hpSlider = slider; hpText = text; UpdateUI(); }

    public void InitHP(int newMaxHP, int? newCurrentHP = null, bool clamp = true)
    {
        maxHP = Mathf.Max(1, newMaxHP);
        currentHP = newCurrentHP.HasValue ? newCurrentHP.Value : maxHP;
        if (clamp) currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateUI();
        OnHpChanged?.Invoke(currentHP, maxHP);
    }

    /// <summary>
    /// IDamageable 구현: 어떤 탄/공격이든 여기로 수렴
    /// 약점은 "보너스 더하기" 방식(배수 아님)
    /// </summary>
    public void TakeDamage(int amount, bool weak, float weakBonus)
    {
        // 인트로
        if (_isChargingIntro && introInvincible) return;

        // 최종 데미지(약점=보너스 가산)
        int final = amount + (weak ? Mathf.RoundToInt(weakBonus) : 0);
        final = Mathf.Max(0, final);

        GameScore.I?.AddDamage(final);     // 🔹 점수 누적 (데미지 기반)

        // 체력 감소
        currentHP = Mathf.Max(0, currentHP - final);

        // UI 갱신
        UpdateUI();
        OnHpChanged?.Invoke(currentHP, maxHP);

        // 데미지 숫자
        if (_dmgPool)
        {
            Vector3 wp = (actor ? actor.position : transform.position) + Vector3.up * 0.6f;
            _dmgPool.Spawn(wp, final, weak ? Color.blue : Color.white);
        }

        // 색/임계치 처리 또는 사망
        if (currentHP == 0) Die();
        else ApplyBarColor();
    }

    /// <summary>사망 공통 처리(바 숨김 + FX/SFX + 액터 삭제)</summary>
    protected virtual void Die()
    {
        if (bossBarRoot) StartCoroutine(BarSlide(false));
        if (fxDisappear) Instantiate(fxDisappear, (actor ? actor.position : transform.position), Quaternion.identity);
        if (audioSrc && sfxDisappear) audioSrc.PlayOneShot(sfxDisappear);
        OnBossDie?.Invoke(this);
        if (actor) Destroy(actor.gameObject, Mathf.Max(0f, deathDelay));
    }

    /// <summary>HP UI 업데이트</summary>
    protected void UpdateUI()
    {
        if (hpSlider) { hpSlider.maxValue = maxHP; hpSlider.value = currentHP; }
        if (hpText)   { hpText.text = currentHP.ToString("D0"); hpText.ForceMeshUpdate(); } // "현재/최대" → 요구대로 최대 숨김
        if (nameTextTarget) nameTextTarget.text = bossName;
    }

    // 레거시 호환 getter
    public int GetCurrentHP() { return currentHP; }
    public int GetMaxHP()     { return maxHP; }
    public int CurrentHP => currentHP;
    public int MaxHP     => maxHP;
    public bool IsDead   => currentHP <= 0;

    // ── 내부 유틸 ──
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
        _isChargingIntro = false;                                   // 종료
        ApplyBarColor();                                            // 종료 시 한 번만 임계치 평가

    }

    void ApplyBarColor()
    {
        if (!hpFill) return;

        // 인트로 충전 중에는 임계치/색/BGM 계산을 건너뜀
        if (_isChargingIntro && introSuppressThreshold)
        {
            // 색은 기본색으로 유지하고 페이드만 적용
            StopCoroutine(nameof(CoFadeFill));
            StartCoroutine(CoFadeFill(hpFill.color, defaultColor, colorFadeTime));
            return;
        }

        int cur = currentHP;
        int tier = -1;
        Color target = defaultColor;

        // 작은 값(더 위험한 구간)일수록 우선되게 선택
        for (int i = 0; i < thresholds.Length; i++)
        {
            var th = thresholds[i];
            if (th.hpLessEqual <= 0) continue;
            if (cur <= th.hpLessEqual) { tier = i; target = th.color; } // 가장 마지막으로 맞는 i가 최저 HP 구간
        }

        // 색은 매번 페이드, 연출/BGM은 '단계 변경 시'만
        StopCoroutine(nameof(CoFadeFill));
        StartCoroutine(CoFadeFill(hpFill.color, target, colorFadeTime));

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
    }

    IEnumerator CoFadeFill(Color from, Color to, float t)
    {
        if (t <= 0f) { hpFill.color = to; yield break; }
        float e = 0f;
        while (e < t)
        {
            e += Time.unscaledDeltaTime;
            hpFill.color = Color.LerpUnclamped(from, to, e / t);
            yield return null;
        }
        hpFill.color = to;
    }

    IEnumerator BarSlide(bool show)
    {
        if (!bossBarRoot) yield break;

        Vector2 from = show ? barOffscreenPos : barOnscreenPos;
        Vector2 to   = show ? barOnscreenPos   : barOffscreenPos;

        float dur = Mathf.Max(0.05f, barAnimTime);
        float t = 0f;

        // SFX/FX
        if (audioSrc && (show ? sfxAppear : sfxDisappear))
            audioSrc.PlayOneShot(show ? sfxAppear : sfxDisappear);
        if (show && fxAppear)
            Instantiate(fxAppear, Camera.main ? Camera.main.transform.position : transform.position, Quaternion.identity);
        if (!show && fxDisappear)
            Instantiate(fxDisappear, (actor ? actor.position : transform.position), Quaternion.identity);

        float shakeLeft = (show && shakeOnAppear) ? shakeOnAppearTime : 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = ease != null ? ease.Evaluate(k) : k;

            Vector2 p = Vector2.LerpUnclamped(from, to, e);

            // 등장 순간 흔들림
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.unscaledDeltaTime;
                float wob = Mathf.Sin(Time.unscaledTime * shakeFreq) * shakeAmp *
                           (shakeLeft / Mathf.Max(0.0001f, shakeOnAppearTime));
                p.x += wob;
            }

            bossBarRoot.anchoredPosition = p;
            yield return null;
        }

        bossBarRoot.anchoredPosition = to;
        if (!show) bossBarRoot.gameObject.SetActive(false);
    }
}