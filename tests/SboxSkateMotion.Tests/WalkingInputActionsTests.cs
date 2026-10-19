using SkateMotion.Core.Input;
using System.Numerics;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class WalkingInputActionsTests
{
	private static ControllerReading Reading( float x, float y ) => new( x, y, 0f, 0f, 0f, 0f, 0 );

	private static WalkingInputContext Context( Vector3 facing, bool project = false, Vector3 normal = default )
		=> new( facing, project, normal, .000001f, .9f );

	[Fact]
	public void NeutralRetainsFourZeroOutputs()
	{
		Assert.Equal( new WalkingInputActions( 0f, 0f, 0f, 0f ),
			WalkingInputActions.Evaluate( Reading( 0f, 0f ), Context( Vector3.UnitX ) ) );
	}

	[Fact]
	public void FacingIgnoresVerticalComponentAndNormalizesHorizontalLength()
	{
		var result = WalkingInputActions.Evaluate( Reading( .25f, -.5f ), Context( new( 0f, 42f, 5f ) ) );
		Assert.Equal( -.5f, result.Magnitude );
		Assert.Equal( .25f, result.WorldX );
		Assert.Equal( -.5f, result.WorldZ );
	}

	[Fact]
	public void ZeroFacingDoesNotDiscardStickMagnitudeOrAxes()
	{
		var result = WalkingInputActions.Evaluate( Reading( 1f, 0f ), Context( Vector3.UnitY ) );
		Assert.Equal( 0f, result.Magnitude );
		Assert.Equal( 1f, result.StickMagnitude );
		Assert.Equal( 1f, result.WorldX );
	}

	[Fact]
	public void NormalizationThresholdUsesStrictLengthComparison()
	{
		var context = new WalkingInputContext( Vector3.UnitX, false, default, 1f, .9f );
		Assert.Equal( 0f, WalkingInputActions.Evaluate( Reading( 1f, 0f ), context ).Magnitude );
		Assert.Equal( 1f, WalkingInputActions.Evaluate( Reading( 1f, 0f ),
			context with { NormalizationMinimum = .999f } ).Magnitude );
	}

	[Fact]
	public void DirectObstructionStopsFacingMagnitudeWithoutChangingRawAxes()
	{
		var result = WalkingInputActions.Evaluate( Reading( .5f, 0f ), Context( Vector3.UnitX, true, -Vector3.UnitX ) );
		Assert.Equal( 0f, result.Magnitude );
		Assert.Equal( .5f, result.StickMagnitude );
		Assert.Equal( .5f, result.WorldX );
	}

	[Fact]
	public void ObliqueObstructionRemovesOnlyIntoSurfaceContribution()
	{
		var result = WalkingInputActions.Evaluate( Reading( .5f, .5f ), Context( Vector3.UnitZ, true, -Vector3.UnitX ) );
		Assert.Equal( .5f, result.Magnitude );
		Assert.Equal( .5f, result.WorldX );
		Assert.Equal( .5f, result.WorldZ );
	}

	[Fact]
	public void MotionAwayFromObstructionRemainsSigned()
	{
		var result = WalkingInputActions.Evaluate( Reading( -.5f, 0f ), Context( Vector3.UnitX, true, -Vector3.UnitX ) );
		Assert.Equal( -.5f, result.Magnitude );
	}

	[Fact]
	public void ProjectionStrengthIsBoundedBeforeApplyingUnnormalizedNormal()
	{
		var context = new WalkingInputContext( Vector3.UnitX, true, new( -2f, 0f, 0f ), .000001f, 3f );
		var result = WalkingInputActions.Evaluate( Reading( 1f, 0f ), context );
		Assert.Equal( -1f, result.Magnitude );
		Assert.Equal( 1f, result.StickMagnitude );
	}

	[Fact]
	public void AlignmentLimitIsStrict()
	{
		var context = new WalkingInputContext( Vector3.UnitX, true, new( -.5f, 0f, 0f ), .000001f, .5f );
		Assert.Equal( .75f, WalkingInputActions.Evaluate( Reading( 1f, 0f ), context ).Magnitude );
		Assert.Equal( 0f, WalkingInputActions.Evaluate( Reading( 1f, 0f ),
			context with { ObstructionAlignmentLimit = .49f } ).Magnitude );
	}

	[Fact]
	public void StickMagnitudeIsIndependentOfFacingAndProjection()
	{
		var reading = Reading( .6f, .8f );
		var result = WalkingInputActions.Evaluate( reading, Context( Vector3.UnitX, true, -Vector3.UnitX ) );
		Assert.InRange( result.StickMagnitude, .9999999f, 1.0000001f );
		Assert.Equal( WalkingInputActions.Evaluate( reading, Context( Vector3.UnitZ ) ).StickMagnitude, result.StickMagnitude );
	}
}
