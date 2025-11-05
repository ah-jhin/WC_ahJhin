using UnityEngine;

/// <summary>
/// 경고(텔레그래프) 프리팹에 붙여 쓰는 보조 스크립트
/// - 활성화 시 SFX 1회 재생
/// - lifeTime 후 자동 파괴
/// - 프리팹에 이 스크립트가 없어도 Pattern 쪽에서 자동으로 붙여준다(override 필요 시)
/// </summary>
[DisallowMultipleComponent]
public class TelegraphOneShot : MonoBehaviour
{
	[Header("수명/지연")]
	[Tooltip("경고가 유지될 시간(초). 시간이 지나면 자동 파괴")]
	public float lifeTime = 0.6f;

	[Header("옵션 SFX")]
	[Tooltip("경고 출력과 함께 재생할 효과음(없으면 재생 안함)")]
	public AudioClip sfx;          // 재생할 오디오 클립
	[Range(0f, 1f)]
	public float sfxVolume = 0.8f; // 재생 볼륨

	private void OnEnable()
	{
		if (sfx != null)
		{
			// 2D 재생용 임시 오브젝트를 만들어서 즉시 재생 후 자동 삭제
			GameObject go = new GameObject("SFX_Telegraph_2D");  // 임시 SFX 오브젝트
			go.transform.position = Vector3.zero;                // 2D라 위치는 무의미
			var src = go.AddComponent<AudioSource>();            // AudioSource 추가
			src.clip = sfx;                                      // 재생할 클립
			src.spatialBlend = 0f;                               // 0=2D, 1=3D
			src.volume = Mathf.Clamp01(sfxVolume);               // 볼륨(0~1)
			src.playOnAwake = false;                             // 수동 재생
			src.loop = false;                                    // 1회 재생
			src.Play();                                          // 재생 시작
			Destroy(go, sfx.length + 0.1f);                      // 재생 끝나면 제거
		}

		if (lifeTime > 0f)
		{
			Destroy(gameObject, lifeTime);                       // 경고표시 오브젝트 수명
		}
	}

}
