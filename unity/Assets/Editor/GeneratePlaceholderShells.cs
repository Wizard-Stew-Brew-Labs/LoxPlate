using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoxPlate.EditorTools;

/// <summary>
/// Generates placeholder barding shells so the custom-asset pipeline can be
/// exercised without hand-made art: curved barrel-hugging panels (partial
/// elliptical arcs) built procedurally in lox body space and saved as two
/// prefabs the bundle builder packs into loxplate.assets. Replace these
/// prefabs with real models whenever ready and rebuild the bundle.
///
/// Menu: LoxPlate > Generate Placeholder Shells
/// </summary>
public static class GeneratePlaceholderShells
{
	private const string OutputFolder = "Assets/LoxPlate/Bundles";

	// Body space: origin (0, 2.2, 1.2) in Visual/offset space, +Z forward.
	// Real lox bounds: x -2..+2, y -2.36..+1.64, z -3.2..+3.8 in this frame
	// (back line y = +1.64). The barrel ellipse below hugs that body.
	private const float CenterX = 0f;
	private const float CenterY = -0.2f;
	private const float RadiusX = 1.75f;
	private const float RadiusY = 1.5f;

	[MenuItem("LoxPlate/Generate Placeholder Shells")]
	public static void GenerateFromMenu()
	{
		Generate();
	}

	public static void GenerateFromCommandLine()
	{
		Generate();
		EditorApplication.Exit(0);
	}

	private static void Generate()
	{
		Directory.CreateDirectory(OutputFolder);
		CreatePrefab("LoxPlate_BlackMetalBarding", MakeBlackMetalMaterial(), $"{OutputFolder}/LoxPlate_BlackMetalBarding.prefab");
		CreatePrefab("LoxPlate_FlametalBarding", MakeFlametalMaterial(), $"{OutputFolder}/LoxPlate_FlametalBarding.prefab");
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log($"[LoxPlate] Placeholder shells written to {OutputFolder}");
	}

	private static void CreatePrefab(string name, Material material, string prefabPath)
	{
		GameObject root = new GameObject(name);
		try
		{
			// Flank panels hugging the barrel.
			AddArc(root, material, "FlankL", 110f, 200f, 0f, 0f, -0.8f, 2.6f, 0.96f);
			AddArc(root, material, "FlankR", -20f, 70f, 0f, 0f, -0.8f, 2.6f, 0.96f);
			// Flared skirts below the flank line.
			AddArc(root, material, "SkirtL", 193f, 226f, 0.1f, -0.15f, -0.4f, 2.4f, 0.93f);
			AddArc(root, material, "SkirtR", -46f, -13f, 0.1f, -0.15f, -0.4f, 2.4f, 0.93f);
			// Spine ridge over the back line.
			AddArc(root, material, "Spine", 72f, 108f, 0.08f, 0.1f, -0.6f, 2.4f, 1f);
			// Neck guard, leaning forward.
			AddArc(root, material, "Neck", 35f, 145f, 0.05f, 0.45f, 2.9f, 3.6f, 0.9f, tiltX: -22f);
			// Rump guard, leaning back.
			AddArc(root, material, "Rump", 42f, 138f, 0.05f, 0.3f, -2.6f, -1.9f, 0.9f, tiltX: 14f);
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
			if (prefab == null)
			{
				throw new InvalidOperationException("SaveAsPrefabAsset returned null for " + prefabPath);
			}
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(root);
		}
		Debug.Log($"[LoxPlate] Wrote {prefabPath}");
	}

	/// <summary>Partial elliptical arc strip: center (CenterX + ox, CenterY + oy),
	/// angles in degrees (0 = +X, 90 = +Y), z0..z1 with a linear radius taper
	/// toward -Z, optional X-axis tilt of the whole panel.</summary>
	private static void AddArc(GameObject root, Material material, string name,
		float a0, float a1, float offsetX, float offsetY, float z0, float z1,
		float rearScale, float tiltX = 0f)
	{
		const int segments = 24;
		const int rows = 2;
		Vector3[] vertices = new Vector3[(rows + 1) * (segments + 1)];
		Vector3[] normals = new Vector3[vertices.Length];
		Vector2[] uv = new Vector2[vertices.Length];
		int[] triangles = new int[rows * segments * 6];

		float rad0 = a0 * Mathf.Deg2Rad;
		float rad1 = a1 * Mathf.Deg2Rad;
		for (int r = 0; r <= rows; r++)
		{
			float t = (float)r / rows;
			float z = Mathf.Lerp(z0, z1, t);
			float scale = Mathf.Lerp(1f, rearScale, 1f - t);
			for (int s = 0; s <= segments; s++)
			{
				float angle = Mathf.Lerp(rad0, rad1, (float)s / segments);
				Vector3 radial = new Vector3(Mathf.Cos(angle) * RadiusX * scale,
					Mathf.Sin(angle) * RadiusY * scale, 0f);
				int index = r * (segments + 1) + s;
				vertices[index] = new Vector3(CenterX + offsetX, CenterY + offsetY, z) + radial;
				normals[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
				uv[index] = new Vector2((float)s / segments, t);
			}
		}
		int triangle = 0;
		for (int r = 0; r < rows; r++)
		{
			for (int s = 0; s < segments; s++)
			{
				int a = r * (segments + 1) + s;
				int b = a + 1;
				int c = a + segments + 1;
				int d = c + 1;
				triangles[triangle++] = a;
				triangles[triangle++] = c;
				triangles[triangle++] = b;
				triangles[triangle++] = b;
				triangles[triangle++] = c;
				triangles[triangle++] = d;
			}
		}
		Mesh mesh = new Mesh
		{
			name = name,
			vertices = vertices,
			normals = normals,
			uv = uv,
			triangles = triangles,
		};
		AssetDatabase.CreateAsset(mesh, $"{OutputFolder}/Mesh_{name}.asset");

		GameObject part = new GameObject(name);
		part.transform.SetParent(root.transform, false);
		part.transform.localPosition = Vector3.zero;
		part.transform.localRotation = Quaternion.Euler(tiltX, 0f, 0f);
		part.AddComponent<MeshFilter>().sharedMesh = mesh;
		part.AddComponent<MeshRenderer>().sharedMaterial = material;
	}

	private static Material MakeBlackMetalMaterial()
	{
		Material material = new Material(Shader.Find("Standard"));
		material.name = "Mat_BlackMetalBarding";
		material.color = new Color(0.16f, 0.16f, 0.18f);
		material.SetFloat("_Metallic", 0.65f);
		material.SetFloat("_Glossiness", 0.35f);
		AssetDatabase.CreateAsset(material, $"{OutputFolder}/Mat_BlackMetalBarding.asset");
		return material;
	}

	private static Material MakeFlametalMaterial()
	{
		Material material = new Material(Shader.Find("Standard"));
		material.name = "Mat_FlametalBarding";
		material.color = new Color(0.45f, 0.14f, 0.04f);
		material.SetFloat("_Metallic", 0.4f);
		material.SetFloat("_Glossiness", 0.5f);
		material.EnableKeyword("_EMISSION");
		material.SetColor("_EmissionColor", new Color(1.0f, 0.32f, 0.05f) * 0.55f);
		AssetDatabase.CreateAsset(material, $"{OutputFolder}/Mat_FlametalBarding.asset");
		return material;
	}
}
