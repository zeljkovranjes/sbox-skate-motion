using SkateMotion.Core.Animation;
using System.Numerics;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationRidingFakieObservationProviderTests
{
	[Fact]
	public void PhysicalPublicationFeedsTheRetainedUpdaterAndSnapshotsMutableInputs()
	{
		var character = new PhysicalPosePreparationState
		{
			ModelToWorld = new( new( Vector3.UnitZ, Vector3.UnitY, -Vector3.UnitX ), Vector3.Zero )
		};
		var body = new PhysicalBodyState { Velocity = new( 3f, 4f, 0f ) };
		var bindings = new PhysicalBodyBinding[ 7 ];
		bindings[ 6 ] = new() { Body = body };
		var direction = new PhysicalDirectionState { FilteredDirection = Vector3.UnitY };
		var observation = AnimationRidingFakieObservationProvider.Capture(
			character, bindings, direction, 0, 1, 0x64, Vector3.UnitZ );
		Assert.Equal( new Vector3( 3f, 4f, 0f ), observation.BoardDirection );
		Assert.Equal( -Vector3.UnitX, observation.ReferenceDirection );
		Assert.Equal( 3f, observation.Speed );
		Assert.Equal( Vector3.UnitZ, observation.AlternateDirection );
		Assert.Equal( 0x64, observation.ControllerCode );

		body.Velocity = Vector3.Zero;
		character.ModelToWorld = LimbRigidTransform.Identity;
		var instance = new AnimationRidingFakieInstance();
		var flags = new AnimationGameplayFlags( 0 );
		var result = false;
		var definition = new AnimationRidingFakieDefinition( 1f, .5f, .2f, 0f );
		instance.Update( definition, observation, .125f, flags, value => result = value );
		instance.Update( definition, observation, .125f, flags, value => result = value );
		Assert.True( result );
	}

	[Fact]
	public void StanceUsesThePhysicalFrameAndMissingBodyCannotBecomeAnEstimatedObservation()
	{
		var character = new PhysicalPosePreparationState { ModelToWorld = LimbRigidTransform.Identity };
		var bindings = new PhysicalBodyBinding[ 7 ];
		var direction = new PhysicalDirectionState { FilteredDirection = Vector3.UnitY };
		Assert.Throws<ArgumentException>( () => AnimationRidingFakieObservationProvider.Capture(
			character, bindings, direction, 4, 2, 0x1f7, Vector3.UnitX ) );
		bindings[ 6 ] = new() { Body = new() { Velocity = new( 0f, 5f, 0f ) } };
		var observation = AnimationRidingFakieObservationProvider.Capture(
			character, bindings, direction, 4, 2, 0x1f7, Vector3.UnitX );
		Assert.Equal( -Vector3.UnitZ, observation.ReferenceDirection );
		Assert.Equal( new Vector3( 0f, 5f, 0f ), observation.BoardDirection );
		Assert.Equal( 0f, observation.Speed );
		Assert.Equal( 2, observation.MovementState );
		Assert.Equal( 0x1f7, observation.ControllerCode );
	}
}
