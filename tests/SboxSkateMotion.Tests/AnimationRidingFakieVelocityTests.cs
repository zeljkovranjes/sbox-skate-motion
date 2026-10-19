using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationRidingFakieVelocityTests
{
	[Fact]
	public void PostsolveVelocityFeedsRetainedObservationsAndResetClearsHistory()
	{
		var physical = PhysicalBoardOwnerFixture.State( true );
		var state = new AnimationRidingFakieVelocity();
		physical.Rig.Velocity = new( 40f, 20f, 10f );
		physical.Rig.Reset.ReferenceVelocity = new( 90f, 80f, 70f );
		var feedback = new PhysicalPoseFeedbackState { Velocity = new( -4f, 2f, 1f ) };
		var direction = new PhysicalDirectionState { FilteredDirection = Vector3.UnitY };
		var first = state.Capture( physical.Rig.Preparation, feedback,
			physical.BoardBodies, direction, 0, 2, 0x258 );
		Assert.Equal( new Vector3( -1f, .5f, .25f ), first.AlternateDirection );
		feedback.Velocity = Vector3.Zero;
		var second = state.Capture( physical.Rig.Preparation, feedback,
			physical.BoardBodies, direction, 0, 2, 0x258 );
		Assert.Equal( new Vector3( -.75f, .375f, .1875f ), second.AlternateDirection );
		Assert.Equal( new Vector3( -1f, .5f, .25f ), first.AlternateDirection );
		Assert.True( state.Acceleration.X > 0f );
		Assert.Equal( 2, second.MovementState );
		Assert.Equal( 0x258, second.ControllerCode );
		state.Reset();
		Assert.Equal( Vector3.Zero, state.Velocity );
		Assert.Equal( Vector3.Zero, state.Acceleration );
		Assert.Equal( Vector3.Zero, state.Capture( physical.Rig.Preparation, feedback, physical.BoardBodies,
			direction, 0, 2, 0x258 ).AlternateDirection );
	}
}
