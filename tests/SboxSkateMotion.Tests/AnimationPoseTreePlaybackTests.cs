using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPoseTreePlaybackTests
{
	private static readonly AnimationPoseStreamKey Name = new( 1, 2, 3, 4, 5, 6 );

	[Fact]
	public void SequenceHandoffChangesBodyAndStreamsTogether()
	{
		var first = Leaf( 10f, 2 );
		var second = Leaf( 20f, 6 );
		var node = new AnimationSequencePosePlayback( first, second );
		var completion = AnimationPlaybackCompletion.Empty;
		node.Advance( 1.125f, 0f, ref completion );
		Assert.True( node.Playback.ActiveNext );
		Assert.Equal( .125f, second.Elapsed );
		Assert.True( node.Prepare( true, false, 0 ) );
		Assert.Equal( 20f, node.Pose.Positions[ 0 ].X );
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		Assert.Equal( new[] { 6u << 23 }, context.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void TransitionUsesOneFadeForBodyAndStreamBlends()
	{
		var node = new AnimationTransitionPosePlayback( Leaf( 0f, 2 ), Leaf( 10f, 6 ),
			1f, AnimationTransitionMode.Both, 0 );
		var completion = AnimationPlaybackCompletion.Empty;
		node.Advance( .5f, 0f, ref completion );
		Assert.True( node.Prepare( true, false, 0 ) );
		Assert.Equal( 5f, node.Pose.Positions[ 0 ].X );
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		Assert.Equal( new[] { 2u << 23, 6u << 23, 0x4007ffffu, 0x80200000u }, context.Commands( 0 ).ToArray() );
		Assert.Equal( .5f, node.Elapsed );
	}

	[Fact]
	public void ChannelMaskDoesNotReplaceStreamWeight()
	{
		var node = new AnimationChannelPosePlayback( Leaf( 0f, 2 ), Leaf( 10f, 6, .25f ), Blend( .5f ) );
		Assert.True( node.Prepare( true, false, 0 ) );
		Assert.Equal( 1.25f, node.Pose.Positions[ 0 ].X );
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		Assert.Equal( 0x4007ffffu, context.Commands( 0 )[ 2 ] );
	}

	[Fact]
	public void DirectionalSelectionIsSharedAndOwnsItsChildArray()
	{
		var children = new IAnimationPosePlaybackNode[] { Leaf( 0f, 2 ), Leaf( 10f, 6 ) };
		var node = new AnimationDirectionalPosePlayback( children, new[] { 0f, 1f } );
		children[ 0 ] = Leaf( 100f, 50 );
		node.Playback.UpdateParameter( .5f, false );
		Assert.True( node.Prepare( true, false, 0 ) );
		Assert.Equal( 5f, node.Pose.Positions[ 0 ].X );
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		Assert.Equal( 2u << 23, context.Commands( 0 )[ 0 ] );
		Assert.Equal( 6u << 23, context.Commands( 0 )[ 1 ] );
		node.SetRate( 2f );
		Assert.Equal( .5f, node.Duration );
	}

	[Fact]
	public void DirectionalEndpointIdentityIncludesAliasedChildren()
	{
		var shared = Leaf( 0f, 2 );
		var node = new AnimationDirectionalPosePlayback( new IAnimationPosePlaybackNode[] { shared, shared, Leaf( 10f, 6 ) },
			new[] { 0f, .5f, 1f } );
		node.Playback.UpdateParameter( .75f, false );
		Assert.Equal( 1, node.Playback.FirstIndex );
		Assert.True( node.Playback.FirstIsEndpoint );
		Assert.True( node.Playback.SecondIsEndpoint );
	}

	[Fact]
	public void PreparationAndTraversalKeepContextsAndHistorySeparate()
	{
		var first = Leaf( 0f, 2 );
		var node = new AnimationSequencePosePlayback( first, Leaf( 10f, 6 ) );
		var completion = AnimationPlaybackCompletion.Empty;
		node.Advance( .25f, 0f, ref completion );
		var a = new AnimationPoseStreamContext( 16 );
		var b = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( a );
		Assert.Equal( 0f, first.Clock.PreviousPosition );
		node.Prepare( true, true, 0 );
		Assert.Equal( .25f, first.Clock.PreviousPosition );
		Assert.Single( a.Commands( 0 ).ToArray() );
		node.SamplePoseStreams( b );
		node.SamplePoseStreams( a );
		Assert.Equal( 2, a.Commands( 0 ).Length );
		Assert.Single( b.Commands( 0 ).ToArray() );
	}

	[Fact]
	public void NestedPreparationAndTraversalReuseStorage()
	{
		var transition = new AnimationTransitionPosePlayback( Leaf( 0f, 2 ), Leaf( 10f, 6 ),
			1f, AnimationTransitionMode.Both, 0 );
		var node = new AnimationChannelPosePlayback( Leaf( 4f, 3 ), transition, Blend( .5f ) );
		var context = new AnimationPoseStreamContext( 64 );
		var pose = node.Pose;
		for ( var n = 0; n < 2000; n++ )
		{
			context.Reset();
			node.Prepare( true, false, 0 );
			node.SamplePoseStreams( context );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var n = 0; n < 2000; n++ )
		{
			context.Reset();
			node.Prepare( true, false, 0 );
			node.SamplePoseStreams( context );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
		Assert.Same( pose, node.Pose );
	}

	private static AnimationChannelBlend Blend( float weight ) => new(
		new AnimationChannelBlendState( weight, weight, 0f, 0f, 0f, 0f, 0f, 0f, 0u ) );

	private static AnimationClipPlayback Leaf( float position, ushort value, float mask = 1f )
	{
		var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
			new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { new Vector4( position, 0f, 0f, mask ) }, null,
			new[] { new AnimationPoseStreamTrack( Name, Name, new[] { new AnimationPoseStreamKeyframe( 8191, value ) } ) } );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}
}
