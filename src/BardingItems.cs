using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoxPlate;

/// <summary>
/// Creates the two barding item prefabs (cloned from the vanilla BlackMetal
/// pickup, which already carries a networked ItemDrop) and registers them
/// with ZNetScene (network drops) plus ObjectDB (inventory, crafting) and the
/// forge recipes. Registration is idempotent and tolerates any Awake order:
/// each stage retries on the next hook until it succeeds.
/// </summary>
internal static class BardingItems
{
	internal const int TierNone = 0;
	internal const int TierBlackMetal = 1;
	internal const int TierFlametal = 2;

	internal const string BlackMetalPrefab = "LoxPlate_BlackMetalBarding";
	internal const string FlametalPrefab = "LoxPlate_FlametalBarding";

	internal const string BlackMetalName = "Black Metal Barding";
	internal const string FlametalName = "Flametal Barding";

	private static GameObject _blackMetal;
	private static GameObject _flametal;

	internal static string PrefabNameOf(int tier)
	{
		return tier switch
		{
			TierBlackMetal => BlackMetalPrefab,
			TierFlametal => FlametalPrefab,
			_ => null,
		};
	}

	internal static GameObject PrefabOf(int tier)
	{
		return tier switch
		{
			TierBlackMetal => _blackMetal,
			TierFlametal => _flametal,
			_ => null,
		};
	}

	/// <summary>Which barding tier a held inventory item is, or TierNone.</summary>
	internal static int TierOf(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return TierNone;
		}
		if (item.m_dropPrefab != null)
		{
			if (((Object)item.m_dropPrefab).name == BlackMetalPrefab)
			{
				return TierBlackMetal;
			}
			if (((Object)item.m_dropPrefab).name == FlametalPrefab)
			{
				return TierFlametal;
			}
		}
		if (item.m_shared.m_name == BlackMetalName)
		{
			return TierBlackMetal;
		}
		if (item.m_shared.m_name == FlametalName)
		{
			return TierFlametal;
		}
		return TierNone;
	}

	/// <summary>ZNetScene.Awake postfix: make the items networkable so barding
	/// dropped by a dying or unhitched lox syncs to every peer.</summary>
	internal static void RegisterNetwork(ZNetScene scene)
	{
		try
		{
			if (scene == null)
			{
				return;
			}
			EnsurePrefabs(scene);
			if (_blackMetal == null && _flametal == null)
			{
				return;
			}
			Dictionary<int, GameObject> named = (Dictionary<int, GameObject>)AccessTools
				.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(scene);
			if (named == null)
			{
				return;
			}
			foreach (GameObject prefab in new[] { _blackMetal, _flametal })
			{
				if (!((Object)(object)prefab == (Object)null) && !scene.m_prefabs.Contains(prefab))
				{
					scene.m_prefabs.Add(prefab);
					named[StringExtensionMethods.GetStableHashCode(((Object)prefab).name)] = prefab;
					LoxPlatePlugin.Log.LogInfo($"Network item registered: {((Object)prefab).name}");
				}
			}
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogError("Barding network registration failed: " + ex);
		}
	}

	/// <summary>ObjectDB.Awake / CopyOtherDB postfix: add items to the item
	/// database and append the forge recipes.</summary>
	internal static void RegisterDatabase(ObjectDB db)
	{
		try
		{
			if (db == null)
			{
				return;
			}
			EnsurePrefabs(null);
			if (_blackMetal == null || _flametal == null)
			{
				return;
			}
			Dictionary<int, GameObject> byHash = (Dictionary<int, GameObject>)AccessTools
				.Field(typeof(ObjectDB), "m_itemByHash").GetValue(db);
			if (byHash == null)
			{
				return;
			}
			foreach (GameObject prefab in new[] { _blackMetal, _flametal })
			{
				if (db.GetItemPrefab(((Object)prefab).name) != null)
				{
					continue;
				}
				db.m_items.Add(prefab);
				byHash[StringExtensionMethods.GetStableHashCode(((Object)prefab).name)] = prefab;
				LoxPlatePlugin.Log.LogInfo($"Item registered: {((Object)prefab).name}");
			}
			EnsureRecipes(db);
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogError("Barding item registration failed: " + ex);
		}
	}

	private static void EnsurePrefabs(ZNetScene scene)
	{
		if (_blackMetal != null && _flametal != null)
		{
			return;
		}
		GameObject donor = FindDonor(scene);
		if (donor == null)
		{
			return;
		}
		try
		{
			_blackMetal = CreateItem(donor, BlackMetalPrefab, BlackMetalName,
				"Heavy lox-hide barding faced with black metal plate. Fitted onto a tamed lox.",
				weight: 10f, tier: TierBlackMetal);
			_flametal = CreateItem(donor, FlametalPrefab, FlametalName,
				"Lox-hide barding reinforced with smoldering flametal. Fitted onto a tamed lox.",
				weight: 14f, tier: TierFlametal);
			Object.DontDestroyOnLoad(_blackMetal);
			Object.DontDestroyOnLoad(_flametal);
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogError("Barding prefab construction failed: " + ex);
			if (_blackMetal != null)
			{
				Object.Destroy(_blackMetal);
			}
			if (_flametal != null)
			{
				Object.Destroy(_flametal);
			}
			_blackMetal = null;
			_flametal = null;
		}
	}

	private static GameObject FindDonor(ZNetScene scene)
	{
		GameObject donor = null;
		if (scene != null)
		{
			donor = ((IEnumerable<GameObject>)scene.m_prefabs).FirstOrDefault((Func<GameObject, bool>)(
				(GameObject p) => (Object)(object)p != (Object)null && ((Object)p).name == "BlackMetal"));
		}
		if (donor == null && ObjectDB.instance != null)
		{
			donor = ObjectDB.instance.GetItemPrefab("BlackMetal");
		}
		if (donor == null)
		{
			LoxPlatePlugin.Log.LogDebug("BlackMetal donor prefab not available yet; will retry");
		}
		return donor;
	}

	private static GameObject CreateItem(GameObject donor, string name, string displayName,
		string description, float weight, int tier)
	{
		GameObject clone = Object.Instantiate(donor);
		((Object)clone).name = name;
		clone.SetActive(false);
		ItemDrop drop = clone.GetComponent<ItemDrop>();
		if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
		{
			throw new InvalidOperationException("BlackMetal donor lacks a usable ItemDrop");
		}
		drop.m_itemData.m_dropPrefab = clone;
		drop.m_itemData.m_shared.m_name = displayName;
		drop.m_itemData.m_shared.m_description = description;
		drop.m_itemData.m_shared.m_weight = weight;
		drop.m_itemData.m_shared.m_maxStackSize = 1;
		// Durability makes the set forge-repairable: absorbing hits wears it
		// (see LoxBarding.ApplyProtection), the vanilla repair tab restores it.
		drop.m_itemData.m_shared.m_useDurability = true;
		drop.m_itemData.m_shared.m_maxDurability = LoxBarding.MaxDurability;
		drop.m_itemData.m_shared.m_canBeReparied = true;
		Sprite icon = BardingIcon.Render(clone, donor, tier);
		if (icon != null)
		{
			drop.m_itemData.m_shared.m_icons = new[] { icon };
		}
		else
		{
			LoxPlatePlugin.Log.LogWarning($"No icon rendered for {name}; keeping donor icon");
		}
		return clone;
	}

	private static void EnsureRecipes(ObjectDB db)
	{
		// The forge recipe doubles as the repair path: 1.0's repair tab
		// resolves the station through the item's recipe, so damaged barding
		// is listed for repair at any forge without custom UI.
		CraftingStation station = FindStation("forge", "Forge");
		if ((Object)(object)station == (Object)null)
		{
			LoxPlatePlugin.Log.LogWarning("Forge CraftingStation unavailable; barding recipe deferred");
			return;
		}
		AddRecipe(db, "Recipe_" + BlackMetalPrefab, _blackMetal, station, new[]
		{
			new KeyValuePair<string, int>("BlackMetal", 20),
			new KeyValuePair<string, int>("LoxPelt", 12),
			new KeyValuePair<string, int>("LinenThread", 10),
			new KeyValuePair<string, int>("GoblinTotem", 1),
		}, minStationLevel: 2);
		AddRecipe(db, "Recipe_" + FlametalPrefab, _flametal, station, new[]
		{
			new KeyValuePair<string, int>("Flametal", 16),
			new KeyValuePair<string, int>("LoxPelt", 16),
			new KeyValuePair<string, int>("LinenThread", 20),
			new KeyValuePair<string, int>("TrophyCharredMelee", 1),
		}, minStationLevel: 3);
	}

	private static void AddRecipe(ObjectDB db, string recipeName, GameObject itemPrefab,
		CraftingStation station, KeyValuePair<string, int>[] ingredients, int minStationLevel)
	{
		if ((Object)(object)itemPrefab == (Object)null)
		{
			return;
		}
		if (db.m_recipes.Any((Recipe r) => r != null && r.name == recipeName))
		{
			return;
		}
		List<Piece.Requirement> requirements = new List<Piece.Requirement>();
		foreach (KeyValuePair<string, int> ingredient in ingredients)
		{
			GameObject ingredientPrefab = db.GetItemPrefab(ingredient.Key);
			if ((Object)(object)ingredientPrefab == (Object)null)
			{
				LoxPlatePlugin.Log.LogWarning($"Missing barding ingredient {ingredient.Key}; recipe deferred");
				return;
			}
			ItemDrop ingredientDrop = ingredientPrefab.GetComponent<ItemDrop>();
			if ((Object)(object)ingredientDrop == (Object)null)
			{
				LoxPlatePlugin.Log.LogError($"Invalid ingredient ItemDrop {ingredient.Key}; recipe deferred");
				return;
			}
			requirements.Add(new Piece.Requirement
			{
				m_resItem = ingredientDrop,
				m_amount = ingredient.Value,
				m_recover = false,
			});
		}
		Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
		((Object)recipe).name = recipeName;
		recipe.m_item = itemPrefab.GetComponent<ItemDrop>();
		recipe.m_amount = 1;
		recipe.m_craftingStation = station;
		recipe.m_minStationLevel = minStationLevel;
		recipe.m_resources = requirements.ToArray();
		db.m_recipes.Add(recipe);
		LoxPlatePlugin.Log.LogInfo($"Recipe registered: {recipeName} ({ingredients.Length} ingredients)");
	}

	private static CraftingStation FindStation(params string[] names)
	{
		ZNetScene scene = ZNetScene.instance;
		if ((Object)(object)scene != (Object)null)
		{
			foreach (string name in names)
			{
				GameObject prefab = ((IEnumerable<GameObject>)scene.m_prefabs).FirstOrDefault(
					(Func<GameObject, bool>)((GameObject p) => (Object)(object)p != (Object)null && ((Object)p).name == name));
				CraftingStation station = ResolveStation(prefab);
				if ((Object)(object)station != (Object)null)
				{
					return station;
				}
			}
		}
		ObjectDB db = ObjectDB.instance;
		if ((Object)(object)db != (Object)null)
		{
			foreach (string name in names)
			{
				CraftingStation station = ResolveStation(db.GetItemPrefab(name));
				if ((Object)(object)station != (Object)null)
				{
					return station;
				}
			}
		}
		return null;
	}

	private static CraftingStation ResolveStation(GameObject prefab)
	{
		if ((Object)(object)prefab == (Object)null)
		{
			return null;
		}
		CraftingStation station = prefab.GetComponent<CraftingStation>();
		if ((Object)(object)station == (Object)null)
		{
			station = prefab.GetComponentInChildren<CraftingStation>(true);
		}
		return station;
	}
}
