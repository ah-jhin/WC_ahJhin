using System;
namespace Ouranos_Boss
{
	/// <summary>보스전 전역 시그널. 보스 사망 통지 등</summary>
	public static class Ouranos_GlobalSignals
	{
		public static event Action BossDied;     // 구독: 엔티티/스포너
		public static void RaiseBossDied() => BossDied?.Invoke();
	}
}
