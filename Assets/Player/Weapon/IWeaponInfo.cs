using UnityEngine;

/// HUD와 매니저가 읽는 최소 정보
public interface IWeaponInfo
{
    string DisplayName { get; }     // 무기 이름
    Sprite Icon { get; }            // 무기 아이콘
    int Ammo { get; }               // 남은 탄(무한이면 0)
    bool IsInfinite { get; }        // 무한 탄 여부

    float FireInterval { get; }     // ★ 무기별 발사 간격(초)  ← 추가
}
