using UnityEngine;

public class ElectricBeam : MonoBehaviour
{
    [Header("Beam Settings")]
    public float speed = 8f;              // �¡�� �̵� �ӵ�
    public int damage = 25;               // ���� �� �ִ� ����
    public float maxLifeTime = 8f;        // ���� �Ҹ� Ÿ�̸�
    public Vector2 moveDir = Vector2.right; // +x �Ǵ� -x
    public float ignorePlayerTime = 0.25f;  // ���� ���� ���� �ð�

    private float _spawnTime;

    void OnEnable()
    {
        _spawnTime = Time.time;
    }

    void Update()
    {
        // �̵�
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);

        // ȭ�� �� ����
        var cam = Camera.main;
        if (cam)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            if (vp.x < -0.2f || vp.x > 1.2f) Destroy(gameObject);
        }

        // ���� �ʰ� �� ����
        if (Time.time - _spawnTime > maxLifeTime)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ���� ���� ��� ����
        if (Time.time - _spawnTime < ignorePlayerTime) return;
        if (!other.CompareTag("Player")) return;

        // �뽬 ���̸� ���
        var pm = other.GetComponent<PlayerMovement>();
        if (pm != null && pm.isDashing) return;

        // �ǰ� ó��
        var hp = other.GetComponent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(damage, false, 1f);
    }
}
