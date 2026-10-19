using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalAnimationPoseProcessorTests
{
	[Fact]
	public void MotionAndContactUseTheirOwnBoneAndTimeSources()
	{
		var state = State();
		state.RelativeBodyFrame = At( 1, 0, 0 );
		var input = Input();
		input.Clips[ 0 ] = At( 3, 4, 0 );
		input.Clips[ 1 ] = At( 100, 200, 300 );
		var root = AnimationAttributeKey.Encode( "ROOT" );
		input = input with
		{
			DeltaTime = .5f,
			OwnerDeltaTime = .25f,
			ModelToWorld = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 99 ) ),
			BoardVelocity = new Vector3( 7, 8, 9 ),
			Attributes = input.Attributes with
			{
				Attributes = new[] { new PhysicalAnimationAttribute( AnimationAttributeKey.Encode( "PUSH_CONTACT" ), 1f, 3, 12, root ) },
				BoneNames = new[] { root },
				BoardDisplacement = new Vector3( 1000 ), DeltaTime = 9f, RightToeBone = 0
			}
		};
		PhysicalAnimationPoseProcessor.Prepare( state, input );
		Assert.Equal( 10f, state.Attributes.PushSpeed );
		Assert.Equal( 0UL, state.Attributes.MotionFlags & (1UL << 26) );
		Assert.Equal( new Vector3( -16, 16, 0 ), state.MotionVelocity );
		Assert.Equal( new Vector3( 7, 8, 9 ), state.BoardVelocity );
		Assert.Equal( At( 4, 4, 0 ), state.ReferenceFrame );
		Assert.Equal( At( 100, 200, 300 ), state.GatheredBoard );
		Assert.Equal( .5f, state.DeltaTime );
	}

	[Fact]
	public void MotionComposesOnTheLeftAndPersistsUntilThePhysicalStageResetsIt()
	{
		var state = State();
		state.RelativeBodyFrame = At( 2, 0, 0 );
		var input = Input();
		input.Clips[ 0 ] = new LimbRigidTransform( new LimbFrame( Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ ), new Vector3( 10, 0, 0 ) );
		PhysicalAnimationPoseProcessor.Prepare( state, input );
		Assert.Equal( new Vector3( 10, 2, 0 ), state.RelativeBodyFrame.Translation );
		Assert.Equal( Vector3.Zero, state.InverseReferenceFrame.Transform( state.ReferenceFrame.Translation ) );
		PhysicalAnimationPoseProcessor.Prepare( state, input );
		Assert.Equal( new Vector3( 8, 10, 0 ), state.RelativeBodyFrame.Translation );
		Assert.Equal( input.Clips[ 1 ], state.Skeleton.AnimatedBones[ 0 ] );
	}

	[Fact]
	public void TiltRequestUsesStrictMagnitudeAndRemainsSetAcrossAFlatPose()
	{
		var state = State();
		var input = Input();
		void Tilt( float value ) => input.Clips[ 1 ] = new LimbRigidTransform(
			new LimbFrame( new Vector3( 1, value, 0 ), Vector3.UnitY, Vector3.UnitZ ), Vector3.Zero );
		Tilt( .71f );
		PhysicalAnimationPoseProcessor.Prepare( state, input );
		Assert.Equal( 0UL, state.Attributes.ActionFlags & (1UL << 47) );
		Tilt( -.72f );
		PhysicalAnimationPoseProcessor.Prepare( state, input );
		Assert.NotEqual( 0UL, state.Attributes.ActionFlags & (1UL << 47) );
		Tilt( 0f );
		PhysicalAnimationPoseProcessor.Prepare( state, input );
		Assert.NotEqual( 0UL, state.Attributes.ActionFlags & (1UL << 47) );
	}

	[Fact]
	public void PublicationCachesAttachedPosesAndMeasuresBoardAxisRatherThanHeight()
	{
		var state = State();
		PhysicalAnimationPoseProcessor.Prepare( state, Input() );
		state.PreviousBoardAxisY = .25f;
		var board = new LimbRigidTransform( new LimbFrame( Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY ), new Vector3( 0, -1000, 0 ) );
		state.Skeleton.MutableBones[ 0 ] = board;
		state.Skeleton.MutableBones[ 23 ] = At( 7, 8, 9 );
		var weights = new float[ 24 ];
		weights[ 23 ] = 1f;
		PhysicalAnimationPoseProcessor.Publish( state, weights, LimbRigidTransform.Identity, true, true, new Vector4( 3, 4, 5, 6 ) );
		Assert.Equal( board, state.CachedBoard );
		Assert.Equal( At( 7, 8, 9 ), state.CachedRoot );
		Assert.Equal( .75f, state.BoardAxisDelta );
		Assert.Equal( 1UL << 12, state.Attributes.MotionFlags & ((1UL << 11) | (1UL << 12)) );
		Assert.Equal( new Vector3( 7, 1008, 9 ), state.PublishedRootOffset );
		Assert.Equal( new Vector4( 3, 4, 5, 6 ), state.PublishedExternalMotion );
		Assert.Equal( LimbRigidTransform.Identity, state.GatheredBoard );
	}

	[Fact]
	public void PublicationReplacesOwnedBitsAndRetainsAnUnrequestedExternalVector()
	{
		var state = State();
		state.Attributes.MotionFlags = (1UL << 63) | (1UL << 12);
		state.Attributes.ActionFlags = (1UL << 63) | (1UL << 49);
		state.PublishedExternalMotion = new Vector4( 9, 8, 7, 6 );
		PhysicalAnimationPoseProcessor.Publish( state, new float[ 24 ], LimbRigidTransform.Identity, false, false, Vector4.Zero );
		Assert.Equal( (1UL << 63) | (1UL << 11), state.Attributes.MotionFlags );
		Assert.Equal( 1UL << 63, state.Attributes.ActionFlags );
		Assert.Equal( new Vector4( 9, 8, 7, 6 ), state.PublishedExternalMotion );
		Assert.Equal( 1f, state.Cache.DeltaScale );
	}

	[Fact]
	public void InvalidMotionIndexFailsBeforeChangingRetainedState()
	{
		var state = State();
		state.RelativeBodyFrame = At( 3, 4, 5 );
		state.DeltaTime = 9f;
		Assert.Throws<ArgumentOutOfRangeException>( () => PhysicalAnimationPoseProcessor.Prepare( state, Input() with { MotionBone = 36 } ) );
		Assert.Equal( At( 3, 4, 5 ), state.RelativeBodyFrame );
		Assert.Equal( 9f, state.DeltaTime );
	}

	private static PhysicalAnimationPoseState State()
	{
		var identity = LimbRigidTransform.Identity;
		var skeleton = new EffectorRigSkeleton( new EffectorBoneBinding[ 24 ], new int[ 24 ],
			Enumerable.Repeat( identity, 24 ).ToArray(), new LimbRigidTransform[ 24 ], Enumerable.Repeat( identity, 24 ).ToArray() );
		return new PhysicalAnimationPoseState( skeleton, new EffectorPoseCache( 24 ), new PhysicalAnimationAttributeState(), new LimbRigidTransform[ 4 ] )
		{
			RelativeBodyFrame = identity
		};
	}

	private static PhysicalAnimationPoseInput Input() => new(
		Enumerable.Repeat( LimbRigidTransform.Identity, 36 ).ToArray(), 0, Enumerable.Range( 1, 24 ).ToArray(),
		new[] { 28, 29, 30, 31 }, new[] { 0, 1, 2, 3 }, LimbRigidTransform.Identity, 1f, 1f, Vector3.Zero,
		new PhysicalAnimationAttributeInput( Array.Empty<PhysicalAnimationAttribute>(), Array.Empty<AnimationAttributeKey>(),
			Vector3.Zero, 0, 1f, false, Vector3.Zero, false, false, .2f, .8f, true, false, 0 ) );

	private static LimbRigidTransform At( float x, float y, float z ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, y, z ) };
}
