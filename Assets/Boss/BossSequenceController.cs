using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossSequenceController : MonoBehaviour
{
    [Header("① 보스 스폰")]
    public GameObject bossPrefab;
    public Vector3 bossWorldPos = new Vector3(8,2,0);

    [Header("② 보스바 UI(Screen Space - Camera 캔버스)")]
    public RectTransform bossBarRoot;
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Image hpFill;
    public TextMeshProUGUI nameText;

    [Header("③ 보스바 연출")]
    public float barAppearTime = 0.35f;
    public float chargeSeconds = 1.5f;
    public AudioClip sfxIntro;
    public ParticleSystem fxIntro;

    [Header("④ BGM")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;
    public bool loopBgm = true;
    bool spawned=false;
    BossBase boss;

    void Update()
    {
        if (!spawned && Input.GetKeyDown(KeyCode.Q)) SpawnBossOnce();
    }

    void SpawnBossOnce()
    {
        spawned=true;

        if (!bossPrefab) { Debug.LogError("[BossSeq] bossPrefab 미지정"); return; }
        var go = Instantiate(bossPrefab, bossWorldPos, Quaternion.identity);

        // 씬의 BossBase 찾기(= BossControl에 붙어 있음)
#if UNITY_2023_1_OR_NEWER
        boss = FindFirstObjectByType<BossBase>();
#else
        boss = FindObjectOfType<BossBase>();
#endif
        if (!boss) { Debug.LogError("[BossSeq] BossBase 없음"); return; }

        // 액터 바인딩
        boss.BindActor(go.transform);

        // UI 주입 + 연출 파라미터 동기화
        boss.bossBarRoot = bossBarRoot;
        boss.hpSlider = hpSlider;
        boss.hpText = hpText;
        boss.hpFill = hpFill;
        boss.nameTextTarget = nameText;
        boss.barAnimTime = barAppearTime;

        // 등장 연출
        boss.ShowBarWithCharge(chargeSeconds);
        if (sfxIntro) AudioSource.PlayClipAtPoint(sfxIntro, Camera.main?Camera.main.transform.position:transform.position, 1f);
        if (fxIntro) Instantiate(fxIntro, go.transform.position, Quaternion.identity);

        // BGM 시작
        StartOrSwapBgm(bgmClip, loopBgm);

        // 임계치 BGM 교체 감시
        boss.OnBgmSwapRequest += OnBgmSwapRequest;
        boss.OnBossDie += OnBossDie;
    }

    void StartOrSwapBgm(AudioClip clip, bool loop)
    {
        if (!clip) return;
        if (!bgmSource)
        {
            var src = new GameObject("BGM_Source").AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            bgmSource = src;
        }
        bgmSource.loop = loop;
        bgmSource.clip = clip;
        bgmSource.Play();
    }
    void OnBgmSwapRequest(AudioClip clip, bool loop)
    {
        StartOrSwapBgm(clip, loop);
    }
    void OnBossDie(BossBase b)
    {
        if (bgmSource && bgmSource.isPlaying) bgmSource.Stop();
        boss.OnBossDie -= OnBossDie;
    }
}
