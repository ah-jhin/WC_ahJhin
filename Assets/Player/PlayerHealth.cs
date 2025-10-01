using UnityEngine;
using UnityEngine.SceneManagement;   // �� �� �ٽ� �ε��

public class PlayerHealth : MonoBehaviour
{
    [Header("HP")]
    public int maxHP = 100;
    public int currentHP;

    [Header("�ǰ� ����/����")]
    public float invincibleTime = 0.6f;
    private float lastHitTime = -999f;
    public SpriteRenderer sr;

    [Header("Death")]
    public float restartDelay = 1.5f;   // �װ� ���� ����۱��� ������
    private bool isDead = false;

    void Awake()
    {
        currentHP = maxHP;
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void TakeDamage(int dmg)
    {
        if (isDead) return;                                 // �̹� �׾����� ����
        if (Time.time - lastHitTime < invincibleTime) return;

        lastHitTime = Time.time;
        currentHP = Mathf.Max(0, currentHP - Mathf.Max(0, dmg));

        if (sr) StartCoroutine(Blink());

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        currentHP = Mathf.Min(maxHP, currentHP + Mathf.Max(0, amount));
    }

    System.Collections.IEnumerator Blink()
    {
        float end = Time.time + 0.25f;
        while (Time.time < end && !isDead)
        {
            if (sr) sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.05f);
        }
        if (sr) sr.enabled = true;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Player Dead");

        // ������/���� ��Ȱ��ȭ
        var move = GetComponent<PlayerMovement>();
        if (move) move.enabled = false;
        var WP_Pistol = GetComponent<WP_Pistol>();
        if (WP_Pistol) WP_Pistol.enabled = false;

        // ���� ����
        var rb = GetComponent<Rigidbody2D>();
        if (rb) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }

        // (����) �÷��̾� �����
        // gameObject.SetActive(false);

        // �� �����
        Invoke(nameof(RestartScene), restartDelay);

        if (GameOverManager.Instance) GameOverManager.Instance.GameOver();
    }

    void RestartScene()
    {
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

}
