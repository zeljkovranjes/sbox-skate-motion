using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionScalarMathTests
{
	[Theory]
	[InlineData( 0x00000000u )]
	[InlineData( 0x80000000u )]
	[InlineData( 0x00000001u )]
	[InlineData( 0x80000001u )]
	[InlineData( 0x007fffffu )]
	[InlineData( 0x807fffffu )]
	public void SubnormalStorageAndSignedZeroSurviveWideningAndNarrowing( uint bits )
	{
		var value = BitConverter.UInt32BitsToSingle( bits );
		Assert.Equal( bits, BitConverter.SingleToUInt32Bits( CollisionScalarMath.Narrow( CollisionScalarMath.Widen( value ) ) ) );
	}

	[Theory]
	[InlineData( .5d, 0x00000000u )]
	[InlineData( 1.5d, 0x00000002u )]
	[InlineData( 2.5d, 0x00000002u )]
	[InlineData( -.5d, 0x80000000u )]
	[InlineData( -1.5d, 0x80000002u )]
	[InlineData( -2.5d, 0x80000002u )]
	public void SubnormalHalfwayValuesRoundToEven( double units, uint expected )
	{
		var value = units * 1.401298464324817e-45;
		Assert.Equal( expected, BitConverter.SingleToUInt32Bits( CollisionScalarMath.Narrow( value ) ) );
	}

	[Fact]
	public void TinyProductsRemainAvailableToFollowingScalarOperations()
	{
		var factor = Math.Pow( 2d, -70 );
		var product = CollisionScalarMath.Product( factor, factor );
		Assert.Equal( Math.Pow( 2d, -140 ), product );
		Assert.Equal( 0x200u, BitConverter.SingleToUInt32Bits( CollisionScalarMath.Narrow( product ) ) );
		Assert.Equal( factor, CollisionScalarMath.SquareRoot( product ) );
		Assert.Equal( 2d, CollisionScalarMath.Divide( product, product * .5d ) );
	}

	[Fact]
	public void FusedAndSeparatelyRoundedProductsRemainDistinct()
	{
		var offset = Math.Pow( 2d, -23 );
		var separate = CollisionScalarMath.Difference( CollisionScalarMath.Product( 1d + offset, 1d - offset ), 1d );
		var fused = CollisionScalarMath.MultiplyAdd( 1d + offset, 1d - offset, -1d );
		Assert.Equal( 0d, separate );
		Assert.Equal( -Math.Pow( 2d, -46 ), fused );
	}
}
