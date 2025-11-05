// Assets/Scripts/Stage/Parallax2D.cs
// 카메라 이동/줌에 비례해 배경을 느리게 움직여 패럴랙스 효과
using UnityEngine;

public class Parallax2D : MonoBehaviour
{
    [Tooltip("0=카메라와 동기, 1=고정. 보통 0.1~0.6")] public float strength = 0.2f;
    private Vector3 _lastCamPos;

    void Start() { if (Camera.main != null) _lastCamPos = Camera.main.transform.position; }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 delta = cam.transform.position - _lastCamPos;
        transform.position += new Vector3(delta.x * strength, delta.y * strength, 0f);
        _lastCamPos = cam.transform.position;
    }
}
