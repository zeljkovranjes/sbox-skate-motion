using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationPhysicalMovementTests
{
	private static AnimationPhysicalMovementInput Input( int controller, bool contact = false )
		=> new( new( controller, AnimationPhysicalControllerCodes.ParentOf( controller ) ),
			0, contact, false, false, false, false, default );

	[Fact]
	public void AirHistoryAndUnknownControllersPreserveThePublishedContactUntilStateChanges()
	{
		var key = new AnimationMovementContactKey( 9, 8, 7, 6, 5 );
		var state = new AnimationPhysicalMovementState( key );
		state.Update( Input( 100, true ) with { BoardContactType = 8 } );
		for ( var i = 0; i < 9; i++ )
			Assert.Equal( 1, state.Update( Input( 200 ) ).Current );
		Assert.Equal( 2, state.Update( Input( 200 ) ).Current );
		Assert.Equal( 1, state.Update( Input( 201, true ) ).Current );
		var contact = new AnimationMovementContactData( 23, 17, 3, .75f,
			0x1122334455667788, 0x8877665544332211, new( 1, 2, 3, 4, 5 ), new( 6, 7, 8, 9, 10 ) );
		Assert.Equal( contact, state.Update( Input( 400 ) with { Contact = contact } ).Contact );
		var retained = state.Update( Input( 999 ) );
		Assert.Equal( 3, retained.Current );
		Assert.Equal( contact, retained.Contact );
		var exited = state.Update( Input( 300 ) );
		Assert.False( exited.ContactActive );
		Assert.Equal( key, exited.Contact.FirstKey );
		Assert.Equal( -1, exited.Contact.Index );
		Assert.Equal( contact, state.RetainedContact );
		state.Reset();
		Assert.Equal( 0, state.Current );
		Assert.Equal( 0, state.AirFrames );
		Assert.True( state.TransitionRequested );
	}

	[Fact]
	public void PhysicalClassificationFeedsTheGraphWithoutRendererOrMovementEnumSubstitution()
	{
		var binding = new AnimationRidingFakieBinding( default );
		var definition = new AnimationRidingFakieDefinition( 1f, .5f, .2f, 0f );
		var flags = new AnimationGameplayFlags( 0 );
		var ownerFlag = false;
		Assert.Throws<InvalidOperationException>( () => binding.UpdateGraph( definition, .1f, flags, _ => { } ) );
		var character = new PhysicalPosePreparationState { ModelToWorld = LimbRigidTransform.Identity };
		var feedback = new PhysicalPoseFeedbackState { Velocity = -Vector3.UnitZ * 4f };
		var bodies = PhysicalBoardOwnerFixture.Bindings( 7 );
		bodies[ 6 ].Body.Velocity = -Vector3.UnitZ * 2f;
		var direction = new PhysicalDirectionState { FilteredDirection = Vector3.UnitY };
		var output = binding.PublishPhysical( character, feedback, bodies, direction, 0, Input( 503 ) );
		Assert.Equal( 6, output.Current );
		Assert.Equal( -Vector3.UnitZ, binding.Observation.AlternateDirection );
		binding.UpdateGraph( definition, .1f, flags, value => ownerFlag = value );
		binding.UpdateGraph( definition, .1f, flags, value => ownerFlag = value );
		Assert.True( ownerFlag );
		output = binding.PublishPhysical( character, feedback, bodies, direction, 0, Input( 503 ) with { OffboardRideMode = true } );
		Assert.Equal( 1, output.Current );
		binding.PublishPhysical( character, feedback, bodies, direction, 0, Input( 702 ) );
		binding.UpdateGraph( definition, .1f, flags, value => ownerFlag = value );
		Assert.False( ownerFlag );
	}

	[Fact]
	public void ControllerPublicationRetainsBothReadsAndUsesSignedParentBoundaries()
	{
		var calls = 0;
		var codes = AnimationPhysicalControllerCodes.Capture( () => calls++ == 0 ? 503 : 201 );
		Assert.Equal( 2, calls );
		Assert.Equal( new AnimationPhysicalControllerCodes( 503, 200 ), codes );
		Assert.Equal( 0, AnimationPhysicalControllerCodes.ParentOf( -1 ) );
		Assert.Equal( 0, AnimationPhysicalControllerCodes.ParentOf( 99 ) );
		Assert.Equal( 100, AnimationPhysicalControllerCodes.ParentOf( 100 ) );
		Assert.Equal( 700, AnimationPhysicalControllerCodes.ParentOf( int.MaxValue ) );
	}
}
