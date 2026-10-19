using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationClipPlaybackTests
{
	[Theory]
	[InlineData( 0u, 1u, false )]
	[InlineData( 1u, 0u, true )]
	[InlineData( 2u, 0u, false )]
	[InlineData( 2u, 1u, true )]
	[InlineData( 2u, 2u, false )]
	[InlineData( 3u, 1u, false )]
	[InlineData( 2147483649u, 1u, false )]
	[InlineData( 4294967295u, 1u, false )]
	public void ImmediatePolicySubstitutesContextOnce( uint flags, uint policy, bool interpolate )
	{
		var context = new AnimationPoseSamplingContext( policy, false );
		var leaf = new AnimationClipPlayback( Clip(), 42, context );
		leaf.Seek( .5f );
		Assert.True( leaf.Prepare( true, false, flags ) );
		Assert.InRange( leaf.Pose.Positions[ 0 ].X, interpolate ? 10.9999f : 10f, interpolate ? 11f : 10f );
		Assert.Equal( .25f, leaf.Pose.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 2f, 3f, 4f, interpolate ? 3f : 5f ), leaf.Pose.Scales[ 0 ] );
	}

	[Fact]
	public void ClockRateAndConsumptionProduceOneMotionInterval()
	{
		var leaf = new AnimationClipPlayback( Clip(), 42, new AnimationPoseSamplingContext( 1, true ) );
		leaf.Seek( .25f );
		leaf.SetRate( 2f );
		Assert.Equal( .125f, leaf.Elapsed );
		var completion = default( AnimationPlaybackCompletion );
		leaf.Advance( .25f, 0f, ref completion );
		Assert.True( leaf.Prepare( true, true, 2 ) );
		Assert.InRange( leaf.Pose.Positions[ 0 ].X, .9999f, 1.0001f );
		Assert.Equal( leaf.Clock.Position, leaf.Clock.PreviousPosition );
		leaf.Prepare( true, true, 2 );
		Assert.Equal( Vector3.Zero, leaf.Pose.Positions[ 0 ] );
	}

	[Fact]
	public void ExactEndpointDoesNotWrapAndLaterAdvanceRetainsCycle()
	{
		var leaf = new AnimationClipPlayback( Clip(), 42, new AnimationPoseSamplingContext( 1, true ) );
		var completion = default( AnimationPlaybackCompletion );
		leaf.Advance( 1f, 0f, ref completion );
		Assert.False( completion.Finished );
		leaf.Prepare( true, true, 2 );
		Assert.Equal( 2f, leaf.Pose.Positions[ 0 ].X );
		leaf.Advance( .25f, 0f, ref completion );
		Assert.True( completion.Finished );
		Assert.Equal( 1u, leaf.Clock.WrapCount );
		leaf.Prepare( true, false, 2 );
		Assert.InRange( leaf.Pose.Positions[ 0 ].X, 8.4999f, 8.5f );
		Assert.Equal( 1u, leaf.Clock.WrapCount );
		leaf.Prepare( true, true, 2 );
		Assert.Equal( 0u, leaf.Clock.WrapCount );
	}

	[Theory]
	[InlineData( 1u )]
	[InlineData( 2u )]
	[InlineData( 2147483648u )]
	[InlineData( 4294967295u )]
	public void BackendPreservesNonzeroUnsignedWrapMeaning( uint wraps )
	{
		var pose = new SkeletonPose( 1 );
		var backend = new AnimationClipPoseBackend( Clip(), 65535, new AnimationPoseSamplingContext( 0, true ), pose );
		Assert.Equal( (ushort)65535, backend.ResolveClip() );
		backend.Sample( 65535, 1f, 0f, wraps, 2 );
		Assert.Equal( 8f, pose.Positions[ 0 ].X );
		Assert.Equal( new Vector4( 2f, 3f, 4f, 5f ), pose.Scales[ 0 ] );
		Assert.Throws<ArgumentException>( () => backend.Sample( 1, 0f, 0f, 0, 0 ) );
		Assert.Equal( 8f, pose.Positions[ 0 ].X );
	}

	[Fact]
	public void SuppressionAndDisabledPreparationConsumeOnlyRequestedHistory()
	{
		var context = new AnimationPoseSamplingContext( 0, true );
		var leaf = new AnimationClipPlayback( Clip(), 42, context );
		var completion = default( AnimationPlaybackCompletion );
		leaf.Advance( 1f, 0f, ref completion );
		leaf.Prepare( true, true, 0 );
		context.Suppressed = true;
		leaf.Advance( .5f, 0f, ref completion );
		Assert.False( leaf.Prepare( true, false, 0 ) );
		Assert.False( leaf.HasPose );
		Assert.Equal( 1u, leaf.Clock.WrapCount );
		Assert.Equal( 2f, leaf.Pose.Positions[ 0 ].X );
		Assert.False( leaf.Prepare( true, true, 0 ) );
		Assert.Equal( 0u, leaf.Clock.WrapCount );
		context.Suppressed = false;
		Assert.False( leaf.Prepare( false, true, 0 ) );
		Assert.Equal( 2f, leaf.Pose.Positions[ 0 ].X );
		Assert.True( leaf.Prepare( true, true, 0 ) );
		Assert.Equal( 0f, leaf.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void PhaseReversalUsesCycleAndSeekClearsIt()
	{
		var leaf = new AnimationClipPlayback( Clip( flags: 3 ), 0, new AnimationPoseSamplingContext( 1, true ) );
		var completion = default( AnimationPlaybackCompletion );
		leaf.Advance( 0f, .75f, ref completion );
		leaf.Prepare( true, true, 2 );
		leaf.Advance( 0f, .25f, ref completion );
		leaf.Prepare( true, false, 2 );
		Assert.InRange( leaf.Pose.Positions[ 0 ].X, 8.9999f, 9.0001f );
		leaf.Seek( .5f );
		leaf.Prepare( true, false, 2 );
		Assert.Equal( 0u, leaf.Clock.WrapCount );
		Assert.Equal( 0f, leaf.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void UnknownTrajectoryIsAllowedOnlyForLocalSampling()
	{
		var clip = Clip( metadata: false );
		Assert.Throws<ArgumentException>( () => new AnimationClipPlayback( clip, 0, new AnimationPoseSamplingContext( 0, true ) ) );
		var leaf = new AnimationClipPlayback( clip, 0, new AnimationPoseSamplingContext( 0, false ) );
		leaf.Seek( 1f );
		leaf.Prepare( true, true, 0 );
		Assert.Equal( 12f, leaf.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void InvalidTimingCannotEnterTimedLoop()
	{
		Assert.Throws<ArgumentOutOfRangeException>( () => new AnimationClipPlayback( Clip( frames: 1 ), 0,
			new AnimationPoseSamplingContext( 0, false ) ) );
		var leaf = new AnimationClipPlayback( Clip(), 0, new AnimationPoseSamplingContext( 0, false ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => leaf.SetRate( 0f ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => leaf.SetRate( float.NaN ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => leaf.Seek( float.PositiveInfinity ) );
		Assert.Equal( 1f, leaf.Duration );
	}

	[Fact]
	public void RepeatedPlaybackAndPreparationReusePoseStorage()
	{
		var leaf = new AnimationClipPlayback( Clip(), 0, new AnimationPoseSamplingContext( 1, true ) );
		var pose = leaf.Pose;
		var completion = default( AnimationPlaybackCompletion );
		for ( var i = 0; i < 2000; i++ )
		{
			leaf.Advance( .125f, 0f, ref completion );
			leaf.Prepare( true, true, 2 );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 2000; i++ )
		{
			leaf.Advance( .125f, 0f, ref completion );
			leaf.Prepare( true, true, 2 );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
		Assert.Same( pose, leaf.Pose );
	}

	private static RawAnimationClip Clip( byte flags = 1, int frames = 2, bool metadata = true ) => new(
		"sample", 1f, 1f, frames, 0, flags, new[] { 0, 0, 2 }, new[] { 2, 0, 1 },
		new[] { new Vector4( 10f, 0f, 0f, .25f ), new Vector4( 12f, 0f, 0f, .25f ), new Vector4( 2f, 3f, 4f, 5f ) },
		metadata ? new AnimationClipTrajectory( 0, new Vector3( 10f, 0f, 0f ), Quaternion.Identity ) : null );
}
