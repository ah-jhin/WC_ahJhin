using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    public Transform cam;  // ���� �ڵ� Camera.main
    public Vector2 parallaxMultiplier = new Vector2(0.05f, 0f);
    Vector3 lastCamPos;

    void Start()
    {
        if (!cam) cam = Camera.main.transform;
        lastCamPos = cam.position;
    }

    void LateUpdate()
    {
        var delta = cam.position - lastCamPos;
        transform.position += new Vector3(delta.x * parallaxMultiplier.x,
                                          delta.y * parallaxMultiplier.y, 0f);
        lastCamPos = cam.position;
    }
}
