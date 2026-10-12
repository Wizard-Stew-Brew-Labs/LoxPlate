using System;

namespace LoxPlate;

/// <summary>
/// Pure damage-routing math for lox barding. Unity-free so the test project
/// compiles this file directly and pins the numbers the mod ships with.
///
/// Protection model: incoming damage is first scaled by (1 - damageReduction);
/// the barding's health pool then absorbs what is left, and only the overflow
/// reaches the lox. The pool regenerates at a fixed rate once the lox has gone
/// regenerationDelay seconds without taking any (post-reduction) damage.
/// </summary>
public static class BardingMath
{
	public static float Clamp(float value, float min, float max)
	{
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	/// <summary>How much of the incoming damage the pool soaks up.</summary>
	public static float Absorb(float pool, float damage)
	{
		return Clamp(Math.Min(Math.Max(pool, 0f), Math.Max(damage, 0f)), 0f, float.MaxValue);
	}

	/// <summary>
	/// HitData multiplier that leaves (damage - absorbed) of the original hit.
	/// A full absorption yields 0 (vanilla then skips the hit entirely).
	/// </summary>
	public static float RemainderFactor(float damage, float absorbed)
	{
		if (damage <= 0.0001f)
		{
			return 0f;
		}
		return Clamp((damage - absorbed) / damage, 0f, 1f);
	}

	/// <summary>
	/// Pool value after lazy regeneration. Returns the clamped pool unchanged
	/// when the lox was harmed more recently than the delay, when the rate is
	/// zero, or when world time moved backwards (save rollback).
	/// </summary>
	public static float Regenerate(float pool, float max, float secondsSinceLastHarm, float delaySeconds, float ratePerSecond)
	{
		pool = Clamp(pool, 0f, max);
		if (ratePerSecond <= 0f || secondsSinceLastHarm <= delaySeconds || secondsSinceLastHarm < 0f)
		{
			return pool;
		}
		return Clamp(pool + (secondsSinceLastHarm - delaySeconds) * ratePerSecond, 0f, max);
	}
}
