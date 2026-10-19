using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandReachBuilderTests
{
	[Fact]
	public void MotionUsesHorizontalAndVerticalSpeedWhileIgnoringDepthVelocity()
	{
		var builder = Create();
		var first = builder.Build( Vector3.Zero, new Vector3( 3f, 4f, 0f ) );
		var second = builder.Build( Vector3.Zero, new Vector3( 3f, 4f, 100f ) );
		Assert.Equal( first, second );
		Assert.Equal( new Vector3( 3f, 5f, 0f ), first.Motion );
	}

	[Fact]
	public void HorizontalSignMirrorsReachAroundOrigin()
	{
		var builder = Create();
		var forward = builder.Build( Vector3.Zero, new Vector3( 3f, 4f, 0f ) );
		var backward = builder.Build( Vector3.Zero, new Vector3( -3f, 4f, 0f ) );
		Assert.Equal( forward.FirstLower.X, -backward.FirstLower.X );
		Assert.Equal( forward.SecondLower.X, -backward.SecondLower.X );
		Assert.Equal( forward.FirstUpper.X, -backward.FirstUpper.X );
		Assert.Equal( forward.SecondUpper.X, -backward.SecondUpper.X );
		Assert.Equal( forward.FirstLower.Y, backward.FirstLower.Y );
		Assert.Equal( forward.FirstUpper.Y, backward.FirstUpper.Y );
	}

	[Fact]
	public void ZeroHorizontalMotionUsesNegativeReachDirection()
	{
		var region = Create().Build( Vector3.Zero, new Vector3( 0f, 4f, 0f ) );
		Assert.True( region.IsValid );
		Assert.True( region.FirstLower.X < 0f );
		Assert.True( region.SecondLower.X < region.FirstLower.X );
	}

	[Fact]
	public void EqualHorizontalBoundsRejectRegionButStillWritePredictions()
	{
		var builder = Create( minimumX: 1f, maximumX: 1f );
		var region = builder.Build( new Vector3( .3f, 1f, -.5f ), new Vector3( 1f, 4f, 0f ) );
		Assert.False( region.IsValid );
		Assert.Equal( region.FirstLower, region.SecondLower );
		Assert.Equal( region.FirstUpper, region.SecondUpper );
		Assert.True( region.FirstUpper.Y > region.FirstLower.Y );
	}

	[Fact]
	public void NegativeVerticalCandidatesKeepOriginBeforeHeightOffset()
	{
		var builder = Create( minimumY: -10f, increaseY: -10f, decreaseY: 10f );
		var origin = new Vector3( 1f, 2f, 3f );
		var region = builder.Build( origin, new Vector3( 1f, 0f, 0f ) );
		var expected = origin - new Vector3( 0f, .25f, 0f );
		Assert.Equal( expected, region.FirstLower );
		Assert.Equal( expected, region.SecondLower );
		Assert.Equal( expected, region.FirstUpper );
		Assert.Equal( expected, region.SecondUpper );
	}

	[Fact]
	public void HeightOffsetDoesNotChangeHorizontalPredictionOrValidity()
	{
		var velocity = new Vector3( 2f, 3f, 0f );
		var first = Create( offset: 0f ).Build( Vector3.Zero, velocity );
		var second = Create( offset: .5f ).Build( Vector3.Zero, velocity );
		Assert.Equal( first.IsValid, second.IsValid );
		Assert.Equal( first.FirstLower.X, second.FirstLower.X );
		Assert.Equal( first.SecondUpper.X, second.SecondUpper.X );
		Assert.Equal( first.FirstLower.Y - .5f, second.FirstLower.Y );
		Assert.Equal( first.SecondUpper.Y - .5f, second.SecondUpper.Y );
	}

	[Fact]
	public void CurveIntervalsControlReachAcrossSuccessiveBuilds()
	{
		var changing = new GrindHeightCorrectionCurve( [ 0f, 2f, 4f, 8f ], [ 0f, 1f, 3f, 7f ] );
		var builder = new PhysicalHandReachBuilder( Flat( .1f ), Flat( 20f ), Flat( 0f ), Flat( 1f ),
			changing, Flat( .5f ), Flat( 1f ), 0f );
		var first = builder.Build( Vector3.Zero, new Vector3( 0f, 2f, 0f ) );
		var second = builder.Build( Vector3.Zero, new Vector3( 0f, 4f, 0f ) );
		Assert.True( second.FirstUpper.Y > first.FirstUpper.Y );
		Assert.Equal( first, builder.Build( Vector3.Zero, new Vector3( 0f, 2f, 0f ) ) );
	}

	private static PhysicalHandReachBuilder Create( float minimumX = .1f, float maximumX = 12f,
		float minimumY = 0f, float increaseY = 2f, float decreaseY = 1f, float offset = .25f ) =>
		new( Flat( minimumX ), Flat( maximumX ), Flat( minimumY ), Flat( decreaseY ),
			Flat( increaseY ), Flat( .5f ), Flat( 1f ), offset );

	private static GrindHeightCorrectionCurve Flat( float value ) =>
		new( [ 0f, 2f, 5f, 10f ], [ value, value, value, value ] );
}
