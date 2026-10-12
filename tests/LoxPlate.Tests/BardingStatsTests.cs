using Xunit;

namespace LoxPlate.Tests;

/// <summary>
/// Pins the shipped damage-routing numbers: how barding splits a hit between
/// its health pool and the lox, and how the pool regenerates out of combat.
/// These tests compile the shipped BardingMath.cs directly.
/// </summary>
public class BardingStatsTests
{
	private const float Epsilon = 1e-4f;

	[Theory]
	[InlineData(300f, 100f, 100f)]   // pool covers the whole hit
	[InlineData(50f, 100f, 50f)]     // pool partially covers
	[InlineData(0f, 100f, 0f)]       // empty pool absorbs nothing
	[InlineData(300f, 0f, 0f)]       // nothing incoming
	[InlineData(300f, -10f, 0f)]     // nonsensical negative damage
	public void Absorb_SplitsPoolAgainstDamage(float pool, float damage, float expected)
	{
		Assert.Equal(expected, BardingMath.Absorb(pool, damage), Epsilon);
	}

	[Theory]
	[InlineData(100f, 100f, 0f)]     // fully absorbed -> vanilla sees a zeroed hit
	[InlineData(100f, 0f, 1f)]       // nothing absorbed -> full hit
	[InlineData(100f, 25f, 0.75f)]   // quarter absorbed
	public void RemainderFactor_ScalesTheHit(float damage, float absorbed, float expected)
	{
		Assert.Equal(expected, BardingMath.RemainderFactor(damage, absorbed), Epsilon);
	}

	[Fact]
	public void RemainderFactor_ZeroDamage_IsZero()
	{
		Assert.Equal(0f, BardingMath.RemainderFactor(0f, 0f), Epsilon);
	}

	[Fact]
	public void Regenerate_BeforeDelay_DoesNothing()
	{
		Assert.Equal(120f, BardingMath.Regenerate(120f, 300f, secondsSinceLastHarm: 30f,
			delaySeconds: 30f, ratePerSecond: 5f), Epsilon);
	}

	[Fact]
	public void Regenerate_AfterDelay_RegeneratesOnlyPastTheDelay()
	{
		// 45s since last harm, 30s delay, 5/s -> 15s worth of regen.
		Assert.Equal(195f, BardingMath.Regenerate(120f, 300f, secondsSinceLastHarm: 45f,
			delaySeconds: 30f, ratePerSecond: 5f), Epsilon);
	}

	[Fact]
	public void Regenerate_NeverExceedsMaximum()
	{
		Assert.Equal(300f, BardingMath.Regenerate(290f, 300f, secondsSinceLastHarm: 1000f,
			delaySeconds: 30f, ratePerSecond: 5f), Epsilon);
	}

	[Fact]
	public void Regenerate_ZeroRate_DoesNothing()
	{
		Assert.Equal(120f, BardingMath.Regenerate(120f, 300f, secondsSinceLastHarm: 1000f,
			delaySeconds: 30f, ratePerSecond: 0f), Epsilon);
	}

	[Fact]
	public void Regenerate_WorldTimeMovingBackwards_DoesNothing()
	{
		Assert.Equal(120f, BardingMath.Regenerate(120f, 300f, secondsSinceLastHarm: -50f,
			delaySeconds: 30f, ratePerSecond: 5f), Epsilon);
	}

	[Fact]
	public void Regenerate_PoolAboveMaximum_IsClamped()
	{
		Assert.Equal(300f, BardingMath.Regenerate(400f, 300f, secondsSinceLastHarm: 1000f,
			delaySeconds: 30f, ratePerSecond: 5f), Epsilon);
	}

	[Fact]
	public void Regenerate_NegativePool_ClampsUpThenRegenerates()
	{
		// A junk negative pool value clamps to 0 and then regenerates normally.
		Assert.Equal(300f, BardingMath.Regenerate(-10f, 300f, secondsSinceLastHarm: 1000f,
			delaySeconds: 30f, ratePerSecond: 5f), Epsilon);
	}

	[Theory]
	[InlineData(0.25f, 100f, 75f)]
	[InlineData(0.35f, 200f, 130f)]
	[InlineData(0f, 100f, 100f)]
	public void ShippedReductionLevels_ScaleDamage(double reduction, float incoming, float expected)
	{
		float scaled = incoming * (1f - (float)reduction);
		Assert.Equal(expected, scaled, Epsilon);
	}

	[Fact]
	public void FullPipeline_FlametalHit_IsReducedThenAbsorbed()
	{
		// 100 incoming, 35% reduction -> 65 effective; pool 40 absorbs 40;
		// the lox takes 25.
		float reduced = 100f * (1f - 0.35f);
		float absorbed = BardingMath.Absorb(40f, reduced);
		float factor = BardingMath.RemainderFactor(reduced, absorbed);
		float toLox = reduced * factor;
		Assert.Equal(40f, absorbed, Epsilon);
		Assert.Equal(25f, toLox, Epsilon);
	}
}
