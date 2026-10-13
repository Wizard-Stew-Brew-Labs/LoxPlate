using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoxPlate;

/// <summary>
/// Attached to tameable animals by a Tameable.Awake postfix. Owns the
/// barding state on the animal's ZDO (tier, remaining pool, last-harm world
/// time), the fit/remove RPC, damage protection and the visual shell.
/// The ZDO keys survive save/load, so barding persists across sessions, and
/// the tier syncs to every peer, so late joiners rebuild the visuals too.
/// </summary>
public sealed class LoxBarding : MonoBehaviour
{
	internal const string RpcFit = "lb_fit";

	internal static readonly int TierKey = StringExtensionMethods.GetStableHashCode("lb_tier");
	internal static readonly int PoolKey = StringExtensionMethods.GetStableHashCode("lb_pool");
	internal static readonly int LastHitKey = StringExtensionMethods.GetStableHashCode("lb_lastHit");
	internal static readonly int DurabilityKey = StringExtensionMethods.GetStableHashCode("lb_durability");

	/// <summary>Barding durability ceiling; matches the item's max durability.</summary>
	internal const float MaxDurability = 100f;

	private ZNetView _net;
	private Tameable _tame;
	private Character _character;

	private GameObject _visual;
	private int _lastAppliedTier = -1;
	private float _nextTierCheck;
	private float _nextRegenTick;

	internal ZNetView Net => _net;

	internal bool IsNetValid => (Object)(object)_net != (Object)null && _net.IsValid();

	internal bool IsOwner => IsNetValid && _net.IsOwner();

	/// <summary>True when this animal currently wears barding and can be protected.</summary>
	internal bool Active
	{
		get
		{
			if (!IsNetValid || (Object)(object)_character == (Object)null || _character.IsDead())
			{
				return false;
			}
			return Tier > BardingItems.TierNone;
		}
	}

	internal int Tier
	{
		get
		{
			if (!IsNetValid)
			{
				return BardingItems.TierNone;
			}
			return _net.GetZDO().GetInt(TierKey, BardingItems.TierNone);
		}
	}

	internal float Pool
	{
		get
		{
			if (!IsNetValid)
			{
				return 0f;
			}
			return _net.GetZDO().GetFloat(PoolKey, LoxPlatePlugin.MaxPool(Tier));
		}
	}

	internal float Durability
	{
		get
		{
			if (!IsNetValid)
			{
				return MaxDurability;
			}
			return Mathf.Clamp(_net.GetZDO().GetFloat(DurabilityKey, MaxDurability), 0f, MaxDurability);
		}
	}

	private void Awake()
	{
		_net = ((Component)this).GetComponent<ZNetView>();
		_tame = ((Component)this).GetComponent<Tameable>();
		_character = ((Component)this).GetComponent<Character>();
		if ((Object)(object)_net != (Object)null)
		{
			_net.Register<int, bool>(RpcFit, OnFitRequest);
		}
	}

	private void Update()
	{
		if ((Object)(object)_net == (Object)null)
		{
			return;
		}
		if (Time.time >= _nextTierCheck)
		{
			_nextTierCheck = Time.time + 0.4f;
			if (_net.IsValid())
			{
				int tier = Tier;
				if (tier != _lastAppliedTier)
				{
					ApplyVisual(tier);
				}
			}
		}
		if (Time.time >= _nextRegenTick)
		{
			_nextRegenTick = Time.time + 1f;
			RegenerationTick();
		}
	}

	/// <summary>Owner-side periodic regeneration: the pool climbs in the ZDO
	/// (and therefore in the hover readout) while the animal stays out of
	/// combat, instead of only being recomputed retroactively on the next
	/// hit. Damage ticks (e.g. poison) keep resetting the delay, by design.
	/// </summary>
	private void RegenerationTick()
	{
		if (!IsOwner || (Object)(object)_character == (Object)null || _character.IsDead())
		{
			return;
		}
		int tier = Tier;
		if (tier == BardingItems.TierNone)
		{
			return;
		}
		float max = LoxPlatePlugin.MaxPool(tier);
		float pool = _net.GetZDO().GetFloat(PoolKey, max);
		if (pool >= max)
		{
			return;
		}
		double worldTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
		float lastHit = _net.GetZDO().GetFloat(LastHitKey, (float)worldTime);
		float regenerated = BardingMath.Regenerate(pool, max,
			(float)(worldTime - (double)lastHit),
			LoxPlatePlugin.RegenDelaySeconds.Value, LoxPlatePlugin.RegenPerSecond.Value);
		if (regenerated > pool + 0.01f)
		{
			_net.GetZDO().Set(PoolKey, regenerated);
		}
	}

	/// <summary>Player-initiated fit request (from the Tameable.UseItem prefix).
	/// Replacing an existing barding returns the previous one to the requesting
	/// player's inventory when possible.</summary>
	internal void RequestFit(int tier)
	{
		if (IsNetValid)
		{
			_net.InvokeRPC(RpcFit, tier, true);
		}
	}

	/// <summary>Player-initiated removal (crouch + Use on a barded lox).</summary>
	internal void RequestUnequip()
	{
		if (IsNetValid)
		{
			_net.InvokeRPC(RpcFit, BardingItems.TierNone, true);
		}
	}

	private void OnFitRequest(long sender, int tier, bool wantPickup)
	{
		if (!IsOwner || (tier != BardingItems.TierNone && tier != BardingItems.TierBlackMetal && tier != BardingItems.TierFlametal))
		{
			return;
		}
		int current = Tier;
		if (tier == BardingItems.TierNone)
		{
			if (current == BardingItems.TierNone)
			{
				return;
			}
			float durability = GetFloat(DurabilityKey, MaxDurability);
			if (!ReturnToInventory(sender, wantPickup, current, durability))
			{
				DropBarding(current, durability);
			}
			SetTier(BardingItems.TierNone);
			_net.GetZDO().Set(DurabilityKey, MaxDurability);
			return;
		}
		if ((Object)(object)_tame == (Object)null || !_tame.IsTamed()
			|| (Object)(object)_character == (Object)null || _character.IsDead())
		{
			return;
		}
		if (current != BardingItems.TierNone && current != tier)
		{
			// Replacing one tier with another returns the previous set
			// (with whatever wear it had accumulated).
			float durability = GetFloat(DurabilityKey, MaxDurability);
			if (!ReturnToInventory(sender, wantPickup, current, durability))
			{
				DropBarding(current, durability);
			}
		}
		double worldTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
		SetTier(tier);
		_net.GetZDO().Set(PoolKey, LoxPlatePlugin.MaxPool(tier));
		_net.GetZDO().Set(LastHitKey, (float)worldTime);
		_net.GetZDO().Set(DurabilityKey, Mathf.Clamp(GetFloat(DurabilityKey, MaxDurability), 0f, MaxDurability));
		ApplyVisual(tier);
	}

	/// <summary>When the removal/swap was requested by this machine's player,
	/// put the barding straight into their inventory (durability preserved).
	/// Falls back to a ground drop when the requester is another peer, the
	/// inventory is full, or there is no local player (dedicated server).</summary>
	private bool ReturnToInventory(long sender, bool wantPickup, int tier, float durability)
	{
		if (!wantPickup || (Object)(object)Player.m_localPlayer == (Object)null
			|| sender != ZNet.GetUID())
		{
			return false;
		}
		GameObject item = MakeDrop(tier, durability);
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		ItemDrop drop = item.GetComponent<ItemDrop>();
		if ((Object)(object)drop == (Object)null)
		{
			return false;
		}
		if (Player.m_localPlayer.GetInventory().AddItem(drop.m_itemData))
		{
			// The ItemData instance now lives in the inventory (vanilla
			// Pickup destroys ZDO-less drops the same way).
			Object.Destroy(item);
			LoxPlatePlugin.Log.LogInfo("Barding returned to inventory");
			return true;
		}
		// No room: leave the drop in the world with a visible pop.
		Rigidbody body = item.GetComponent<Rigidbody>();
		if ((Object)(object)body != (Object)null)
		{
			body.AddForce(Vector3.up * 4f, ForceMode.VelocityChange);
		}
		return false;
	}

	/// <summary>Called on the damage path before the hit is applied: lazy pool
	/// regeneration, then flat damage reduction, then pool absorption.</summary>
	internal void ApplyProtection(HitData hit)
	{
		if (!IsNetValid || hit == null)
		{
			return;
		}
		int tier = Tier;
		if (tier == BardingItems.TierNone)
		{
			return;
		}
		double worldTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
		float lastHit = _net.GetZDO().GetFloat(LastHitKey, (float)worldTime);
		float pool = _net.GetZDO().GetFloat(PoolKey, LoxPlatePlugin.MaxPool(tier));
		pool = BardingMath.Regenerate(pool, LoxPlatePlugin.MaxPool(tier),
			(float)(worldTime - (double)lastHit), LoxPlatePlugin.RegenDelaySeconds.Value,
			LoxPlatePlugin.RegenPerSecond.Value);
		float reduction = LoxPlatePlugin.DamageReduction(tier);
		if (reduction > 0f)
		{
			hit.ApplyModifier(1f - reduction);
		}
		// Worn-out barding keeps its flat reduction but can no longer absorb.
		float durability = Mathf.Clamp(GetFloat(DurabilityKey, MaxDurability), 0f, MaxDurability);
		float total = hit.GetTotalDamage();
		if (total > 0f && durability > 0f)
		{
			float absorbed = BardingMath.Absorb(pool, total);
			if (absorbed > 0f)
			{
				pool -= absorbed;
				float remainder = total - absorbed;
				hit.ApplyModifier(BardingMath.RemainderFactor(total, absorbed));
				if (remainder <= 0.01f && (Object)(object)DamageText.instance != (Object)null)
				{
					// Vanilla skips its own damage text when a hit lands at
					// zero, which made fully-absorbed hits look like the lox
					// was taking no damage at all. Show the soaked amount as
					// an immune-styled number instead (broadcasts to peers).
					DamageText.instance.ShowText(HitData.DamageModifier.Immune, hit.m_point, absorbed, true);
				}
			}
		}
		_net.GetZDO().Set(PoolKey, pool);
		_net.GetZDO().Set(LastHitKey, (float)worldTime);
		if (total > 0f && durability > 0f && LoxPlatePlugin.DurabilityPerAbsorb.Value > 0f)
		{
			// Every hit the barding soaks wears it down; repair the item at
			// a forge to restore full protection.
			_net.GetZDO().Set(DurabilityKey,
				Mathf.Max(0f, durability - LoxPlatePlugin.DurabilityPerAbsorb.Value));
		}
	}

	/// <summary>Spawn the barding item back into the world (unequip / death),
	/// carrying its remaining durability so the forge repair means something.</summary>
	internal void DropBarding(int tier, float durability)
	{
		GameObject item = MakeDrop(tier, durability);
		if ((Object)(object)item == (Object)null)
		{
			return;
		}
		Rigidbody body = item.GetComponent<Rigidbody>();
		if ((Object)(object)body != (Object)null)
		{
			body.AddForce(Vector3.up * 4f, ForceMode.VelocityChange);
		}
	}

	/// <summary>Instantiate the barding item drop near the animal with its
	/// stored durability, without any initial impulse.</summary>
	private GameObject MakeDrop(int tier, float durability)
	{
		GameObject prefab = BardingItems.PrefabOf(tier);
		if ((Object)(object)prefab == (Object)null)
		{
			LoxPlatePlugin.Log.LogWarning($"Cannot drop barding tier {tier}: prefab not registered");
			return null;
		}
		Vector3 center = (Object)(object)_character != (Object)null
			? _character.GetCenterPoint()
			: ((Component)this).transform.position + Vector3.up;
		GameObject item = Object.Instantiate(prefab, center + Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward * 0.4f,
			Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
		item.name = prefab.name;
		ItemDrop drop = item.GetComponent<ItemDrop>();
		if ((Object)(object)drop != (Object)null)
		{
			drop.m_itemData.m_durability = Mathf.Clamp(durability, 0f, MaxDurability);
			drop.m_itemData.m_stack = 1;
			drop.m_itemData.m_worldLevel = (byte)(Game.instance != null ? Game.m_worldLevel : 0);
		}
		return item;
	}

	internal void ClearState()
	{
		if (IsNetValid)
		{
			SetTier(BardingItems.TierNone);
			_net.GetZDO().Set(DurabilityKey, MaxDurability);
		}
		if ((Object)(object)_visual != (Object)null)
		{
			Object.Destroy(_visual);
			_visual = null;
		}
		_lastAppliedTier = BardingItems.TierNone;
	}

	private void SetTier(int tier)
	{
		_net.GetZDO().Set(TierKey, tier);
	}

	private float GetFloat(int key, float defaultValue)
	{
		return _net.GetZDO().GetFloat(key, defaultValue);
	}

	private void ApplyVisual(int tier)
	{
		_lastAppliedTier = tier;
		if ((Object)(object)_visual != (Object)null)
		{
			Object.Destroy(_visual);
			_visual = null;
		}
		if (tier == BardingItems.TierNone || (Object)(object)_character == (Object)null || _character.IsDead())
		{
			return;
		}
		try
		{
			_visual = BardingVisuals.Build(_tame, _character, tier);
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogWarning($"Barding visual failed for tier {tier}: {ex.Message}");
		}
	}
}
