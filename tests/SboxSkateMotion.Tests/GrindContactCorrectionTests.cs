using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindContactCorrectionTests
{
	[Theory]
	[InlineData( int.MinValue )]
	[InlineData( -1 )]
	[InlineData( 0 )]
	public void NonpositiveSamplesPreserveEveryVelocityLane( int sample )
	{
		var velocity = new Vector4( 1, -2, 3, -4 );
		Assert.Equal( velocity, new GrindContactCorrection( .25f ).Apply(
			velocity, new Vector4( float.NaN ), sample, 0f ) );
	}

	[Fact]
	public void CorrectionUsesSampleTimeAndAddsToExistingVelocity()
	{
		var result = new GrindContactCorrection( .25f ).Apply(
			new Vector4( 7, 8, 9, 10 ), new Vector4( 1, 2, 3, 4 ), 2, 100f );
		Assert.Equal( new Vector4( 9, 12, 15, 18 ), result );
	}

	[Theory]
	[InlineData( 2.5f, 1.5f, 2f, 5f )]
	[InlineData( -2.5f, -1.5f, -2f, -5f )]
	[InlineData( 0f, 0f, 0f, 0f )]
	public void SpatialSpeedLimitScalesEveryLane( float limit, float x, float y, float w )
	{
		var result = new GrindContactCorrection( .25f ).Apply(
			Vector4.Zero, new Vector4( 3, 4, 0, 10 ), 4, limit );
		Assert.Equal( new Vector4( x, y, 0, w ), result );
	}

	[Fact]
	public void FourthLaneDoesNotContributeToSpeedLimit()
	{
		var result = new GrindContactCorrection( .25f ).Apply(
			Vector4.Zero, new Vector4( 0, 0, 0, 100 ), 4, 1f );
		Assert.Equal( new Vector4( 0, 0, 0, 100 ), result );
	}

	[Fact]
	public void EqualSquaredLimitDoesNotApplyNegativeScale()
	{
		var result = new GrindContactCorrection( .25f ).Apply(
			Vector4.Zero, new Vector4( 3, 4, 0, 10 ), 4, -5f );
		Assert.Equal( new Vector4( 3, 4, 0, 10 ), result );
	}

	[Fact]
	public void ClampedCallDoesNotAffectNextUnclampedCall()
	{
		var correction = new GrindContactCorrection( .25f );
		correction.Apply( new Vector4( 10 ), new Vector4( 3, 4, 0, 10 ), 4, -2.5f );
		Assert.Equal( new Vector4( 1, 2, 0, 3 ), correction.Apply(
			Vector4.Zero, new Vector4( 1, 2, 0, 3 ), 4, 100f ) );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -1f )]
	[InlineData( float.NaN )]
	[InlineData( float.PositiveInfinity )]
	public void SampleIntervalMustBeFiniteAndPositive( float interval )
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => new GrindContactCorrection( interval ) );
	}
}
