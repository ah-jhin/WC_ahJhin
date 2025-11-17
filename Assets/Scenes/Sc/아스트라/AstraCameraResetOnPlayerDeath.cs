using UnityEngine;

/// <summary>
/// 플레이어 오브젝트에 붙여서,
/// PlayerHealth가 사망 상태가 되면 CameraController를 통해 카메라를 즉시 원래 상태로 돌려주는 스크립트.
/// - PlayerHealth.cs 는 수정하지 않고 IsDead 프로퍼티만 읽는다.
/// </summary>
public class AstraCameraResetOnPlayerDeath : MonoBehaviour
{
	[Tooltip("플레이어 체력을 관리하는 PlayerHealth 컴포넌트.")]
	[SerializeField] private PlayerHealth playerHealth;

	[Tooltip("카메라 연출 컨트롤러. 비워두면 CameraController.Instance 를 사용한다.")]
	[SerializeField] private CameraController cameraController;

	private bool _done = false;

	void Awake()
	{
		if (!playerHealth)
			playerHealth = GetComponent<PlayerHealth>();

		if (!cameraController && CameraController.Instance != null)
			cameraController = CameraController.Instance;
	}

	void Update()
	{
		if (_done) return;
		if (!playerHealth || !cameraController) return;

		if (playerHealth.IsDead)
		{
			// 플레이어가 죽는 순간 카메라를 즉시 원래 상태로 돌린다.
			cameraController.ResetAll(true, true, false);
			_done = true;
		}
	}
}
