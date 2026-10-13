using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoxPlate;

/// <summary>
/// Optional custom-asset support: loads `loxplate.assets` (a Unity
/// AssetBundle built with the game's exact editor version) from the mod's
/// install folder. The bundle may contain a GameObject per barding tier -
/// `LoxPlate_BlackMetalBarding` and `LoxPlate_FlametalBarding` - authored in
/// lox body space (+Z forward, +Y up, origin (0, 2.2, 1.2) in
/// Visual/offset space). When the bundle or a tier prefab is missing, the
/// procedural shell from BardingVisuals is used instead, so the mod works
/// with or without shipped art.
/// </summary>
internal static class BardingAssets
{
	internal const string BundleFileName = "loxplate.assets";
	internal const string BlackMetalShellName = "LoxPlate_BlackMetalBarding";
	internal const string FlametalShellName = "LoxPlate_FlametalBarding";

	private static AssetBundle _bundle;
	private static bool _attempted;

	internal static GameObject LoadShellPrefab(int tier)
	{
		if (!_attempted)
		{
			_attempted = true;
			TryLoadBundle();
		}
		if ((Object)(object)_bundle == (Object)null)
		{
			return null;
		}
		string name = tier == BardingItems.TierFlametal ? FlametalShellName : BlackMetalShellName;
		GameObject prefab;
		try
		{
			prefab = _bundle.LoadAsset<GameObject>(name);
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogWarning($"Custom shell '{name}' could not be loaded: {ex.Message}");
			return null;
		}
		if ((Object)(object)prefab == (Object)null && tier == BardingItems.TierBlackMetal)
		{
			LoxPlatePlugin.Log.LogInfo($"Custom bundle has no '{name}'; procedural shell will be used");
		}
		return prefab;
	}

	/// <summary>Instantiate a shell under the compensated anchor, stripping
	/// any colliders the authored prefab may carry (the shell is visual
	/// only - colliders would break the lox's hitbox and player aim).</summary>
	internal static GameObject InstantiateShell(int tier, Transform container, int layer)
	{
		GameObject prefab = LoadShellPrefab(tier);
		if ((Object)(object)prefab == (Object)null)
		{
			return null;
		}
		try
		{
			GameObject shell = Object.Instantiate(prefab, container, false);
			shell.name = "LoxPlate_Barding";
			shell.transform.localPosition = Vector3.zero;
			shell.transform.localRotation = Quaternion.identity;
			shell.transform.localScale = Vector3.one;
			shell.layer = layer;
			Collider[] colliders = shell.GetComponentsInChildren<Collider>(true);
			foreach (Collider collider in colliders)
			{
				Object.Destroy(collider);
			}
			return shell;
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogWarning($"Custom shell '{prefab.name}' failed to instantiate: {ex.Message}");
			return null;
		}
	}

	private static void TryLoadBundle()
	{
		try
		{
			string path = FindBundle();
			if (path == null)
			{
				LoxPlatePlugin.Log.LogInfo($"No {BundleFileName} found - using procedural barding shells");
				return;
			}
			if ((int)SystemInfo.graphicsDeviceType == 4)
			{
				// Headless server: skip bundle loading entirely.
				return;
			}
			// Try the file path first, then from-memory: some Unity runtime
			// builds reject file-loaded bundles with a misleading version
			// error while accepting the exact same bytes from memory
			// (ValheimArmory ships its bundle embedded for this reason).
			_bundle = AssetBundle.LoadFromFile(path);
			if ((Object)(object)_bundle == (Object)null)
			{
				LoxPlatePlugin.Log.LogInfo($"LoadFromFile failed for {Path.GetFileName(path)} - retrying from memory");
				_bundle = AssetBundle.LoadFromMemory(File.ReadAllBytes(path));
			}
			if ((Object)(object)_bundle == (Object)null)
			{
				LoxPlatePlugin.Log.LogWarning($"{path} could not be loaded; procedural shells in use");
				return;
			}
			LoxPlatePlugin.Log.LogInfo($"Custom asset bundle loaded: {Path.GetFileName(path)}");
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogWarning($"Asset bundle load failed: {ex.Message}");
		}
	}

	private static string FindBundle()
	{
		try
		{
			string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			string candidate = assemblyDir != null
				? Path.Combine(assemblyDir, BundleFileName)
				: null;
			if (candidate != null && File.Exists(candidate))
			{
				return candidate;
			}
			candidate = Path.Combine(Paths.PluginPath, BundleFileName);
			return File.Exists(candidate) ? candidate : null;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
