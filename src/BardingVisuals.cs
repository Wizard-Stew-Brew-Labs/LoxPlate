using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoxPlate;

/// <summary>
/// Procedural barding shells assembled at runtime from Unity primitives and
/// vanilla materials (the LoxCarts "native models and materials" approach).
///
/// The shell rides the lox's animated hip spine bone
/// (Visual/offset/Armature/Root/Hip) through a compensation container, so it
/// tracks the animal's movement and animation. Panels are composed in true
/// body coordinates as draped layers: tilted flank plates, flared skirts,
/// shoulder/hindquarter wraps, a spine ridge and leather belly straps. No
/// colliders are created - the shell is purely visual.
/// </summary>
internal static class BardingVisuals
{
	private const string RootName = "LoxPlate_Barding";

	private static Material _plateBlack;
	private static Material _plateEmber;
	private static Material _strap;

	/// <summary>Attach a tier shell to the animal's animated spine bone.</summary>
	internal static GameObject Build(Tameable tame, Character character, int tier)
	{
		Transform frame = ((Component)character).transform.Find("Visual/offset");
		if ((Object)(object)frame == (Object)null)
		{
			frame = ((Component)character).transform;
		}
		Transform bone = ((Component)character).transform.Find("Visual/offset/Armature/Root/Hip");

		Transform root;
		if ((Object)(object)bone != (Object)null)
		{
			// Ride the animated hip bone. The armature is authored at 1/100
			// scale in an arbitrary frame, so the container compensates:
			// world scale 1, world rotation aligned to the character's axes,
			// origin at a chosen point in body space. It still inherits the
			// bone's animated motion from here on.
			root = new GameObject(RootName).transform;
			root.SetParent(bone, false);
			Vector3 originWorld = frame.TransformPoint(new Vector3(0f, 2.2f, 1.2f));
			root.localPosition = bone.InverseTransformPoint(originWorld);
			root.localRotation = Quaternion.Inverse(bone.rotation) * frame.rotation;
			Vector3 scale = bone.lossyScale;
			root.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
		}
		else
		{
			// Skeleton not found (unexpected): fall back to a static shell on
			// the character root.
			root = new GameObject(RootName).transform;
			root.SetParent(frame, false);
			root.localPosition = new Vector3(0f, 2.2f, 1.2f);
		}

		int layer = ((Component)character).gameObject.layer;
		root.gameObject.layer = layer;
		// Custom art first (loxplate.assets), procedural composition as the
		// fallback so the mod works with or without shipped assets.
		GameObject shell = BardingAssets.InstantiateShell(tier, root, layer);
		if (shell != null)
		{
			return root.gameObject;
		}
		BuildParts(root, tier, layer);
		return root.gameObject;
	}

	/// <summary>
	/// Build the parts under a container whose axes match the lox's body
	/// space (+Z forward, +Y up, origin at (0, 2.2, 1.2) in Visual/offset
	/// space) with 1 unit = 1 meter. Real lox bounds from the rig dump:
	/// body spans x -2..+2, y -0.16..+3.84 (back line ~3.84), z -2..+5.
	/// </summary>
	internal static void BuildParts(Transform parent, int tier, int layer)
	{
		EnsureMaterials();
		bool ember = tier == BardingItems.TierFlametal;
		Material plate = ember ? _plateEmber : _plateBlack;
		Material trim = ember ? _plateEmber : _plateBlack;

		// Upper flank plates: hug the barrel just below the back line,
		// tilted so the top edge sits close to the fur.
		Plate(parent, layer, plate, new Vector3(-1.72f, 0.9f, 0.6f), new Vector3(0.08f, 1.5f, 2.6f), 0f, -10f);
		Plate(parent, layer, plate, new Vector3(1.72f, 0.9f, 0.6f), new Vector3(0.08f, 1.5f, 2.6f), 0f, 10f);
		// Flared skirt panels below the flank plates.
		Plate(parent, layer, plate, new Vector3(-1.95f, 0.15f, 0.6f), new Vector3(0.06f, 0.8f, 2.4f), 0f, -22f);
		Plate(parent, layer, plate, new Vector3(1.95f, 0.15f, 0.6f), new Vector3(0.06f, 0.8f, 2.4f), 0f, 22f);
		// Shoulder and hindquarter wraps, turned to follow the barrel.
		Plate(parent, layer, plate, new Vector3(-1.6f, 0.9f, 2.2f), new Vector3(0.08f, 1.3f, 0.8f), 0f, -12f, -18f);
		Plate(parent, layer, plate, new Vector3(1.6f, 0.9f, 2.2f), new Vector3(0.08f, 1.3f, 0.8f), 0f, 12f, 18f);
		Plate(parent, layer, plate, new Vector3(-1.62f, 0.85f, -0.9f), new Vector3(0.08f, 1.35f, 0.8f), 0f, -12f, 18f);
		Plate(parent, layer, plate, new Vector3(1.62f, 0.85f, -0.9f), new Vector3(0.08f, 1.35f, 0.8f), 0f, 12f, -18f);
		// Spine ridge riding just above the back line.
		Plate(parent, layer, plate, new Vector3(0f, 1.7f, 0.9f), new Vector3(0.6f, 0.16f, 2.2f), 0f, 0f);
		// Neck and rump guards.
		Plate(parent, layer, plate, new Vector3(0f, 1.45f, 2.9f), new Vector3(1.2f, 0.7f, 0.08f), -22f, 0f);
		Plate(parent, layer, plate, new Vector3(0f, 1.5f, -1.2f), new Vector3(1.4f, 0.75f, 0.08f), 16f, 0f);
		// Trim strips along the flank plate tops.
		Plate(parent, layer, trim, new Vector3(-1.66f, 1.6f, 0.6f), new Vector3(0.05f, 0.08f, 2.6f), 0f, -10f);
		Plate(parent, layer, trim, new Vector3(1.66f, 1.6f, 0.6f), new Vector3(0.05f, 0.08f, 2.6f), 0f, 10f);
		// Leather belly straps passing under the barrel.
		Plate(parent, layer, _strap, new Vector3(0f, -1.2f, 1.4f), new Vector3(3.6f, 0.18f, 0.14f), 0f, 0f);
		Plate(parent, layer, _strap, new Vector3(0f, -1.2f, -1.4f), new Vector3(3.6f, 0.18f, 0.14f), 0f, 0f);
	}

	private static void Plate(Transform parent, int layer, Material material, Vector3 position,
		Vector3 size, float rotX, float rotZ, float rotY = 0f)
	{
		GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
		Object.Destroy(plate.GetComponent<Collider>());
		plate.layer = layer;
		plate.transform.SetParent(parent, false);
		plate.transform.localPosition = position;
		plate.transform.localRotation = Quaternion.Euler(rotX, rotY, rotZ);
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
