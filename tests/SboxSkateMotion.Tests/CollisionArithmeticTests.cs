using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionArithmeticTests
{
	[Fact]
	public void TinyOpposingTermRoundsTowardZeroAcrossLargeExponentGap()
	{
		Assert.Equal( 0x3f7fffffu, CollisionArithmetic.Subtract( 0x3f800000u, 0x00800000u ) );
		Assert.Equal( 0xbf7fffffu, CollisionArithmetic.Add( 0xbf800000u, 0x00800000u ) );
	}

	[Fact]
	public void MultiplyAddRoundsProductBeforeCancellation()
	{
		Assert.Equal( 0u, CollisionArithmetic.MultiplyAdd( 0x3f800001u, 0x3f800001u, 0xbf800002u ) );
		Assert.Equal( 0u, CollisionArithmetic.NegativeMultiplySubtract( 0x3f800001u, 0x3f800001u, 0x3f800002u ) );
	}

	[Fact]
	public void SubnormalOperandsFlushBeforeComparisonAndArithmetic()
	{
		Assert.False( CollisionArithmetic.Greater( 0x007fffffu, 0u ) );
		Assert.False( CollisionArithmetic.Greater( 0u, 0x807fffffu ) );
		Assert.Equal( 0x3f800000u, CollisionArithmetic.Subtract( 0x3f800000u, 0x007fffffu ) );
		Assert.Equal( 0u, CollisionArithmetic.Multiply( 0x80800000u, 0x3f000000u ) );
	}

	[Fact]
	public void ExtendedMultiplicationUsesDifferentRulesFromMultiplyAdd()
	{
		Assert.Equal( 0xffffffffu, CollisionArithmetic.Multiply( 0x7fffffffu, 0xbf800000u ) );
		Assert.Equal( 0u, CollisionArithmetic.Multiply( 0x7fffffffu, 0x80000000u ) );
		Assert.Equal( 0x3f800000u, CollisionArithmetic.MultiplyAdd( 0x7fffffffu, 0x3f800000u, 0x3f800000u ) );
	}

	[Fact]
	public void ZeroRootEstimateCanBeConsumedWithoutNativeNanMultiplication()
	{
		var inverseRoot = CollisionArithmetic.Interpolate( 0u, CollisionArithmetic.RootEstimate( 0u ) );
		Assert.Equal( 0x7f800000u, inverseRoot & 0x7f800000u );
		Assert.Equal( 0u, CollisionArithmetic.Multiply( 0u, inverseRoot ) );
	}
}
