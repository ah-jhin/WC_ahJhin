using UnityEngine;

/// UGUI 데미지 숫자 풀(월드 스페이스 Canvas 자식에 둔다)
public class DamageNumberPool : MonoBehaviour
{
    [Header("프리팹")] 
    public DamageNumberUI prefab;   // ← 반드시 DamageNumberUI 타입이어야 함
    [Header("미리 만들 개수")]
    public int preload = 32;

    private DamageNumberUI[] pool;
    private int idx = -1;
    private Canvas canvas; 
    private Camera cam;

    void Awake()
    {
        // ★ 부모 Canvas 확보(월드 스페이스 캔버스여야 함)
        canvas = GetComponentInParent<Canvas>();
        cam = Camera.main;

        if (!prefab) { Debug.LogError("[DmgPool] prefab 미지정"); return; }
        if (!canvas) { Debug.LogError("[DmgPool] 부모 Canvas 없음"); return; }

        // ★ 풀 미리 생성(Instantiate는 여기서만)
        pool = new DamageNumberUI[Mathf.Max(1, preload)];
        for (int i = 0; i < pool.Length; i++)
        {
            var dn = Instantiate(prefab, transform); // 부모=dmgPool
            dn.gameObject.SetActive(false);
            pool[i] = dn;
        }
    }

    /// <summary>월드 좌표에 데미지 숫자 표시</summary>
    public void Spawn(Vector3 worldPos, int dmg, Color col)
    {
        if (pool == null) return;

        // 월드→스크린→Canvas 로컬
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        var rtCanvas = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rtCanvas, screen, cam, out var local);

        idx = (idx + 1) % pool.Length;
        var dn = pool[idx];
        var rt = dn.GetComponent<RectTransform>();
        rt.anchoredPosition = local;      // 위치 지정
        dn.gameObject.SetActive(true);		// 먼저 키기
        dn.Show(dmg, col);                 // 텍스트/색·수명 재생
    }
}
