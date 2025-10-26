// UIHUD.cs
using UnityEngine; using UnityEngine.UI; using TMPro;

/// HUD 스크: 체력/무기/데미지 숫자 표시
public class UIHUD : MonoBehaviour
{
    [Header("HP")]
    public Slider hpBar;                 // 체력 바
    public TMP_Text hpValue;             // "73 / 100"

    [Header("Weapon")]
    public Image wIcon;                  // 무기 아이콘
    public TMP_Text wName;               // 무기 이름
    public TMP_Text wAmmo;               // "∞" 또는 "24"

    [Header("Damage Number")]
    public DamageNumberPool dmgPool;     // 데미지 숫자 풀(씬에 1개)

    // HP 갱신
    public void SetHP(int cur, int max){
        hpBar.maxValue = max; hpBar.value = cur;
        hpValue.text = $"{cur}"; //  / {max}
    }

    // 무기 갱신
    public void SetWeapon(Sprite icon, string name, int ammo, bool infinite){
        wIcon.sprite = icon; wIcon.enabled = icon!=null;
        wName.text = name ?? "";
        wAmmo.text = infinite ? "∞" : ammo.ToString();
    }

    // 데미지 숫자(월드좌표, 색)
    public void ShowDamage(Vector3 worldPos, int amount, Color color){
        if (dmgPool) dmgPool.Spawn(worldPos, amount, color);
    }
}
