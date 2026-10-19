using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalJumpVelocityTests
{
	[Fact]
	public void PublishedHeightReachesEveryBodyThroughTheAttributeConsumer()
	{
		var height = AnimationAttributeKey.Encode( "TRICKHEIGHT" );
		var sink = new TrickHeightOutputSink( new( 1, [] ), 2, _ => height );
		sink.Publish( 0, new( .5f, .75f ) );
		var attributes = Attributes();
		var assembly = new PhysicalAnimationAttributeAssembly( 3 );
		assembly.Update( attributes,
			new( [], [], default, -1, 1f / 60f, false, default, false, false, .2f, .8f, true, false, 0 ),
			sink.Queued, [ Scalar( "JUMP", 1f ), Scalar( "TRICKHEIGHT", .25f ) ] );
		var state = State();
		PhysicalBodyState[] bodies = [ new(), new(), new() ];
		var result = PhysicalJumpVelocityController.Launch( attributes, state, Input(), Tuning(), bodies );
		AssertBits( result, 0x40000000, 0x40400000, 0x3fa4cccc );
		foreach ( var body in bodies )
			Assert.Equal( result, body.Velocity );
		Assert.True( state.Triggered );
		Assert.Equal( 0x3eccccccu, BitConverter.SingleToUInt32Bits( state.Cooldown ) );
		Assert.Equal( 1, sink.QueuedCount );
	}

	[Fact]
	public void StrengthExtrapolatesPastBothTuningEndpoints()
	{
		Assert.Equal( -.5f, PhysicalJumpVelocityController.InterpolateSpeed( -1f, .5f, 1.5f ) );
		Assert.Equal( 2.5f, PhysicalJumpVelocityController.InterpolateSpeed( 2f, .5f, 1.5f ) );
	}

	[Fact]
	public void RecentLaunchAddsMinimumLateralSpeedAndActivatesHold()
	{
		var attributes = Attributes();
		attributes.GrindStabilityNudge = .1f;
		var state = State();
		state.Cooldown = .3f;
		var result = PhysicalJumpVelocityController.Launch( attributes, state, Input(), Tuning(), [] );
		AssertBits( result, 0x40000000, 0x400f9db2, 0x3fa4cccc );
		Assert.Equal( 10, state.HoldFrames );
		Assert.Equal( 0x2000001UL, attributes.ActionFlags );
		Assert.Equal( 0f, state.Cooldown );
	}

	[Fact]
	public void ExactCooldownThresholdPreservesTheExistingHold()
	{
		var attributes = Attributes();
		var state = State();
		state.Cooldown = .31f;
		var result = PhysicalJumpVelocityController.Launch( attributes, state, Input(), Tuning(), [] );
		AssertBits( result, 0x40000000, 0x40400000, 0x3fa4cccc );
		Assert.Equal( 3, state.HoldFrames );
		Assert.Equal( 1UL, attributes.ActionFlags );
		Assert.Equal( 0f, state.Cooldown );
	}

	[Fact]
	public void ZeroLateralControlUsesTheReferenceSide()
	{
		var state = State();
		state.Cooldown = .3f;
		var result = PhysicalJumpVelocityController.Launch( Attributes(), state, Input(), Tuning(), [] );
		AssertBits( result, 0x40000000, 0x4070624e, 0x3fa4cccc );
	}

	[Fact]
	public void DegenerateBlendedDirectionFallsBackToThePrimaryDirection()
	{
		var result = PhysicalJumpVelocityController.Launch( Attributes(), State(),
			Input() with { FallbackDirection = -Vector3.UnitZ }, Tuning(), [] );
		AssertBits( result, 0x40000000, 0x40400000, 0x3fa4cccc );
	}

	[Fact]
	public void EnabledCorrectionIsCappedBeforeTheBodyWrite()
	{
		var result = PhysicalJumpVelocityController.Launch( Attributes(), State(),
			Input() with { CorrectionEnabled = true, Correction = new( 10f, 0f, 0f ) }, Tuning(), [] );
		AssertBits( result, 0x3e4cccd0, 0x40400000, 0x3fa4cccc );
	}

	[Theory]
	[InlineData( 2, 1f, 0x40865b66u, 0x3df3e8d1u )]
	[InlineData( 4, 1f, 0x4019213fu, 0x3fc74178u )]
	[InlineData( 2, -1f, 0x3faa9d39u, 0x3fdd48dfu )]
	public void SurfaceModesUseTheirDistinctBiasAndAnchorSide( int mode, float side, uint y, uint z )
	{
		var result = PhysicalJumpVelocityController.Launch( Attributes(), State(),
			Input() with { Mode = mode, Position = new( 0f, side, 0f ) }, Tuning(), [] );
		AssertBits( result, 0x40000000, y, z );
	}

	private static PhysicalAnimationAttributeState Attributes() => new() { JumpStrength = .75f, ActionFlags = 1 };
	private static PhysicalJumpVelocityState State() => new() { Cooldown = 1f, HoldFrames = 3 };
	private static PhysicalJumpVelocityTuning Tuning() => new( .5f, 1.5f, .7f );
	private static PhysicalJumpVelocityInput Input() => new( new( 2f, 3f, -4f ), Vector3.UnitZ,
		default, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitZ, default, default, 0, false );
	private static AnimationAttributeQueryResult Scalar( string name, float value ) =>
		new( new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ),
			AnimationAttributeKey.Encode( name ), -1f, -1f, 6, 0 );

	private static void AssertBits( Vector3 value, uint x, uint y, uint z )
	{
		Assert.Equal( x, BitConverter.SingleToUInt32Bits( value.X ) );
		Assert.Equal( y, BitConverter.SingleToUInt32Bits( value.Y ) );
		Assert.Equal( z, BitConverter.SingleToUInt32Bits( value.Z ) );
	}
}
