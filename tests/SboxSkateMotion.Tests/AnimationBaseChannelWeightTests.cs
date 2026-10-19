using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationBaseChannelWeightTests
{
	[Fact]
	public void ChannelTransitionUsesTheRequestedPreparedInputMask()
	{
		foreach ( var usePrevious in new[] { false, true } )
		{
			var owner = new AnimationBasePoseTreeOwner( new Factory() );
			owner.Replace( new( 0, AnimationBaseTransition.Play ) );
			Advance( owner, .125f );
			owner.Replace( new( 1, AnimationBaseTransition.ChannelBlend, Duration: 1f,
				Synchronization: AnimationTransitionMode.MatchTime, UseChannelFromWeights: usePrevious ) );
			Advance( owner, .5f );
			Assert.Equal( .625f, owner.Elapsed );
			AssertPose( owner, usePrevious ? 1.25f : 3.75f );
			Assert.Equal( 1, owner.RetainedTransitionCount );
		}
	}

	[Fact]
	public void ReusedTransitionSlotResetsBothWeightedModeAndMaskSelection()
	{
		var owner = new AnimationBasePoseTreeOwner( new Factory() );
		owner.Replace( new( 0, AnimationBaseTransition.Play ) );
		owner.Replace( new( 1, AnimationBaseTransition.ChannelBlend, Duration: 1f ) );
		var wrapper = owner.Root;
		Advance( owner, .5f );
		AssertPose( owner, 3.75f );
		Advance( owner, .75f );
		Assert.Equal( 1, owner.CleanupCompleted() );

		owner.Replace( new( 2, AnimationBaseTransition.Blend, Duration: 1f ) );
		Assert.Same( wrapper, owner.Root );
		Advance( owner, .5f );
		AssertPose( owner, 15f );
		Advance( owner, .75f );
		Assert.Equal( 1, owner.CleanupCompleted() );

		owner.Replace( new( 3, AnimationBaseTransition.ChannelBlend, Duration: 1f,
			UseChannelFromWeights: true ) );
		Assert.Same( wrapper, owner.Root );
		Advance( owner, .5f );
		AssertPose( owner, 21.25f );
	}

	private static void Advance( AnimationBasePoseTreeOwner owner, float delta )
	{
		var completion = AnimationPlaybackCompletion.Empty;
		owner.Advance( delta, 0f, ref completion );
	}

	private static void AssertPose( AnimationBasePoseTreeOwner owner, float x )
	{
		Assert.True( owner.Prepare( true, false, 0 ) );
		Assert.Equal( x, owner.Pose.Positions[ 0 ].X );
	}

	private sealed class Tree : IAnimationPlaybackTree
	{
		internal Tree( int definition )
		{
			var mask = (definition & 1) == 0 ? .25f : .75f;
			var clip = new RawAnimationClip( "sample", 1f, 1f, 2, 0, 0,
				new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { new Vector4( definition * 10f, 0, 0, mask ) } );
			Root = new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
		}

		public IAnimationPlaybackNode Root { get; }
	}

	private sealed class Factory : IAnimationBasePoseTreeFactory
	{
		public IAnimationPlaybackTree Create( int storageSlot, int definition ) => new Tree( definition );
		public void Publish( IAnimationPosePlaybackNode root ) { }
	}
}
