using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RawSkeletonPoseTests
{
	[Fact]
	public void ReferencedSamplingAndCopyRetainEveryTransformLane()
	{
		var pose = new SkeletonPose( 1 );
		pose.Sample( Clip(), 0f, false );
		var copy = new SkeletonPose( 1 );
		copy.CopyFrom( pose );
		Assert.Equal( new Vector4( 2f, 3f, 4f, .25f ), copy.Transform( 0 ).Position );
		Assert.Equal( new Quaternion( 0f, 0f, 0f, 2f ), copy.Rotations[ 0 ] );
		Assert.Equal( new Vector4( 3f, 4f, 5f, 6f ), copy.Scales[ 0 ] );
		pose.Sample( Clip(), 0f, false );
		Assert.Equal( copy.Scales[ 0 ], pose.Scales[ 0 ] );
	}

	[Theory]
	[InlineData( true, 1f, .3125f, 3f )]
	[InlineData( false, 3f, .4375f, 5f )]
	public void WeightedBlendUsesMaskIndependentlyOfAuxiliaryScale( bool firstMask,
		float position, float mask, float scaleW )
	{
		var first = Pose( 0f, .25f, 2f );
		var second = Pose( 8f, .75f, 10f );
		first.BlendWeightedToward( second, .5f, firstMask );
		Assert.Equal( position, first.Positions[ 0 ].X );
		Assert.Equal( mask, first.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( scaleW ), first.Scales[ 0 ] );
	}

	[Fact]
	public void UniformBlendInterpolatesScaleAndMaskSeparately()
	{
		var first = Pose( 0f, .25f, 2f );
		first.BlendToward( Pose( 8f, .75f, 10f ), .5f );
		Assert.Equal( 4f, first.Positions[ 0 ].X );
		Assert.Equal( .5f, first.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 6f ), first.Scales[ 0 ] );
	}

	[Fact]
	public void RestAndLegacySamplingResetScale()
	{
		var skeleton = new ClipSkeleton( new[] { "root" }, new[] { -1 },
			new[] { Vector3.Zero }, new[] { Quaternion.Identity } );
		var legacy = new AnimationClip( "legacy", 30f, 1, new[] { 0 }, new[] { 0 },
			Array.Empty<Quaternion>(), new[] { 0 }, new[] { 0 }, Array.Empty<Vector3>() );
		var pose = Pose( 0f, .25f, 7f );
		pose.SetRest( skeleton );
		Assert.Equal( Vector4.One, pose.Scales[ 0 ] );
		pose.Scales[ 0 ] = new Vector4( 7f );
		pose.Sample( legacy, skeleton, 0f, false );
		Assert.Equal( Vector4.One, pose.Scales[ 0 ] );
		Assert.Equal( 1f, pose.BoneWeights[ 0 ] );
	}

	[Fact]
	public void ReferencedPoseSamplingAndBlendingReuseBuffers()
	{
		var clip = Clip();
		var first = new SkeletonPose( 1 );
		var second = Pose( 0f, .5f, 2f );
		for ( var i = 0; i < 1000; i++ )
		{
			first.Sample( clip, 0f, false );
			first.BlendWeightedToward( second, .5f, false );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 1000; i++ )
		{
			first.Sample( clip, 0f, false );
			first.BlendWeightedToward( second, .5f, false );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}

	private static SkeletonPose Pose( float position, float mask, float scale )
	{
		var pose = new SkeletonPose( 1 );
		pose.Positions[ 0 ] = new Vector3( position, 0f, 0f );
		pose.Rotations[ 0 ] = Quaternion.Identity;
		pose.BoneWeights[ 0 ] = mask;
		pose.Scales[ 0 ] = new Vector4( scale );
		return pose;
	}

	private static ReferencedAnimationClip Clip()
	{
		var clip = new RawAnimationClip( "sample", 30f, 1f, 1, 0, 0,
			new[] { 0, 1, 2 }, new[] { 1, 1, 1 },
			new[] { new Vector4( 2f, 3f, 4f, .25f ), new Vector4( 0f, 0f, 0f, 2f ), new Vector4( 3f, 4f, 5f, 6f ) } );
		return new ReferencedAnimationClip( clip, new[] {
			new AnimationBoneTransform( new Vector4( 0f, 0f, 0f, 1f ), Quaternion.Identity, Vector4.One ) } );
	}
}
