// UIHUD.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 스크립트: HP/무기/점수/데미지 숫자 표시
/// </summary>
public class UIHUD : MonoBehaviour
{
    [Header("HP")]
    public Slider hpBar;       // 체력 바
    public TMP_Text hpValue;   // "73"

    [Header("Weapon")]
    public Image wIcon;        // 무기 아이콘
    public TMP_Text wName;     // 무기 이름
    public TMP_Text wAmmo;     // "─" 또는 "24"

    [Header("Score")]
    public TMP_Text wScore;    // 점수 표시 텍스트

    [Header("Damage Number")]
    public DamageNumberPool dmgPool; // 월드 데미지 숫자 풀

    void Start()
    {
        // 씬 시작 시 GameScore가 있다면 HUD를 바인딩하고 현재 점수 표시
        if (GameScore.I) GameScore.I.BindHUD(this);
    }

    // ===== HP 갱신 =====
    public void SetHP(int cur, int max)
    {
        if (hpBar)
        {
            hpBar.maxValue = max;
            hpBar.value = cur;
        }
        if (hpValue) hpValue.text = $"{cur}";
    }

    // ===== 무기 갱신 =====
    public void SetWeapon(Sprite icon, string name, int ammo, bool infinite)
    {
        if (wIcon) { wIcon.sprite = icon; wIcon.enabled = icon != null; }
        if (wName) wName.text = name ?? "";
        if (wAmmo) wAmmo.text = infinite ? "─" : ammo.ToString();
    }

    // ===== 데미지 숫자(월드좌표, 색) =====
    public void ShowDamage(Vector3 worldPos, int amount, Color color)
    {
        if (dmgPool) dmgPool.Spawn(worldPos, amount, color);
    }

    // ===== 점수 갱신(숫자 표시) =====
    public void SetScore(int score)
    {
        if (wScore) wScore.text = score.ToString(); // 단순 숫자 표기
    }
}
