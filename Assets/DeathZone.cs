using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Player 태그를 가진 오브젝트만 반응
        if (collision.CompareTag("Player"))
        {
            PlayerHealth player = collision.GetComponent<PlayerHealth>();
            if (player != null)
            {
                // 이미 죽은 상태면 중복 처리 방지
                if (player.currentHP > 0)
                {
                    Debug.Log("[DeathZone] Player fell into abyss");
                    player.currentHP = 0; // 체력 0
                    player.SendMessage("OnDead", SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }
}