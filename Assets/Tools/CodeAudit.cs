#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

/// 프로젝트에서 클래스명 중복과 IDamageable 구현 파일만 빠르게 찾는다.
public static class CodeAudit
{
	static readonly Regex rxClass = new(@"\bclass\s+([A-Za-z_]\w*)");
	static readonly Regex rxIface = new(@"\bIDamageable\b");
	[MenuItem("Tools/Code Audit/Scan Duplicates & Implementers")]
	public static void Scan()
	{
		var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
		var names = new System.Collections.Generic.Dictionary<string, int>();
		var dmgFiles = new System.Collections.Generic.List<string>();
		foreach (var g in guids)
		{
			var p = AssetDatabase.GUIDToAssetPath(g); if (!p.EndsWith(".cs")) continue;
			var t = File.ReadAllText(p);
			foreach (Match m in rxClass.Matches(t)) { var c = m.Groups[1].Value; names[c] = names.ContainsKey(c) ? names[c] + 1 : 1; }
			if (rxIface.IsMatch(t)) dmgFiles.Add(p);
		}
		foreach (var kv in names.Where(k => k.Value > 1)) Debug.Log($"중복 클래스: {kv.Key} x{kv.Value}");
		foreach (var p in dmgFiles) Debug.Log($"IDamageable 관련: {p}");
		EditorUtility.DisplayDialog("Code Audit", "Console 확인", "OK");
	}
}
#endif
