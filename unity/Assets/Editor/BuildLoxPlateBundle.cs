using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LoxPlate.EditorTools;

/// <summary>
/// Builds the LoxPlate custom-asset bundle. Everything under
/// Assets/LoxPlate/Bundles is packed into `loxplate.assets` for the Linux
/// 64-bit target (the platform Valheim runs on).
///
/// Menu: LoxPlate > Build Asset Bundle
/// CLI:  unity-editor -batchmode -quit -projectPath <project> \
///         -executeMethod LoxPlate.EditorTools.BuildLoxPlateBundle.BuildFromCommandLine
/// </summary>
public static class BuildLoxPlateBundle
{
	private const string SourceFolder = "Assets/LoxPlate/Bundles";
	private const string OutputFolder = "Build/AssetBundles";
	private const string BundleName = "loxplate.assets";

	[MenuItem("LoxPlate/Build Asset Bundle")]
	public static void BuildFromMenu()
	{
		Build();
	}

	public static void BuildFromCommandLine()
	{
		Build();
		EditorApplication.Exit(0);
	}

	private static void Build()
	{
		if (!Directory.Exists(SourceFolder))
		{
			Debug.LogError($"[LoxPlate] {SourceFolder} does not exist - nothing to bundle.");
			return;
		}
		string[] assets = Directory
			.EnumerateFiles(SourceFolder, "*", SearchOption.AllDirectories)
			.Select(path => path.Replace('\\', '/'))
			.Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
				&& !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.Ordinal)
			.ToArray();
		if (assets.Length == 0)
		{
			Debug.LogError($"[LoxPlate] {SourceFolder} is empty - add your barding prefabs first.");
			return;
		}

		var build = new AssetBundleBuild[1];
		build[0].assetBundleName = BundleName;
		build[0].assetNames = assets;

		Directory.CreateDirectory(OutputFolder);
		AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
			OutputFolder,
			build,
			BuildAssetBundleOptions.ChunkBasedCompression,
			BuildTarget.StandaloneLinux64);
		if (manifest == null)
		{
			Debug.LogError("[LoxPlate] AssetBundle build failed - see console for errors.");
			return;
		}
		string output = Path.GetFullPath(OutputFolder);
		Debug.Log($"[LoxPlate] Built {BundleName} with {assets.Length} assets -> {output}\n"
			+ "Copy it into the LoxPlate repo at packaging/assets/loxplate.assets (or straight into "
			+ "BepInEx/plugins/Stew_and_Brew_Labs-LoxPlate/ next to LoxPlate.dll).");
	}
}
