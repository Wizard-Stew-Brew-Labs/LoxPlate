using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LoxPlate;

/// <summary>
/// Renders the barding icon by photographing a miniature shell on an
/// offscreen stage (the WagonThumbnail technique, simplified). Falls back to
/// the donor item's icon when rendering is unavailable (headless server).
/// </summary>
internal static class BardingIcon
{
	private const int Size = 128;

	internal static Sprite Render(GameObject itemClone, GameObject donor, int tier)
	{
		if ((int)SystemInfo.graphicsDeviceType == 4)
		{
			return DonorIcon(donor);
		}
		GameObject stage = null;
		RenderTexture target = null;
		Texture2D texture = null;
		RenderTexture previousActive = RenderTexture.active;
		UnityEngine.Rendering.AmbientMode ambientMode = RenderSettings.ambientMode;
		Color ambientLight = RenderSettings.ambientLight;
		bool fog = RenderSettings.fog;
		List<Light> disabledLights = new List<Light>();
		try
		{
			Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
			if ((Object)(object)shader == (Object)null)
			{
				throw new InvalidOperationException("no neutral shader");
			}
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);
			RenderSettings.fog = false;

			stage = new GameObject("LoxPlateIconStage");
			stage.transform.position = new Vector3(10000f, 10000f, 10000f);
			bool customShell = BardingAssets.InstantiateShell(tier, stage.transform, 31) != null;
			if (!customShell)
			{
				BardingVisuals.BuildParts(stage.transform, tier, 31);
			}

			Renderer[] renderers = stage.GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0)
			{
				throw new InvalidOperationException("shell has no renderers");
			}
			// Neutralize every material to the plain shader so the icon does
			// not depend on donor shader availability - but keep authored
			// bundle materials untouched so custom art shows as authored.
			if (!customShell)
			{
				foreach (Renderer renderer in renderers)
				{
					Material[] materials = renderer.sharedMaterials;
					for (int i = 0; i < materials.Length; i++)
					{
						Material neutral = new Material(shader);
						Material original = materials[i];
						if ((Object)(object)original != (Object)null && original.HasProperty("_Color"))
						{
							neutral.color = original.color;
						}
						materials[i] = neutral;
					}
					renderer.sharedMaterials = materials;
				}
			}
			Bounds bounds = renderers[0].bounds;
			foreach (Renderer renderer in renderers)
			{
				bounds.Encapsulate(renderer.bounds);
			}

			GameObject cameraHost = new GameObject("IconCamera");
			cameraHost.transform.SetParent(stage.transform, false);
			Camera camera = cameraHost.AddComponent<Camera>();
			((Behaviour)camera).enabled = false;
			camera.cullingMask = int.MinValue;
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = Color.clear;
			camera.orthographic = true;
			camera.aspect = 1f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 100f;
			camera.allowHDR = false;
			camera.allowMSAA = true;
			Vector3 viewDirection = new Vector3(10f, 7f, -12f).normalized;
			cameraHost.transform.position = bounds.center + viewDirection * 25f;
			cameraHost.transform.LookAt(bounds.center);
			float extent = 0f;
			for (int x = -1; x <= 1; x += 2)
			{
				for (int y = -1; y <= 1; y += 2)
				{
					for (int z = -1; z <= 1; z += 2)
					{
						Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
						Vector3 local = cameraHost.transform.InverseTransformPoint(corner);
						extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
					}
				}
			}
			camera.orthographicSize = extent * 1.1f;

			GameObject lightHost = new GameObject("IconLight");
			lightHost.transform.SetParent(stage.transform, false);
			Light light = lightHost.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1f;
			light.cullingMask = int.MinValue;
			lightHost.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
			light.shadows = LightShadows.None;
			foreach (Light other in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
			{
				if ((Object)(object)other != (Object)(object)light && ((Behaviour)other).enabled && other.type == LightType.Directional)
				{
					disabledLights.Add(other);
					((Behaviour)other).enabled = false;
				}
			}

			target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
			target.antiAliasing = 4;
			target.Create();
			camera.targetTexture = target;
			camera.Render();
			RenderTexture.active = target;
			texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
			texture.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
			texture.filterMode = FilterMode.Bilinear;
			texture.Apply();
			texture.name = "LoxPlateItemIcon";
			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
			LoxPlatePlugin.Log.LogDebug($"Rendered icon for barding tier {tier}");
			return sprite;
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogWarning($"Icon render failed for tier {tier}; using donor icon ({ex.Message})");
			if (texture != null)
			{
				Object.Destroy(texture);
			}
			return DonorIcon(donor);
		}
		finally
		{
			RenderTexture.active = previousActive;
			RenderSettings.ambientMode = ambientMode;
			RenderSettings.ambientLight = ambientLight;
			RenderSettings.fog = fog;
			foreach (Light other in disabledLights)
			{
				if ((Object)(object)other != (Object)null)
				{
					((Behaviour)other).enabled = true;
				}
			}
			if ((Object)(object)stage != (Object)null)
			{
				Object.Destroy(stage);
			}
			if ((Object)(object)target != (Object)null)
			{
				target.Release();
				Object.Destroy(target);
			}
		}
	}

	private static Sprite DonorIcon(GameObject donor)
	{
		if ((Object)(object)donor == (Object)null)
		{
			return null;
		}
		ItemDrop drop = donor.GetComponent<ItemDrop>();
		if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null
			|| drop.m_itemData.m_shared.m_icons == null || drop.m_itemData.m_shared.m_icons.Length == 0)
		{
			return null;
		}
		return drop.m_itemData.m_shared.m_icons[0];
	}
}
