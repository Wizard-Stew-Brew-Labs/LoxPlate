using System.Text;
using UnityEngine;

namespace LoxPlate;

/// <summary>
/// One-shot (per session) dump of the live lox prefab's rig: the saddle
/// anchor frame, renderer bounds in anchor-local space and the transform
/// tree. Written to the Info log so shell offsets can be tuned from real
/// coordinates instead of guesswork.
/// </summary>
internal static class BardingDiagnostics
{
	private static bool _dumped;

	internal static void DumpLoxPrefab(ZNetScene scene)
	{
		if (_dumped || scene == null)
		{
			return;
		}
		foreach (GameObject prefab in scene.m_prefabs)
		{
			if ((Object)(object)prefab == (Object)null || ((Object)prefab).name != "Lox")
			{
				continue;
			}
			_dumped = true;
			try
			{
				Tameable tameable = prefab.GetComponent<Tameable>();
				Character character = prefab.GetComponent<Character>();
				Transform anchor = tameable != null && tameable.m_saddle != null
					? tameable.m_saddle.transform
					: prefab.transform;
				StringBuilder sb = new StringBuilder();
				sb.AppendLine($"[LoxPlate] Lox rig dump (anchor={GetPath(anchor, prefab.transform)}):");
				sb.AppendLine($"  anchor localPos={anchor.localPosition} localRot={anchor.localEulerAngles} lossyScale={anchor.lossyScale}");
				Sadle sadle = tameable != null ? tameable.m_saddle : null;
				if (sadle != null && sadle.m_attachPoint != null)
				{
					sb.AppendLine($"  attachPoint localPos={sadle.m_attachPoint.localPosition} (in sadle frame)");
				}
				Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
				foreach (Renderer renderer in renderers)
				{
					Bounds b = renderer.bounds;
					Vector3 min = anchor.InverseTransformPoint(b.min);
					Vector3 max = anchor.InverseTransformPoint(b.max);
					sb.AppendLine($"  renderer {((Component)renderer).name} ({renderer.GetType().Name}) anchor-local min={min} max={max}");
				}
				int count = 0;
				AppendTree(prefab.transform, prefab.transform, sb, 0, ref count);
				LoxPlatePlugin.Log.LogInfo(sb.ToString());
			}
			catch (System.Exception ex)
			{
				LoxPlatePlugin.Log.LogWarning($"Lox rig dump failed: {ex.Message}");
			}
			return;
		}
	}

	private static void AppendTree(Transform node, Transform root, StringBuilder sb, int depth, ref int count)
	{
		if (count >= 90)
		{
			return;
		}
		string path = GetPath(node, root);
		sb.AppendLine($"  t[{count}] {path} localPos={node.localPosition} localScale={node.localScale}");
		count++;
		for (int i = 0; i < node.childCount; i++)
		{
			AppendTree(node.GetChild(i), root, sb, depth + 1, ref count);
		}
	}

	private static string GetPath(Transform node, Transform root)
	{
		if (node == root)
		{
			return node.name;
		}
		return GetPath(node.parent, root) + "/" + node.name;
	}
}
