using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAnimationAttributeProcessorTests
{
	[Fact]
	public void OrderedDuplicateAttributesSurviveUnrelatedGraphMetadata()
	{
		var state = new PhysicalAnimationAttributeState { BodyAdjustX = 7f, BodyAdjustZ = 8f };
		var input = Input( Scalar( "BODYADJUSTX", .25f ), Scalar( "DISTTOCOG", 90f ),
			Scalar( "BODYADJUSTZ", -.5f ), Scalar( "BODYADJUSTX", .75f ) );
		PhysicalAnimationAttributeProcessor.Update( state, input );
		Assert.Equal( .75f, state.BodyAdjustX );
		Assert.Equal( -.5f, state.BodyAdjustZ );
		var known = AnimationAttributeKey.Encode( "BODYADJUSTX" );
		PhysicalAnimationAttributeProcessor.Update( state, Input(
			new PhysicalAnimationAttribute( known with { E = known.E ^ 1u }, 100f ) ) );
		Assert.Equal( .75f, state.BodyAdjustX );
	}

	[Fact]
	public void ContactBoneChoosesTheHandRegardlessOfEventLabel()
	{
		var state = new PhysicalAnimationAttributeState();
		var root = AnimationAttributeKey.Encode( "ROOT" );
		var hand = AnimationAttributeKey.Encode( "RIGHTHAND" );
		var input = Input( new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "LEFT_HAND_GRAB" ), .5f, 3, 4, hand ) )
			with { BoneNames = new[] { root, hand } };
		PhysicalAnimationAttributeProcessor.Update( state, input );
		Assert.Equal( 1, state.ContactBone );
		Assert.Equal( 1UL << 40, state.ActionFlags );
		PhysicalAnimationAttributeProcessor.Update( state, input with
		{
			Attributes = new[] { new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "RIGHT_HAND_GRAB" ), 0f, 3, 8, root ) }
		} );
		Assert.Equal( 0, state.ContactBone );
		Assert.Equal( (1UL << 40) | (1UL << 39), state.ActionFlags );
	}

	[Fact]
	public void InactiveContactsPreserveStateButUnknownActiveEventsStillResolveBones()
	{
		var state = new PhysicalAnimationAttributeState { ContactBone = 3, PushSpeed = 8f };
		var attribute = new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "PUSH_CONTACT" ), 1f, 3 );
		PhysicalAnimationAttributeProcessor.Update( state, Input( attribute ) );
		Assert.Equal( 3, state.ContactBone );
		Assert.Equal( 8f, state.PushSpeed );
		PhysicalAnimationAttributeProcessor.Update( state, Input( attribute with
		{
			Name = AnimationAttributeKey.Encode( "CUSTOM_EVENT" ), EventFlags = 4
		} ) );
		Assert.Equal( -1, state.ContactBone );
		Assert.Equal( 8f, state.PushSpeed );
		Assert.Equal( 0UL, state.MotionFlags );
	}

	[Fact]
	public void PushContactUsesDisplacementAndClearsOnlyItsOwnPhaseFlags()
	{
		var state = new PhysicalAnimationAttributeState { MotionFlags = 1UL << 60 };
		var root = AnimationAttributeKey.Encode( "ROOT" );
		var attribute = new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "PUSH_CONTACT" ), 1f, 3, 12, root );
		var input = Input( attribute ) with { BoardDisplacement = new Vector3( 3, 4, 0 ), DeltaTime = .5f };
		PhysicalAnimationAttributeProcessor.Update( state, input );
		Assert.Equal( 10f, state.PushSpeed );
		Assert.Equal( (1UL << 60) | (1UL << 27) | (1UL << 26) | (1UL << 25), state.MotionFlags );
		PhysicalAnimationAttributeProcessor.Update( state, input with
		{
			Attributes = new[] { attribute with { Value = -1f } }
		} );
		Assert.Equal( 0f, state.PushSpeed );
		Assert.Equal( (1UL << 60) | (1UL << 27) | (1UL << 26) | (1UL << 24), state.MotionFlags );
	}

	[Fact]
	public void PreparedJumpControlsSurviveControllerLossAndUseTheirOwnSecondaryAction()
	{
		var state = new PhysicalAnimationAttributeState();
		var input = Input( Scalar( "PREPAREJUMP" ) ) with
		{
			HasController = true, Controls = new Vector3( -2f, .75f, .25f )
		};
		PhysicalAnimationAttributeProcessor.Update( state, input );
		Assert.Equal( -1f, state.PreparedControlX );
		Assert.Equal( .75f, state.PreparedControlY );
		Assert.Equal( .25f, state.JumpControlY );
		PhysicalAnimationAttributeProcessor.Update( state, Input() with { UsePreparedControls = true } );
		Assert.Equal( -1f, state.JumpControlX );
		Assert.Equal( .75f, state.JumpControlY );
		PhysicalAnimationAttributeProcessor.Update( state, Input( Scalar( "PREPAREJUMP" ) ) );
		Assert.Equal( .75f, state.PreparedControlY );
		Assert.Equal( .75f, state.JumpControlY );
	}

	[Fact]
	public void JumpHeightCacheExpiresWhenTheAttributeIsAbsent()
	{
		var state = new PhysicalAnimationAttributeState();
		PhysicalAnimationAttributeProcessor.Update( state, Input( Scalar( "JUMP" ),
			Scalar( "JUMPHEIGHTOVERRIDE", .9f ), Scalar( "TRICKHEIGHT", .5f ) ) );
		Assert.Equal( .9f, state.JumpStrength );
		Assert.True( state.HeightOverrideActive );
		PhysicalAnimationAttributeProcessor.Update( state, Input() );
		Assert.False( state.HeightOverrideActive );
		Assert.Equal( 0f, state.HeightOverride );
		Assert.Equal( 0f, state.JumpStrength );
	}

	[Fact]
	public void DropRequestClearsWorldGrabAndReleaseReplacesThrowBits()
	{
		var state = new PhysicalAnimationAttributeState { ActionFlags = 1UL << 60 };
		PhysicalAnimationAttributeProcessor.Update( state, Input( Scalar( "GRABWORLD" ),
			Scalar( "OB_THROWBOARD" ), Scalar( "OB_RELEASEBOARD" ), Scalar( "OB_DROPPINGBOARD" ) ) );
		Assert.Equal( (1UL << 60) | 0x2200UL, state.ActionFlags );
	}

	[Fact]
	public void ReplacingOneTranslationAxisPreservesOtherAxesAndFourthLane()
	{
		var state = new PhysicalAnimationAttributeState { AnimationTranslation = new Vector4( 1, 2, 3, 4 ) };
		PhysicalAnimationAttributeProcessor.Update( state, Input( Scalar( "ANIMTRANSY", -5f ) ) );
		Assert.Equal( new Vector4( 1, -5, 3, 4 ), state.AnimationTranslation );
	}

	private static PhysicalAnimationAttribute Scalar( string name, float value = 0f ) =>
		new( AnimationAttributeKey.Encode( name ), value );

	private static PhysicalAnimationAttributeInput Input( params PhysicalAnimationAttribute[] attributes ) =>
		new( attributes, new[] { AnimationAttributeKey.Encode( "ROOT" ) }, Vector3.Zero, 0, 1f / 60f,
			false, Vector3.Zero, false, false, .2f, .8f, true, false, 0UL );
}
