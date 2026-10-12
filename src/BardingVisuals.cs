using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoxPlate;

/// <summary>
/// Procedural barding shells assembled at runtime from Unity primitives and
/// vanilla materials (the LoxCarts "native models and materials" approach):
/// side and spine plates, brow and rump guards, and belly straps. No colliders
/// are created - the shell is purely visual, so the lox's own hitbox and the
/// player's aim raycasts stay untouched.
/// </summary>
internal static class BardingVisuals
{
	private const string RootName = "LoxPlate_Barding";

	private static Material _plateBlack;
	private static Material _plateEmber;
	private static Material _strap;

	/// <summary>Attach a tier shell under the lox. Returns the shell root.</summary>
	internal static GameObject Build(Transform lox, int tier)
	{
		GameObject root = new GameObject(RootName);
		root.transform.SetParent(lox, false);
		root.layer = lox.gameObject.layer;
		BuildParts(root.transform, tier, lox.gameObject.layer);
		return root;
	}

	/// <summary>Build the parts under any transform (also used for icons).</summary>
	internal static void BuildParts(Transform parent, int tier, int layer)
	{
		EnsureMaterials();
		bool ember = tier == BardingItems.TierFlametal;
		Material plate = ember ? _plateEmber : _plateBlack;

		// Side plates (slightly tilted outward)
		Plate(parent, layer, plate, new Vector3(-1.02f, 1.9f, 0.35f), new Vector3(0.09f, 1.5f, 2.7f), 0f, -6f);
		Plate(parent, layer, plate, new Vector3(1.02f, 1.9f, 0.35f), new Vector3(0.09f, 1.5f, 2.7f), 0f, 6f);
		// Spine plate
		Plate(parent, layer, plate, new Vector3(0f, 2.75f, 0.25f), new Vector3(1.7f, 0.09f, 2.8f), 0f, 0f);
		// Brow guard
		Plate(parent, layer, plate, new Vector3(0f, 2.45f, 1.85f), new Vector3(1.3f, 0.9f, 0.09f), -18f, 0f);
		// Rump guard
		Plate(parent, layer, plate, new Vector3(0f, 2.4f, -1.6f), new Vector3(1.45f, 1.0f, 0.09f), 14f, 0f);
		// Shoulder guards
		Plate(parent, layer, plate, new Vector3(-0.95f, 2.2f, 1.15f), new Vector3(0.09f, 0.8f, 0.9f), 0f, -10f);
		Plate(parent, layer, plate, new Vector3(0.95f, 2.2f, 1.15f), new Vector3(0.09f, 0.8f, 0.9f), 0f, 10f);
		// Belly straps
		Plate(parent, layer, _strap, new Vector3(0f, 1.0f, 0.85f), new Vector3(2.3f, 0.24f, 0.16f), 0f, 0f);
		Plate(parent, layer, _strap, new Vector3(0f, 1.0f, -0.85f), new Vector3(2.3f, 0.24f, 0.16f), 0f, 0f);
		if (ember)
		{
			// Ember edges along the top of the side plates
			Plate(parent, layer, _plateEmber, new Vector3(-1.08f, 2.68f, 0.3f), new Vector3(0.05f, 0.08f, 2.6f), 0f, -6f);
			Plate(parent, layer, _plateEmber, new Vector3(1.08f, 2.68f, 0.3f), new Vector3(0.05f, 0.08f, 2.6f), 0f, 6f);
		}
	}

	private static void Plate(Transform parent, int layer, Material material, Vector3 position,
		Vector3 size, float rotX, float rotZ)
	{
		GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
		Object.Destroy(plate.GetComponent<Collider>());
		plate.layer = layer;
		plate.transform.SetParent(parent, false);
		plate.transform.localPosition = position;
		plate.transform.localRotation = Quaternion.Euler(rotX, 0f, rotZ);
		plate.transform.localScale = size;
		plate.GetComponent<MeshRenderer>().sharedMaterial = material;
	}

	private static void EnsureMaterials()
	{
		if (_plateBlack != null && _plateEmber != null && _strap != null)
		{
			return;
		}
		_plateBlack = _plateBlack ?? DonorMaterial("BlackMetal", null, new Color(0.16f, 0.16f, 0.18f));
		_plateEmber = _plateEmber ?? DonorMaterial("FlametalOre", "Flametal", new Color(0.55f, 0.16f, 0.03f));
		_strap = _strap ?? DonorMaterial("LoxPelt", null, new Color(0.42f, 0.3f, 0.18f));
	}

	private static Material DonorMaterial(string itemName, string altDonor, Color fallback)
	{
		ObjectDB db = ObjectDB.instance;
		if (db != null)
		{
			foreach (string candidate in new[] { itemName, altDonor })
			{
				if (candidate == null)
				{
					continue;
				}
				GameObject prefab = db.GetItemPrefab(candidate);
				if ((Object)(object)prefab == (Object)null)
				{
					continue;
				}
				MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
				if (!((Object)(object)renderer == (Object)null) && !((Object)(object)renderer.sharedMaterial == (Object)null))
				{
					return new Material(renderer.sharedMaterial);
				}
			}
		}
		LoxPlatePlugin.Log.LogWarning($"Vanilla material donor {itemName} unavailable; using a plain tinted material");
		return FallbackMaterial(fallback);
	}

	private static Material FallbackMaterial(Color color)
	{
		Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
		Material material = new Material(shader);
		material.color = color;
		return material;
	}
}
