// Assets/_UI/DamageNumberPool.cs  (경로는 네 프로젝트에 맞게 유지)
using UnityEngine;

/// <summary>
/// 월드 스페이스 Canvas 하위에 두는 데미지 숫자 풀
/// - 색상 프리셋(공격/약점/피격/회복)
/// - WorldPos, Collider2D, Transform 기준 스폰 API 제공
/// </summary>
public class DamageNumberPool : MonoBehaviour
{
	[Header("프리팹")]
	public DamageNumberUI prefab;              // 숫자 1개를 그리는 컴포넌트

	[Header("풀 크기")]
	public int preload = 32;

	[Header("색상 프리셋")]
	public Color colAttack = new Color(1f, 0.95f, 0.3f);   // 일반 공격
	public Color colWeak = new Color(1f, 0.4f, 0.4f);    // 약점 적중
	public Color colHit = new Color(0.8f, 0.8f, 1f);    // 내가 피해
	public Color colHeal = new Color(0.4f, 1f, 0.4f);    // 회복

	public static DamageNumberPool I { get; private set; } // 싱글톤

	DamageNumberUI[] _pool;
	int _idx = -1;
	Canvas _canvas;
	Camera _cam;

	void Awake()
	{
		I = this;
		_canvas = GetComponentInParent<Canvas>(); // 반드시 World Space
		_cam = Camera.main;

		if (!prefab) { Debug.LogError("[DmgPool] prefab 미지정"); return; }
		if (!_canvas) { Debug.LogError("[DmgPool] 부모 Canvas 없음"); return; }

		_pool = new DamageNumberUI[Mathf.Max(1, preload)];
		for (int i = 0; i < _pool.Length; i++)
		{
			var dn = Instantiate(prefab, transform);
			dn.gameObject.SetActive(false);
			_pool[i] = dn;
		}
	}

	// ===== 외부 API =====

	/// <summary>공격 숫자(월드 좌표)</summary>
	public void ShowAttack(Vector3 worldPos, int dmg, bool isWeak = false)
		=> Spawn(worldPos, dmg, isWeak ? colWeak : colAttack);

	public void ShowAttack(Collider2D hitCol, Vector3 fromPos, int dmg, bool isWeak = false)
	{
		var p = hitCol ? (Vector3)hitCol.ClosestPoint(fromPos) : fromPos;
		ShowAttack(p, dmg, isWeak);
	}

	public void ShowAttack(Transform t, int dmg, bool isWeak = false)
		=> ShowAttack(t ? t.position : Vector3.zero, dmg, isWeak);

	public void ShowHit(Vector3 worldPos, int dmg)
		=> Spawn(worldPos, dmg, colHit);

	public void ShowHeal(Vector3 worldPos, int amount)
		=> Spawn(worldPos, amount, colHeal);

	// ===== 내부 공통 스폰 =====

	/// <summary>월드 좌표에 데미지/회복 숫자 생성</summary>
	public void Spawn(Vector3 worldPos, int value, Color col)
	{
		if (_pool == null) return;

		Vector2 screen = RectTransformUtility.WorldToScreenPoint(_cam, worldPos);
		var rtCanvas = _canvas.transform as RectTransform;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(rtCanvas, screen, _cam, out var local);

		_idx = (_idx + 1) % _pool.Length;
		var dn = _pool[_idx];
		var rt = dn.GetComponent<RectTransform>();
		rt.anchoredPosition = local;
		dn.gameObject.SetActive(true);
		dn.Show(value, col);
	}

}
