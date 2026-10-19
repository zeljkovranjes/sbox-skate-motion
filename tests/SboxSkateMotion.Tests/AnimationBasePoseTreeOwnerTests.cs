using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationBasePoseTreeOwnerTests
{
	[Fact]
	public void UnderInsertionPreservesTheOutgoingBlendAndDelaysTheQueuedPose()
	{
		var factory = new Factory();
		var owner = new AnimationBasePoseTreeOwner( factory );
		owner.Replace( new( 1, AnimationBaseTransition.Play ) );
		owner.Replace( new( 2, AnimationBaseTransition.Blend, Duration: .25f ) );
		Advance( owner, .125f );
		AssertPose( owner, 15f );
		var root = owner.Root;
		owner.Replace( new( 3, AnimationBaseTransition.Sequence, Under: true ) );
		Assert.Same( root, owner.Root );
		Assert.Equal( 2, factory.Publications );
		AssertPose( owner, 15f );
		Advance( owner, .125f );
		Assert.Equal( 0, owner.CleanupCompleted() );
		AssertPose( owner, 20f );
		Advance( owner, .125f );
		Assert.Equal( 1, owner.CleanupCompleted() );
		Assert.IsType<AnimationSequencePosePlayback>( owner.Root );
		Advance( owner, .75f );
		Assert.Equal( 1, owner.CleanupCompleted() );
		Assert.Equal( .125f, owner.Elapsed );
		AssertPose( owner, 30f );
	}

	[Fact]
	public void CapacityPressureReusesReturnedStorageAndFailureDoesNotPublish()
	{
		var factory = new Factory();
		var owner = new AnimationBasePoseTreeOwner( factory );
		owner.Replace( new( 1, AnimationBaseTransition.Play ) );
		owner.Replace( new( 2, AnimationBaseTransition.Sequence ) );
		owner.Replace( new( 3, AnimationBaseTransition.Blend ) );
		owner.Replace( new( 4, AnimationBaseTransition.Blend, Under: true ) );
		owner.Replace( new( 5, AnimationBaseTransition.Sequence ) );
		Assert.Equal( new[] { 3, 2, 1, 0, 3 }, factory.Slots );
		Assert.Equal( 3, owner.RetainedCount );
		var root = owner.Root;
		var publications = factory.Publications;
		Assert.False( owner.Replace( new( 99, AnimationBaseTransition.Blend ) ) );
		Assert.Same( root, owner.Root );
		Assert.Equal( publications, factory.Publications );
		Assert.Equal( 0, owner.RetainedCount );
		Assert.True( owner.Replace( new( 7, AnimationBaseTransition.Play ) ) );
		Assert.Equal( 3, factory.Slots[ ^1 ] );
		AssertPose( owner, 70f );
	}

	[Fact]
	public void CompletedOuterSequenceRecursivelyReturnsBothChildrenOfItsUnfinishedTransition()
	{
		var factory = new Factory();
		var owner = new AnimationBasePoseTreeOwner( factory );
		owner.Replace( new( 1, AnimationBaseTransition.Play ) );
		owner.Replace( new( 2, AnimationBaseTransition.Blend, Duration: 10f ) );
		owner.Replace( new( 3, AnimationBaseTransition.Sequence ) );
		Advance( owner, 2f );
		Assert.Equal( 2, owner.CleanupCompleted() );
		Assert.Equal( 0, owner.RetainedTransitionCount );
		AssertPose( owner, 30f );
		owner.Replace( new( 4, AnimationBaseTransition.Sequence ) );
		Assert.Equal( 2, factory.Slots[ ^1 ] );
	}

	[Fact]
	public void FirstQueuedRequestFallsBackToImmediatePlayWithoutSeeking()
	{
		var factory = new Factory();
		var owner = new AnimationBasePoseTreeOwner( factory );
		Assert.True( owner.Replace( new( 1, AnimationBaseTransition.Sequence, Rate: 2f, StartTime: .25f ) ) );
		Assert.Equal( .5f, owner.Duration );
		Assert.Equal( 0f, owner.Elapsed );
		Assert.Equal( 0, owner.RetainedCount );
		owner.Replace( new( 2, AnimationBaseTransition.Play, StartTime: .25f ) );
		Assert.Equal( .25f, owner.Elapsed );
		Assert.Equal( new[] { 3, 3 }, factory.Slots );
	}

	private static void Advance( AnimationBasePoseTreeOwner owner, float delta )
	{
		var completion = AnimationPlaybackCompletion.Empty;
		owner.Advance( delta, 0f, ref completion );
	}

	private static void AssertPose( AnimationBasePoseTreeOwner owner, float position )
	{
		Assert.True( owner.Prepare( true, false, 0 ) );
		Assert.Equal( position, owner.Pose.Positions[ 0 ].X );
	}

	private sealed class Tree : IAnimationPlaybackTree
	{
		internal Tree( int definition )
		{
			var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
				new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { new Vector4( definition * 10f, 0, 0, 1 ) } );
			Root = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		}

		public IAnimationPlaybackNode Root { get; }
	}

	private sealed class Factory : IAnimationBasePoseTreeFactory
	{
		internal readonly List<int> Slots = new();
		internal int Publications;
		public IAnimationPlaybackTree Create( int storageSlot, int definition )
		{
			Slots.Add( storageSlot );
			return definition == 99 ? null : new Tree( definition );
		}
		public void Publish( IAnimationPosePlaybackNode root ) => Publications++;
	}
}
