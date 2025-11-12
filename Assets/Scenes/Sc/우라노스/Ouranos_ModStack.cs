// Assets/Boss/Ouranos_ModStack.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 이동/점프에 대한 '중첩 가능한' 임시 보정 관리자
/// - 키(문자열)별로 (이동/점프 배수, 만료시각) 관리
/// - 다수 효과가 겹쳐도 곱셈으로 합산하고 만료/삭제 시 자동 복원
/// </summary>
[DisallowMultipleComponent]
public class Ouranos_ModStack : MonoBehaviour
{
	[Tooltip("자동으로 참조할 PlayerMovement")]
	public PlayerMovement Ouranos_pm;

	private float _baseMove;
	private float _baseJump;
	private float _baseHighJump;

	private class Entry { public float mMul, jMul, expire; }
	private readonly Dictionary<string, Entry> _map = new();

	void Awake()
	{
		if (!Ouranos_pm) Ouranos_pm = GetComponentInParent<PlayerMovement>() ?? GetComponent<PlayerMovement>();
		if (Ouranos_pm)
		{
			_baseMove = Ouranos_pm.moveSpeed;
			_baseJump = Ouranos_pm.jumpForce;
			_baseHighJump = Ouranos_pm.highJumpForce;
		}
	}

	void Update()
	{
		if (!Ouranos_pm) return;

		// 만료 제거
		float now = Time.time;
		var keys = new List<string>(_map.Keys);
		foreach (var k in keys) if (_map[k].expire <= now) _map.Remove(k);

		// 배수 계산(없으면 기본값 복원)
		float m = 1f, j = 1f;
		foreach (var e in _map.Values) { m *= e.mMul; j *= e.jMul; }

		Ouranos_pm.moveSpeed = _baseMove * m;
		Ouranos_pm.jumpForce = _baseJump * j;
		Ouranos_pm.highJumpForce = _baseHighJump * j;
	}

	/// <summary>슬로우/점프 보정 적용 또는 갱신</summary>
	public void Ouranos_ApplyOrRefresh(string key, float moveMul, float jumpMul, float duration)
	{
		if (!_map.TryGetValue(key, out var e)) _map[key] = e = new Entry();
		e.mMul = moveMul; e.jMul = jumpMul; e.expire = Time.time + Mathf.Max(0f, duration);
	}

	/// <summary>보정을 즉시 해제</summary>
	public void Ouranos_Remove(string key)
	{
		if (_map.ContainsKey(key)) _map.Remove(key);
	}
}
