// Assets/Boss/Ouranos_Pattern.cs
using System.Collections;
using UnityEngine;

namespace Ouranos_Boss
{
	/// <summary>
	/// BossSequenceController → 패턴 시작 신호를 받는 최소 베이스.
	/// - autoStart: 자체 시작(프로토타입용)
	/// - waitForSpawnSignal: 컨트롤러 신호 후 시작
	/// - 파생 클래스는 MainLoop() 구현
	/// </summary>
	public abstract class Ouranos_Pattern : MonoBehaviour
	{
		[Header("시작 제어")]
		public bool autoStart = false;          // true면 Start에서 즉시 시작
		public bool waitForSpawnSignal = true;  // 컨트롤러 신호를 받은 뒤 시작

		bool _started;

		protected virtual void Start()
		{
			// 자동 시작 모드
			if (autoStart && !_started) StartPatterns();
		}

		/// <summary>컨트롤러에서 호출. 보스 스폰 완료 신호</summary>
		public void SignalBossSpawned()
		{
			if (!_started) StartPatterns();
		}

		/// <summary>패턴 시작. MainLoop 코루틴 실행</summary>
		public void StartPatterns()
		{
			if (_started) return;
			_started = true;
			StartCoroutine(MainLoop());
		}

		/// <summary>파생 클래스에서 보스 패턴 메인 루프 구현</summary>
		protected abstract IEnumerator MainLoop();
	}
}
