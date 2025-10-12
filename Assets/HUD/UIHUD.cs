// UIHUD.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIHUD : MonoBehaviour
{
	// HP
	public Slider hpBar;          // 인스펙터 연결
	public TMP_Text hpValue;      // "73 / 100" 표시

	// 무기 슬롯
	[System.Serializable]
	public class WeaponSlotUI
	{
		public Image icon;        // 무기 아이콘
		public TMP_Text ammo;     // "∞" 또는 "24"
	}
	public WeaponSlotUI[] slots = new WeaponSlotUI[3]; // 0:권총, 1~2:보급무기

	// 우하
	public TMP_Text scoreText;
	public TMP_Text timeText;

	// 보스바
	public TMP_Text bossNameLeft;
	public Slider bossHpBar;
	public TMP_Text bossHpRight;



	// HP 갱신
	public void SetHP(int cur, int max)
	{
		hpBar.maxValue = max; hpBar.value = cur;
		hpValue.text = $"{cur} / {max}";          // 초보자용: 문자열 보간
	}

	// 무기 슬롯 갱신
	public void SetSlot(int idx, Sprite icon, int ammo, bool infinite)
	{
		slots[idx].icon.sprite = icon;
		slots[idx].icon.enabled = icon != null;
		slots[idx].ammo.text = infinite ? "∞" : ammo.ToString();
	}

	// 스코어/타임
	public void SetScore(int v) { scoreText.text = v.ToString(); }
	public void SetTime(float sec) { timeText.text = $"{sec:0.0}s"; }

	// 보스바
	public void SetBoss(string name, int cur, int max)
	{
		bossNameLeft.text = name;
		bossHpBar.maxValue = max; bossHpBar.value = cur;
		bossHpRight.text = $"{cur} / {max}";
	}
}
