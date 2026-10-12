using System;
using System.Collections.Generic;
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

	private ZNetView _net;
	private Tameable _tame;
	private Character _character;

	private GameObject _visual;
	private int _lastAppliedTier = -1;
	private float _nextTierCheck;

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

	private void Awake()
	{
		_net = ((Component)this).GetComponent<ZNetView>();
		_tame = ((Component)this).GetComponent<Tameable>();
		_character = ((Component)this).GetComponent<Character>();
		if ((Object)(object)_net != (Object)null)
		{
			_net.Register<int>(RpcFit, OnFitRequest);
		}
	}

	private void Update()
	{
		if ((Object)(object)_net == (Object)null || Time.time < _nextTierCheck)
		{
			return;
		}
		_nextTierCheck = Time.time + 0.4f;
		if (!_net.IsValid())
		{
			return;
		}
		int tier = Tier;
		if (tier == _lastAppliedTier)
		{
			return;
		}
		ApplyVisual(tier);
	}

	/// <summary>Player-initiated fit request (from the Tameable.UseItem prefix).</summary>
	internal void RequestFit(int tier)
	{
		if (IsNetValid)
		{
			_net.InvokeRPC(RpcFit, tier);
		}
	}

	/// <summary>Player-initiated removal (crouch + Use on a barded lox).</summary>
	internal void RequestUnequip()
	{
		RequestFit(BardingItems.TierNone);
	}

	private void OnFitRequest(long sender, int tier)
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
			DropBarding(current);
			SetTier(BardingItems.TierNone);
			return;
		}
		if ((Object)(object)_tame == (Object)null || !_tame.IsTamed()
			|| (Object)(object)_character == (Object)null || _character.IsDead())
		{
			return;
		}
		if (current != BardingItems.TierNone && current != tier)
		{
			// Replacing one tier with another returns the previous set.
			DropBarding(current);
		}
		double worldTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
		SetTier(tier);
		_net.GetZDO().Set(PoolKey, LoxPlatePlugin.MaxPool(tier));
		_net.GetZDO().Set(LastHitKey, (float)worldTime);
		ApplyVisual(tier);
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
		float total = hit.GetTotalDamage();
		if (total > 0f)
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
	}

	/// <summary>Spawn the barding item back into the world (unequip / death).</summary>
	internal void DropBarding(int tier)
	{
		GameObject prefab = BardingItems.PrefabOf(tier);
		if ((Object)(object)prefab == (Object)null)
		{
			LoxPlatePlugin.Log.LogWarning($"Cannot drop barding tier {tier}: prefab not registered");
			return;
		}
		Vector3 center = (Object)(object)_character != (Object)null
			? _character.GetCenterPoint()
			: ((Component)this).transform.position + Vector3.up;
		CharacterDrop.DropItems(new List<KeyValuePair<GameObject, int>>
		{
			new KeyValuePair<GameObject, int>(prefab, 1),
		}, center, 0.5f, false);
	}

	internal void ClearState()
	{
		if (IsNetValid)
		{
			SetTier(BardingItems.TierNone);
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
			_visual = BardingVisuals.Build(((Component)_character).transform, tier);
			if (_lastAppliedTier == BardingItems.TierBlackMetal || tier == BardingItems.TierFlametal)
			{
				LogShellBounds(tier);
			}
		}
		catch (Exception ex)
		{
			LoxPlatePlugin.Log.LogWarning($"Barding visual failed for tier {tier}: {ex.Message}");
		}
	}

	/// <summary>One-off diagnostics for tuning the shell offsets against the
	/// live lox model (visible in the server log during dev testing).</summary>
	private void LogShellBounds(int tier)
	{
		Renderer[] renderers = ((Component)_character).GetComponentsInChildren<Renderer>();
		if (renderers.Length == 0)
		{
			return;
		}
		Bounds bounds = renderers[0].bounds;
		foreach (Renderer renderer in renderers)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		Vector3 localMin = ((Component)_character).transform.InverseTransformPoint(bounds.min);
		Vector3 localMax = ((Component)_character).transform.InverseTransformPoint(bounds.max);
		LoxPlatePlugin.Log.LogDebug($"Barding tier {tier} fitted; lox local bounds min={localMin} max={localMax}");
	}
}
