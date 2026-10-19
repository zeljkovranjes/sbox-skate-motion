using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationChannelPoseTreeOwnerTests
{
	private static readonly AnimationPoseStreamKey Name = new( 1, 2, 3, 4, 5, 6 );

	[Fact]
	public void InterruptedTransitionRebindsBodyAndStreamsToPromotedChildren()
	{
		var factory = new Factory();
		var owner = factory.Owner;
		owner.Replace( Request( 1 ) );
		var composite = owner.Root;
		Advance( owner, .125f );
		AssertPose( owner, 5f );
		owner.Replace( Request( 2 ) );
		Assert.Same( composite, owner.Root );
		AssertPose( owner, 10f );
		Assert.Equal( new[] { 4u << 23 }, Commands( owner ) );
		Advance( owner, .125f );
		AssertPose( owner, 15f );
		Assert.Equal( new[] { 4u << 23, 6u << 23, 0x4007ffffu, 0x80200000u }, Commands( owner ) );
		Assert.Equal( new[] { "create:1", "commit:1", "release:0:root:1:pending:True", "create:2", "commit:2" }, factory.Events );
	}

	[Fact]
	public void CleanupPublishesSuccessorBeforeReleaseCallbackSamplesIt()
	{
		var factory = new Factory();
		factory.Owner.Replace( Request( 1 ) );
		Advance( factory.Owner, .25f );
		Assert.False( factory.Owner.CleanupCompleted() );
		factory.OnRelease = () =>
		{
			Assert.Same( factory.Trees[ 1 ].Root, factory.Owner.Root );
			Assert.True( factory.Owner.HasPending );
			AssertPose( factory.Owner, 10f );
			Assert.Equal( new[] { 4u << 23 }, Commands( factory.Owner ) );
		};
		Advance( factory.Owner, .125f );
		Assert.True( factory.Owner.CleanupCompleted() );
		Assert.False( factory.Owner.HasPending );
	}

	[Fact]
	public void FailedFactoryKeepsPromotedBodyAndStreams()
	{
		var factory = new Factory();
		factory.Owner.Replace( Request( 1 ) );
		factory.Fail = true;
		Assert.Equal( AnimationChannelReplaceResult.MissingTree, factory.Owner.Replace( Request( 2 ) ) );
		Assert.False( factory.Owner.HasPending );
		AssertPose( factory.Owner, 10f );
		Assert.Equal( new[] { 4u << 23 }, Commands( factory.Owner ) );
	}

	[Fact]
	public void AdmissionFailureKeepsPendingPoseTree()
	{
		var factory = new Factory();
		factory.Owner.Replace( Request( 1 ) );
		var root = factory.Owner.Root;
		factory.Blend.Stop( .5f, true );
		factory.Blend.Update( .125f, 1f, 0f );
		Assert.Equal( AnimationChannelReplaceResult.FadingOut, factory.Owner.Replace( Request( 2 ) ) );
		Assert.Same( root, factory.Owner.Root );
		Assert.True( factory.Owner.HasPending );
		Assert.Equal( 2, factory.Events.Count );
		AssertPose( factory.Owner, 0f );
		Assert.Equal( new[] { 2u << 23 }, Commands( factory.Owner ) );
	}

	[Fact]
	public void ReusedSequenceClearsHandoffAndRebindsItsPoseChildren()
	{
		var factory = new Factory();
		var owner = factory.Owner;
		owner.Replace( Request( 1 ) with { Transition = false } );
		var sequence = owner.Root;
		Advance( owner, 1.125f );
		Assert.True( owner.CleanupCompleted() );
		AssertPose( owner, 10f );
		owner.Replace( Request( 2 ) with { Transition = false } );
		Assert.Same( sequence, owner.Root );
		Assert.False( ((AnimationSequencePosePlayback)sequence).Playback.ActiveNext );
		AssertPose( owner, 10f );
		Assert.Equal( new[] { 4u << 23 }, Commands( owner ) );
		Advance( owner, 1f );
		AssertPose( owner, 20f );
		Assert.Equal( new[] { 6u << 23 }, Commands( owner ) );
	}

	[Fact]
	public void InvalidPairResetLeavesClockAndPoseChildrenUnchanged()
	{
		var first = new Tree( 0 );
		var second = new Tree( 1 );
		var node = new AnimationSequencePosePlayback( (IAnimationPosePlaybackNode)first.Root, (IAnimationPosePlaybackNode)second.Root );
		Assert.Throws<ArgumentNullException>( () => node.Reset( (IAnimationPosePlaybackNode)second.Root, null ) );
		Assert.True( node.Prepare( true, false, 0 ) );
		Assert.Equal( 0f, node.Pose.Positions[ 0 ].X );
		var context = new AnimationPoseStreamContext( 16 );
		node.SamplePoseStreams( context );
		Assert.Equal( 2u << 23, context.Commands( 0 )[ 0 ] );
	}

	private static AnimationChannelReplacement Request( int id ) => new( id, 1f, false, true, .25f, .25f, 0f, 0u, false );
	private static void Advance( AnimationChannelPoseTreeOwner owner, float delta )
	{
		var completion = AnimationPlaybackCompletion.Empty;
		owner.Advance( delta, 0f, ref completion );
	}

	private static void AssertPose( AnimationChannelPoseTreeOwner owner, float position )
	{
		Assert.True( owner.Prepare( true, false, 0 ) );
		Assert.Equal( position, owner.Pose.Positions[ 0 ].X );
	}

	private static uint[] Commands( AnimationChannelPoseTreeOwner owner )
	{
		var context = new AnimationPoseStreamContext( 32 );
		owner.SamplePoseStreams( context );
		return context.Commands( 0 ).ToArray();
	}

	private sealed class Tree : IAnimationPlaybackTree
	{
		internal Tree( int id )
		{
			Id = id;
			var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
				new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { new Vector4( id * 10f, 0f, 0f, 1f ) }, null,
				new[] { new AnimationPoseStreamTrack( Name, Name, new[] { new AnimationPoseStreamKeyframe( 8191, (ushort)( 2 + id * 2 ) ) } ) } );
			Root = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		}

		internal int Id { get; }
		public IAnimationPlaybackNode Root { get; }
	}

	private sealed class Factory : IAnimationChannelTreeFactory
	{
		internal readonly Tree[] Trees = [ new( 0 ), new( 1 ), new( 2 ) ];
		internal readonly List<string> Events = new();
		internal readonly AnimationChannelBlend Blend = new( new AnimationChannelBlendState( 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0u ) );
		internal readonly AnimationChannelPoseTreeOwner Owner;
		internal bool Fail;
		internal Action OnRelease;

		internal Factory() => Owner = new AnimationChannelPoseTreeOwner( Trees[ 0 ], this, Blend );

		public IAnimationPlaybackTree Create( AnimationChannelReplacement request )
		{
			Events.Add( $"create:{request.Definition}" );
			return Fail ? null : Trees[ request.Definition ];
		}

		public void Commit( IAnimationPlaybackTree tree ) => Events.Add( $"commit:{((Tree)tree).Id}" );

		public void Release( IAnimationPlaybackTree tree )
		{
			var promoted = Array.FindIndex( Trees, candidate => ReferenceEquals( candidate.Root, Owner.Root ) );
			Events.Add( $"release:{((Tree)tree).Id}:root:{promoted}:pending:{Owner.HasPending}" );
			OnRelease?.Invoke();
		}
	}
}
