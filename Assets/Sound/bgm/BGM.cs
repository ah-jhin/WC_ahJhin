// BGMPlayer.cs  (BGM 오브젝트에 부착)
// 아주 단순: 매 프레임 전역값을 반영
using UnityEngine;
public class BGM : MonoBehaviour
{
	public AudioSource src; // 같은 오브젝트의 AudioSource
	void Awake() { if (!src) src = GetComponent<AudioSource>(); }
	void Update() { if (src) src.volume = AudioBus.BGM; }
}
