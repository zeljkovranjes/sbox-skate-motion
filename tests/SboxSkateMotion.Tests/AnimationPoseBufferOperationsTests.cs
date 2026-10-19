using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPoseBufferOperationsTests
{
	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public void CompositionUsesPreparationOrderAndPreservesActiveMask( bool firstActive )
	{
		var a = Reference( new Vector3( 1f, 0f, 0f ), .25f, Quaternion.Identity, new Vector4( 2f, 3f, 4f, 5f ) );
		var b = Reference( new Vector3( 0f, 2f, 0f ), .75f, new Quaternion( 0f, 0f, 1f, 0f ), new Vector4( 7f, 11f, 13f, 17f ) );
		var operations = Operations( a, b );
		var control = new AnimationCompositionPoseControl( operations, firstActive );
		operations.Begin( true, 2 );
		Assert.True( control.Prepare( true ) );
		Assert.Equal( new Vector3( -1f, 2f, 0f ), operations.Pose.Positions[ 0 ] );
		Assert.Equal( firstActive ? .25f : .75f, operations.Pose.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 14f, 33f, 52f, 85f ), operations.Pose.Scales[ 0 ] );
		Assert.Equal( new Vector3( 1f, 0f, 0f ), a.Pose.Positions[ 0 ] );
		Assert.Equal( 1, operations.PreparedCount );
	}

	[Fact]
	public void ConstantPoseRestoresAllLanesWithoutRetainingCallerMutation()
	{
		var values = new[] { new AnimationBoneTransform( new Vector4( 1f, 2f, 3f, .25f ),
			new Quaternion( 0f, 0f, 0f, 2f ), new Vector4( 2f, 3f, 4f, 5f ) ) };
		var reference = new AnimationReferencePose( values );
		values[ 0 ] = default;
		Assert.False( reference.Prepare( false, true, 99 ) );
		Assert.True( reference.Prepare( true, true, 99 ) );
		Assert.Equal( 2f, reference.Pose.Rotations[ 0 ].W );
		reference.Pose.Positions[ 0 ] = Vector3.Zero;
		reference.Prepare( true, false, 0 );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), reference.Pose.Positions[ 0 ] );
		Assert.Equal( .25f, reference.Pose.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 2f, 3f, 4f, 5f ), reference.Pose.Scales[ 0 ] );
	}

	[Fact]
	public void DisabledAndSuppressedChildrenDoNotSupplyStaleOperands()
	{
		var context = new AnimationPoseSamplingContext( 1, false );
		var leaf = Leaf( 10f, context );
		var reference = Reference( Vector3.Zero, 1f );
		var operations = Operations( leaf, reference );
		var control = new AnimationCompositionPoseControl( operations, true );
		operations.Begin( true, 2 );
		Assert.True( control.Prepare( true ) );
		context.Suppressed = true;
		operations.Begin( true, 2 );
		Assert.False( control.Prepare( true ) );
		Assert.Equal( 0, operations.PreparedCount );
		Assert.Throws<InvalidOperationException>( () => operations.Compose( true, true ) );
		context.Suppressed = false;
		operations.Begin( true, 2 );
		Assert.False( control.Prepare( false ) );
		Assert.Equal( 0, operations.PreparedCount );
	}

	[Fact]
	public void SequenceHandoffPreparesTheSuccessorAtItsUnusedTime()
	{
		var context = new AnimationPoseSamplingContext( 1, false );
		var first = Leaf( 0f, context );
		var second = Leaf( 10f, context );
		var playback = new AnimationSequencePlayback( first, second );
		var operations = Operations( first, second );
		var control = new AnimationSequencePoseControl( playback, operations );
		var completion = default( AnimationPlaybackCompletion );
		playback.Advance( 1.5f, 0f, ref completion );
		operations.Begin( true, 2 );
		Assert.True( control.Prepare( true ) );
		Assert.True( playback.ActiveNext );
		Assert.Equal( .5f, second.Elapsed );
		Assert.InRange( operations.Pose.Positions[ 0 ].X, 10.9999f, 11f );
		Assert.Equal( .5f, second.Clock.PreviousPosition );
		Assert.Equal( 1, operations.PreparedCount );
	}

	[Theory]
	[InlineData( 0, 5.9999f, 6f )]
	[InlineData( -1, 2.2499f, 2.25f )]
	[InlineData( int.MinValue, 4.7499f, 4.75f )]
	public void TransitionBlendsPreparedClipsWithTheSelectedMask( int mode, float lower, float upper )
	{
		var context = new AnimationPoseSamplingContext( 1, false );
		var first = Leaf( 0f, context, .25f );
		var second = Leaf( 10f, context, .75f );
		var playback = new AnimationTransitionPlayback( first, second, 1f, AnimationTransitionMode.Both );
		var operations = Operations( first, second );
		var control = new AnimationTransitionPoseControl( playback, operations, mode );
		var completion = default( AnimationPlaybackCompletion );
		playback.Advance( .5f, 0f, ref completion );
		operations.Begin( true, 2 );
		Assert.True( control.Prepare( true ) );
		Assert.InRange( operations.Pose.Positions[ 0 ].X, lower, upper );
		Assert.Equal( 1, operations.PreparedCount );
	}

	[Fact]
	public void ChannelWeightsUseOverlayMaskAndLeaveSourcePosesIntact()
	{
		var first = Reference( Vector3.Zero, .25f );
		var second = Reference( new Vector3( 8f, 0f, 0f ), .75f );
		var blend = new AnimationChannelBlend( new AnimationChannelBlendState( .5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0 ) );
		var operations = Operations( first, second );
		var control = new AnimationChannelPoseControl( blend, operations );
		operations.Begin( false, 0 );
		Assert.True( control.Prepare( true ) );
		Assert.Equal( 3f, operations.Pose.Positions[ 0 ].X );
		Assert.Equal( Vector3.Zero, first.Pose.Positions[ 0 ] );
		Assert.Equal( 8f, second.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void RepeatedChildPreparationSnapshotsBeforeItsOutputChanges()
	{
		var context = new AnimationPoseSamplingContext( 1, false );
		var leaf = Leaf( 0f, context );
		var operations = Operations( leaf, leaf );
		operations.Begin( false, 2 );
		operations.Prepare( 0, true );
		leaf.Seek( 1f );
		operations.Prepare( 1, true );
		operations.Blend( .5f );
		Assert.Equal( 1f, operations.Pose.Positions[ 0 ].X );
		Assert.Equal( 2f, leaf.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void AttributeSamplingKeepsItsOwnOrderAndWeightWithoutTouchingBones()
	{
		var events = new List<string>();
		var operations = new AnimationPoseBufferOperations(
			new IAnimationPreparedPoseNode[] { Reference( Vector3.Zero, 1f ), Reference( Vector3.One, 1f ) },
			child => events.Add( "child" + child ), weight => events.Add( "weight" + weight ) );
		var control = new AnimationDirectionalPoseControl( operations );
		operations.Begin( false, 0 );
		control.Prepare( 0, 1, .5f, .1f, false, false, true );
		var before = operations.Pose.Positions[ 0 ];
		control.Sample( 0, 1, .5f, false, false );
		Assert.Equal( new[] { "child0", "child1", "weight0.5" }, events );
		Assert.Equal( before, operations.Pose.Positions[ 0 ] );
		Assert.Equal( 1, operations.PreparedCount );
	}

	[Fact]
	public void RepeatedCompositionDoesNotAccumulateOrAllocate()
	{
		var operations = Operations( Reference( Vector3.One, .25f ), Reference( Vector3.One, .75f ) );
		var control = new AnimationCompositionPoseControl( operations, true );
		for ( var i = 0; i < 2000; i++ )
		{
			operations.Begin( true, 2 );
			control.Prepare( true );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 2000; i++ )
		{
			operations.Begin( true, 2 );
			control.Prepare( true );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
		Assert.Equal( new Vector3( 2f ), operations.Pose.Positions[ 0 ] );
	}

	private static AnimationPoseBufferOperations Operations( params IAnimationPreparedPoseNode[] nodes ) =>
		new( nodes, _ => throw new InvalidOperationException( "No attribute sampler bound in this pose test." ),
			_ => throw new InvalidOperationException( "No attribute blend bound in this pose test." ) );

	private static AnimationReferencePose Reference( Vector3 position, float mask,
		Quaternion? rotation = null, Vector4? scale = null ) => new( new[]
	{
		new AnimationBoneTransform( new Vector4( position, mask ), rotation ?? Quaternion.Identity, scale ?? Vector4.One )
	} );

	private static AnimationClipPlayback Leaf( float offset, AnimationPoseSamplingContext context, float mask = 1f ) =>
		new( new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
			new[] { 0, 0, 0 }, new[] { 2, 0, 0 },
			new[] { new Vector4( offset, 0f, 0f, mask ), new Vector4( offset + 2f, 0f, 0f, mask ) } ), 0, context );
}
