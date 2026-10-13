using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoxPlate;

[BepInPlugin("benjamin.loxplate", "LoxPlate", "0.1.0")]
public sealed class LoxPlatePlugin : BaseUnityPlugin
{
	internal const string LoxPrefabName = "Lox";

	internal static ManualLogSource Log;

	private Harmony _harmony;

	internal static ConfigEntry<bool> AllowAnyTame;

	internal static ConfigEntry<bool> RecoverOnDeath;

	internal static ConfigEntry<float> BlackMetalDamageReduction;

	internal static ConfigEntry<float> BlackMetalHealthPool;

	internal static ConfigEntry<float> BlackMetalSpeedPenalty;

	internal static ConfigEntry<float> FlametalDamageReduction;

	internal static ConfigEntry<float> FlametalHealthPool;

	internal static ConfigEntry<float> FlametalSpeedPenalty;

	internal static ConfigEntry<float> RegenPerSecond;

	internal static ConfigEntry<float> RegenDelaySeconds;

	internal static ConfigEntry<float> DurabilityPerAbsorb;

	private void Awake()
	{
		Log = Logger;
		AllowAnyTame = Config.Bind("General", "AllowAnyTame", false,
			"Let other tameable animals (wolves, boars, ...) wear barding too, not just loxes.");
		RecoverOnDeath = Config.Bind("General", "RecoverOnDeath", true,
			"When a barded animal dies, its barding drops with the corpse instead of being lost.");
		BlackMetalDamageReduction = Config.Bind("BlackMetalBarding", "DamageReduction", 0.25f,
			new ConfigDescription("Fraction of incoming damage the Black Metal barding ignores (0-0.9).",
				new AcceptableValueRange<float>(0f, 0.9f)));
		BlackMetalHealthPool = Config.Bind("BlackMetalBarding", "HealthPool", 300f,
			new ConfigDescription("Bonus damage pool the Black Metal barding absorbs before the lox is hurt (0-5000).",
				new AcceptableValueRange<float>(0f, 5000f)));
		BlackMetalSpeedPenalty = Config.Bind("BlackMetalBarding", "SpeedPenalty", 0.05f,
			new ConfigDescription("Movement speed penalty on the barded animal (0-0.5).",
				new AcceptableValueRange<float>(0f, 0.5f)));
		FlametalDamageReduction = Config.Bind("FlametalBarding", "DamageReduction", 0.35f,
			new ConfigDescription("Fraction of incoming damage the Flametal barding ignores (0-0.9).",
				new AcceptableValueRange<float>(0f, 0.9f)));
		FlametalHealthPool = Config.Bind("FlametalBarding", "HealthPool", 600f,
			new ConfigDescription("Bonus damage pool the Flametal barding absorbs before the lox is hurt (0-5000).",
				new AcceptableValueRange<float>(0f, 5000f)));
		FlametalSpeedPenalty = Config.Bind("FlametalBarding", "SpeedPenalty", 0.05f,
			new ConfigDescription("Movement speed penalty on the barded animal (0-0.5).",
				new AcceptableValueRange<float>(0f, 0.5f)));
		RegenPerSecond = Config.Bind("Pool", "RegenPerSecond", 5f,
			new ConfigDescription("Health pool regeneration per second while out of combat (0-100).",
				new AcceptableValueRange<float>(0f, 100f)));
		RegenDelaySeconds = Config.Bind("Pool", "RegenDelaySeconds", 30f,
			new ConfigDescription("Seconds without taking damage before the pool starts regenerating (0-600).",
				new AcceptableValueRange<float>(0f, 600f)));
		DurabilityPerAbsorb = Config.Bind("Barding", "DurabilityPerAbsorb", 1f,
			new ConfigDescription("Durability the barding loses per hit it absorbs. At zero durability it only gives flat damage reduction until repaired at a forge. 0 disables wear.",
				new AcceptableValueRange<float>(0f, 10f)));
		Config.Save();
		_harmony = new Harmony("benjamin.loxplate");
		_harmony.PatchAll(typeof(LoxPlatePlugin).Assembly);
		Logger.LogInfo($"LoxPlate {Info.Metadata.Version} loading (armor barding for tamed loxes; multiplayer unverified)");
	}

	private void OnDestroy()
	{
		if (_harmony != null)
		{
			_harmony.UnpatchSelf();
		}
	}

	internal static float DamageReduction(int tier)
	{
		float value = tier == BardingItems.TierFlametal ? FlametalDamageReduction.Value : BlackMetalDamageReduction.Value;
		return Mathf.Clamp(value, 0f, 0.9f);
	}

	internal static float MaxPool(int tier)
	{
		float value = tier == BardingItems.TierFlametal ? FlametalHealthPool.Value : BlackMetalHealthPool.Value;
		return Mathf.Clamp(value, 0f, 5000f);
	}

	internal static float SpeedPenalty(int tier)
	{
		float value = tier == BardingItems.TierFlametal ? FlametalSpeedPenalty.Value : BlackMetalSpeedPenalty.Value;
		return Mathf.Clamp(value, 0f, 0.5f);
	}

	[HarmonyPatch(typeof(ZNetScene), "Awake")]
	private static class RegisterNetworkItems
	{
		private static void Postfix(ZNetScene __instance)
		{
			BardingItems.RegisterNetwork(__instance);
			// The scene prefab list is ready at this point even when the item
			// database finished its Awake first; a null ObjectDB (menu) is a
			// harmless no-op that later hooks retry.
			BardingItems.RegisterDatabase(ObjectDB.instance);
			BardingDiagnostics.DumpLoxPrefab(__instance);
		}
	}

	[HarmonyPatch(typeof(ZNet), "Start")]
	private static class RegisterItemsAtNetStart
	{
		private static void Postfix()
		{
			// Dedicated servers can finish scene load without ever calling
			// ObjectDB.CopyOtherDB; net start is the guaranteed "world and
			// databases ready" moment for both clients and servers.
			BardingItems.RegisterDatabase(ObjectDB.instance);
		}
	}

	[HarmonyPatch(typeof(ObjectDB), "Awake")]
	private static class RegisterItems
	{
		private static void Postfix(ObjectDB __instance)
		{
			BardingItems.RegisterDatabase(__instance);
		}
	}

	[HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
	private static class RegisterItemsAfterCopy
	{
		private static void Postfix(ObjectDB __instance)
		{
			BardingItems.RegisterDatabase(__instance);
		}
	}

	[HarmonyPatch(typeof(Tameable), "Awake")]
	private static class AttachBardingComponent
	{
		private static void Postfix(Tameable __instance)
		{
			try
			{
				if ((Object)(object)((Component)__instance).GetComponent<LoxBarding>() == (Object)null)
				{
					((Component)__instance).gameObject.AddComponent<LoxBarding>();
				}
			}
			catch (Exception ex)
			{
				Log.LogError("Could not attach barding component: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(Tameable), "UseItem")]
	private static class FitOrRemoveBarding
	{
		private static bool Prefix(Tameable __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
		{
			try
			{
				return HandleUse(__instance, user, item, ref __result);
			}
			catch (Exception ex)
			{
				Log.LogError("Barding use failed: " + ex);
				return true;
			}
		}

		private static bool HandleUse(Tameable tame, Humanoid user, ItemDrop.ItemData item, ref bool __result)
		{
			if (item == null || (Object)(object)user == (Object)null)
			{
				return true;
			}
			LoxBarding barding = ((Component)tame).GetComponent<LoxBarding>();
			if ((Object)(object)barding == (Object)null)
			{
				return true;
			}
			int held = BardingItems.TierOf(item);
			int current = barding.Tier;
			bool remove = held == BardingItems.TierNone && current != BardingItems.TierNone
				&& user.IsCrouching();
			if (held == BardingItems.TierNone && !remove)
			{
				return true;
			}
			__result = true;
			if (remove)
			{
				barding.RequestUnequip();
				user.Message(MessageHud.MessageType.Center, "Barding removed");
				return false;
			}
			Character character = ((Component)tame).GetComponent<Character>();
			if ((Object)(object)character == (Object)null || character.IsDead())
			{
				return true;
			}
			if (!AllowAnyTame.Value
				&& Utils.GetPrefabName(((Component)tame).gameObject) != LoxPrefabName)
			{
				user.Message(MessageHud.MessageType.Center, "Barding fits loxes only");
				return false;
			}
			if (!tame.IsTamed())
			{
				user.Message(MessageHud.MessageType.Center, "The animal must be tamed before it accepts barding");
				return false;
			}
			if (current == held)
			{
				user.Message(MessageHud.MessageType.Center, tame.GetHoverName() + " already wears this barding");
				return false;
			}
			barding.RequestFit(held);
			// Carry the item's wear onto the animal: a damaged set fits as
			// damaged and must be repaired at a forge.
			ZNetView tameNet = ((Component)tame).GetComponent<ZNetView>();
			if ((Object)(object)tameNet != (Object)null && tameNet.IsValid())
			{
				tameNet.GetZDO().Set(LoxBarding.DurabilityKey,
					Mathf.Clamp(item.m_durability, 0f, LoxBarding.MaxDurability));
			}
			user.GetInventory().RemoveOneItem(item);
			user.Message(MessageHud.MessageType.Center, item.m_shared.m_name + " fitted");
			return false;
		}
	}

	[HarmonyPatch(typeof(Tameable), "Interact")]
	private static class RemoveBardingOnCrouchUse
	{
		// Empty-handed Use reaches Tameable.Interact (vanilla pet/command),
		// not UseItem - so crouch + Use removal must hook this path too, or
		// it fights the pet interaction. Alt+Use (rename) keeps vanilla
		// precedence; hold-Use is ignored as in vanilla.
		private static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
		{
			try
			{
				return HandleInteract(__instance, user, hold, alt, ref __result);
			}
			catch (Exception ex)
			{
				Log.LogWarning("barding removal failed: " + ex);
				return true;
			}
		}

		private static bool HandleInteract(Tameable tame, Humanoid user, bool hold, bool alt, ref bool __result)
		{
			if (hold || alt || (Object)(object)user == (Object)null)
			{
				return true;
			}
			LoxBarding barding = ((Component)tame).GetComponent<LoxBarding>();
			if ((Object)(object)barding == (Object)null || !barding.Active)
			{
				return true;
			}
			if (!user.IsCrouching())
			{
				return true;
			}
			barding.RequestUnequip();
			user.Message(MessageHud.MessageType.Center, "Barding removed");
			__result = true;
			return false;
		}
	}

	[HarmonyPatch(typeof(Tameable), "GetHoverText")]
	private static class BardingHoverHint
	{
		private static void Postfix(Tameable __instance, ref string __result)
		{
			try
			{
				LoxBarding barding = ((Component)__instance).GetComponent<LoxBarding>();
				if ((Object)(object)barding == (Object)null || !barding.Active)
				{
					return;
				}
				int pool = Mathf.RoundToInt(barding.Pool);
				int max = Mathf.RoundToInt(LoxPlatePlugin.MaxPool(barding.Tier));
				int durability = Mathf.RoundToInt(barding.Durability);
				string wear = durability <= 0
					? " — worn out (repair at a forge)"
					: $" · {durability}%";
				__result += $"\nBarding: {pool} / {max}{wear}"
					+ "\n[<color=yellow><b>Crouch + Use</b></color>] Remove barding";
			}
			catch (Exception)
			{
			}
		}
	}

	[HarmonyPatch(typeof(Character), "ApplyDamage")]
	private static class BardingProtects
	{
		private static bool _ratesLogged;

		private static void Prefix(Character __instance, HitData hit)
		{
			// Contained so a bug here can never skip the original method and
			// nullify damage globally (a throwing Harmony prefix does exactly
			// that - learned the hard way).
			try
			{
				HandleHit(__instance, hit);
			}
			catch (Exception ex)
			{
				Log.LogWarning($"barding hit handling failed: {ex}");
			}
		}

		private static void HandleHit(Character __instance, HitData hit)
		{
			// HitData is a plain class - a plain null check, NOT the
			// UnityEngine.Object cast idiom (which throws for it).
			if (hit == null)
			{
				return;
			}
			if (!_ratesLogged && Game.instance != null)
			{
				_ratesLogged = true;
				Log.LogInfo($"damage rates: playerDamageRate={Game.m_playerDamageRate} "
					+ $"enemyDamageRate={Game.m_enemyDamageRate} localDamageTakenRate={Game.m_localDamgeTakenRate}");
			}
			float total = hit.GetTotalDamage();
			Log.LogDebug($"hit: victim={__instance.name} player={__instance.IsPlayer()} incoming={total:F1}");
			LoxBarding barding = ((Component)__instance).GetComponent<LoxBarding>();
			if ((Object)(object)barding == (Object)null)
			{
				return;
			}
			int tier = barding.Tier;
			if (tier == BardingItems.TierNone)
			{
				return;
			}
			bool active = barding.Active;
			float poolBefore = barding.Pool;
			Log.LogInfo($"barding hit: victim={__instance.m_name} tier={tier} active={active} "
				+ $"pool_before={poolBefore:F0} incoming={total:F1}");
			if (!active)
			{
				return;
			}
			barding.ApplyProtection(hit);
			Log.LogInfo($"barding hit: after -> outgoing={hit.GetTotalDamage():F1} pool_now={barding.Pool:F0}");
		}
	}

	[HarmonyPatch(typeof(Character), "GetJogSpeedFactor")]
	private static class BardingJogSpeed
	{
		private static void Postfix(Character __instance, ref float __result)
		{
			ApplySpeedPenalty(__instance, ref __result);
		}
	}

	[HarmonyPatch(typeof(Character), "GetRunSpeedFactor")]
	private static class BardingRunSpeed
	{
		private static void Postfix(Character __instance, ref float __result)
		{
			ApplySpeedPenalty(__instance, ref __result);
		}
	}

	private static void ApplySpeedPenalty(Character character, ref float speedFactor)
	{
		LoxBarding barding = ((Component)character).GetComponent<LoxBarding>();
		if ((Object)(object)barding == (Object)null || !barding.Active)
		{
			return;
		}
		float penalty = SpeedPenalty(barding.Tier);
		if (penalty > 0f)
		{
			speedFactor *= 1f - penalty;
		}
	}

	[HarmonyPatch(typeof(Character), "OnDeath")]
	private static class BardingDropsOnDeath
	{
		private static void Postfix(Character __instance)
		{
			try
			{
				LoxBarding barding = ((Component)__instance).GetComponent<LoxBarding>();
				if ((Object)(object)barding == (Object)null || !barding.IsNetValid)
				{
					return;
				}
				int tier = barding.Tier;
				if (tier == BardingItems.TierNone || !barding.IsOwner)
				{
					return;
				}
				if (RecoverOnDeath.Value)
				{
					barding.DropBarding(tier, barding.Durability);
				}
				barding.ClearState();
				Log.LogDebug($"Barding tier {tier} removed on death of {__instance.m_name}");
			}
			catch (Exception ex)
			{
				Log.LogError("Barding death drop failed: " + ex);
			}
		}
	}
}
