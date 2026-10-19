using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationIndexedPosePlaybackTests
{
	private static readonly AnimationPoseStreamKey Left = new( 1, 0, 0, 0, 0, 0 );
	private static readonly AnimationPoseStreamKey Right = new( 2, 0, 0, 0, 0, 0 );

	[Fact]
	public void SelectionComposesReferenceOnceAndRetainsLocalMask()
	{
		var playback = Playback();
		var context = Context( 1u << 23 );
		Assert.True( playback.Prepare( context, Left, 100 ) );
		Assert.Equal( new Vector3( 12f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
		Assert.Equal( .25f, playback.Pose.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 8f ), playback.Pose.Scales[ 0 ] );
		Assert.True( playback.Prepare( context, Left, 100 ) );
		Assert.Equal( new Vector3( 12f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void MissingEmptyAndWeightOnlyStreamsReplacePriorResultWithReference()
	{
		var playback = Playback();
		Assert.True( playback.Prepare( Context( 2u << 23 ), Left, 100 ) );
		foreach ( var context in new[] { new AnimationPoseStreamContext( 16 ), Context(), Context( 0x40080000u ) } )
		{
			Assert.False( playback.Prepare( context, Left, 100 ) );
			Assert.Equal( new Vector3( 10f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
			Assert.Equal( .75f, playback.Pose.BoneWeights[ 0 ] );
			Assert.Equal( new Vector4( 4f ), playback.Pose.Scales[ 0 ] );
		}
	}

	[Fact]
	public void PairedSelectionUsesTheRequestedContextOrientation()
	{
		var context = Context( 0u );
		context.Append( 1, 2u << 23 );
		context.SwapPairs = true;
		var playback = Playback();
		Assert.True( playback.Prepare( context, Left, 100 ) );
		Assert.Equal( new Vector3( 13f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
		context.SwapPairs = false;
		Assert.True( playback.Prepare( context, Left, 100 ) );
		Assert.Equal( new Vector3( 11f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void UnreducedOperandsApplyTheTopPose()
	{
		var playback = Playback();
		Assert.True( playback.Prepare( Context( 0u, 2u << 23 ), Left, 100 ) );
		Assert.Equal( new Vector3( 13f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void SampleLimitStopsAfterTheFirstReducedGroup()
	{
		var playback = Playback();
		Assert.True( playback.Prepare( Context( 0u, 1u << 23, 0x400fffffu, 0x80200000u, 2u << 23 ), Left, 0 ) );
		Assert.Equal( new Vector3( 12f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void MultiPoseGroupComposesAfterItsWeightedReduction()
	{
		var playback = Playback();
		Assert.True( playback.Prepare( Context( 0u, 1u << 23, 2u << 23,
			0x40000000u, 0x40000000u, 0x400fffffu, 0x80200000u ), Left, 100 ) );
		Assert.Equal( new Vector3( 13f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void ReferenceAndOutputHaveIndependentStorage()
	{
		var reference = Reference();
		var playback = new AnimationIndexedPosePlayback( Bank(), reference, 3 );
		reference[ 0 ] = default;
		Assert.False( playback.Prepare( Context(), Left, 100 ) );
		playback.Pose.Positions[ 0 ] = Vector3.Zero;
		Assert.False( playback.Prepare( Context(), Left, 100 ) );
		Assert.Equal( new Vector3( 10f, 20f, 30f ), playback.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void ConstructionAndEmptyTopRejectInvalidInputs()
	{
		Assert.Throws<ArgumentNullException>( () => new AnimationIndexedPosePlayback( null, Reference(), 2 ) );
		Assert.Throws<ArgumentNullException>( () => new AnimationIndexedPosePlayback( Bank(), null, 2 ) );
		Assert.Throws<ArgumentException>( () => new AnimationIndexedPosePlayback( Bank(), Array.Empty<AnimationBoneTransform>(), 2 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new AnimationIndexedPosePlayback( Bank(), Reference(), 0 ) );
		Assert.Throws<InvalidOperationException>( () => new AnimationPoseStack( 1, 2 ).Top );
	}

	private static AnimationIndexedPosePlayback Playback() => new( Bank(), Reference(), 4 );

	private static AnimationBoneTransform[] Reference() => new[]
	{
		new AnimationBoneTransform( new Vector4( 10f, 20f, 30f, .75f ), Quaternion.Identity, new Vector4( 4f ) )
	};

	private static AnimationPoseStreamContext Context( params uint[] commands )
	{
		var context = new AnimationPoseStreamContext( 16 );
		context.Register( Left, Right );
		foreach ( var command in commands )
			context.Append( 0, command );
		return context;
	}

	private static AnimationIndexedPoseBank Bank()
	{
		var keys = new[] { new Vector4( 1f, 0f, 0f, .25f ), new Vector4( 2f, 0f, 0f, .25f ),
			new Vector4( 3f, 0f, 0f, .25f ), new Vector4( 0f, 0f, 0f, 1f ), new Vector4( 2f ) };
		return new AnimationIndexedPoseBank( new RawAnimationClip( "poses", 1f, 1f, 3, 0, 0,
			new[] { 0, 3, 4 }, new[] { 3, 1, 1 }, keys, null, null, AnimationClipSamplingMode.Continuous ) );
	}
}
