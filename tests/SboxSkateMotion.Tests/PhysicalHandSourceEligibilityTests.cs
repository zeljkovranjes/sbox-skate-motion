using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandSourceEligibilityTests
{
	[Fact]
	public void SpeedThresholdIsStrict()
	{
		var velocity = new Vector3( 3, 4, 0 );
		Assert.True( Gate( MathF.BitDecrement( 5f ), 0f ).Allows( velocity, Vector3.UnitX ) );
		Assert.False( Gate( 5f, 0f ).Allows( velocity, Vector3.UnitX ) );
		Assert.False( Gate( MathF.BitIncrement( 5f ), 0f ).Allows( velocity, Vector3.UnitX ) );
	}

	[Fact]
	public void SurfaceAngleThresholdIsStrict()
	{
		var velocity = new Vector3( 3, 4, 0 );
		Assert.True( Gate( 1f, MathF.BitDecrement( 90f ) ).Allows( velocity, Vector3.UnitX ) );
		Assert.False( Gate( 1f, 90f ).Allows( velocity, Vector3.UnitX ) );
		Assert.False( Gate( 1f, MathF.BitIncrement( 90f ) ).Allows( velocity, Vector3.UnitX ) );
	}

	[Theory]
	[InlineData( 0f, true )]
	[InlineData( .95f, true )]
	[InlineData( -.95f, true )]
	[InlineData( .9500001f, false )]
	[InlineData( -.9500001f, false )]
	[InlineData( 1.2f, false )]
	public void VerticalExclusionUsesTheSuppliedNormalY( float normalY, bool expected )
	{
		Assert.Equal( expected, Gate( 1f, 0f ).Allows( new Vector3( 3, 4, 0 ), new Vector3( 100, normalY, 100 ) ) );
	}

	[Theory]
	[InlineData( -1f, false )]
	[InlineData( 0f, false )]
	[InlineData( .001f, true )]
	public void RequiresPositiveUpwardVelocity( float upward, bool expected )
	{
		Assert.Equal( expected, Gate( 1f, 0f ).Allows( new Vector3( 5, upward, 0 ), Vector3.UnitX ) );
	}

	[Fact]
	public void ZeroVelocityCannotPassAPositiveMinimum()
	{
		Assert.False( Gate( 1f, 0f ).Allows( Vector3.Zero, Vector3.UnitX ) );
	}

	private static PhysicalHandSourceEligibility Gate( float speed, float angle ) => new(
		new LimbArcSine( -.5044005513191223f, .9339332580566406f,
			-5.54846715927124f, 5.603603363037109f, 1.5707963705062866f ), speed, angle );
}
