// DamageNumber.cs
using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
	public TMP_Text label;           // 인스펙터로 연결
	public float riseSpeed = 1.5f;   // 위로 떠오르는 속도
	public float life = 0.6f;        // 표시 시간

	float _t;

	void OnEnable() { _t = 0f; }      // 재활용 시 타이머 리셋

	void Update()
	{
		_t += Time.deltaTime;
		transform.position += Vector3.up * riseSpeed * Time.deltaTime; // 위로 이동
		if (_t >= life) gameObject.SetActive(false);                   // 자동 비활성
	}

	// 숫자와 색 적용
	public void Show(int dmg, Color c)
	{
		label.text = dmg.ToString();
		label.color = c;
	}
}
