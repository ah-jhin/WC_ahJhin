// DamageNumberPool.cs
using UnityEngine;

public class DamageNumberPool : MonoBehaviour
{
	public static DamageNumberPool I;           // 간단한 싱글톤
	public DamageNumber prefab;
	public int preload = 32;

	DamageNumber[] pool;
	int idx;

	void Awake()
	{
		I = this;
		pool = new DamageNumber[preload];
		for (int i = 0; i < preload; i++)
		{
			pool[i] = Instantiate(prefab, transform);
			pool[i].gameObject.SetActive(false);
		}
	}

	// 월드 좌표에 스폰
	public void Spawn(Vector3 worldPos, int dmg, Color c)
	{
		idx = (idx + 1) % pool.Length;
		var dn = pool[idx];
		dn.transform.position = worldPos;
		dn.gameObject.SetActive(true);
		dn.Show(dmg, c);
	}
}
