using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPoseStackTests
{
	[Fact]
	public void MultiBlendAccumulatesRawRotationsBeforeNormalizingAndPreservesEveryLane()
	{
		var poses = new[]
		{
			Pose( 0f, .25f, new Quaternion( 0f, 0f, 0f, 2f ), 2f ),
			Pose( 8f, .5f, new Quaternion( 0f, 0f, 2f, -2f ), 4f ),
			Pose( 16f, .75f, new Quaternion( 0f, 0f, 4f, 0f ), 8f )
		};
		var result = new SkeletonPose( 1 );
		result.BlendMany( poses, new[] { .25f, .25f, .5f } );
		Assert.Equal( new Vector3( 10f, 20f, 30f ), result.Positions[ 0 ] );
		Assert.Equal( .5625f, result.BoneWeights[ 0 ] );
		Assert.Equal( new Vector4( 5.5f, 6.5f, 7.5f, 8.5f ), result.Scales[ 0 ] );
		Assert.InRange( result.Rotations[ 0 ].Z, .999999f, 1.000001f );
		Assert.Equal( 0f, result.Rotations[ 0 ].W );
	}

	[Fact]
	public void SingleInputStillNormalizesItsRotation()
	{
		var pose = Pose( 4f, .25f, new Quaternion( 0f, 0f, 0f, 2f ) );
		pose.BlendMany( new[] { pose }, new[] { 1f } );
		Assert.Equal( Quaternion.Identity, pose.Rotations[ 0 ] );
		Assert.Equal( 4f, pose.Positions[ 0 ].X );
		Assert.Equal( .25f, pose.BoneWeights[ 0 ] );
	}

	[Fact]
	public void NestedReductionConsumesOnlyTheMostRecentOperands()
	{
		var stack = new AnimationPoseStack( 1, 4 );
		stack.Push( Pose( 10f ) );
		stack.Push( Pose( 20f ) );
		stack.Push( Pose( 40f ) );
		stack.BlendMany( new[] { .25f, .75f } );
		Assert.Equal( 2, stack.Count );
		Assert.Equal( 10f, stack.Pose.Positions[ 0 ].X );
		stack.Blend( .5f );
		Assert.Equal( 1, stack.Count );
		Assert.Equal( 22.5f, stack.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void PushSnapshotsSharedOutputAndClearReusesTheResultBuffer()
	{
		var stack = new AnimationPoseStack( 1, 2 );
		var result = stack.Pose;
		var source = Pose( 4f );
		stack.Push( source );
		source.Positions[ 0 ] = new Vector3( 12f );
		stack.Push( source );
		source.Positions[ 0 ] = Vector3.Zero;
		stack.BlendMany( new[] { .5f, .5f } );
		Assert.Equal( 8f, stack.Pose.Positions[ 0 ].X );
		stack.Clear();
		Assert.Equal( 0, stack.Count );
		stack.Push( Pose( 7f ) );
		Assert.Same( result, stack.Pose );
		Assert.Equal( 7f, result.Positions[ 0 ].X );
	}

	[Fact]
	public void InvalidShapesAndCapacityLeaveExistingOperandsUntouched()
	{
		var stack = new AnimationPoseStack( 1, 1 );
		stack.Push( Pose( 7f ) );
		Assert.Throws<InvalidOperationException>( () => stack.Push( Pose( 9f ) ) );
		Assert.Throws<ArgumentException>( () => stack.Push( new SkeletonPose( 2 ) ) );
		Assert.Throws<InvalidOperationException>( () => stack.BlendMany( Array.Empty<float>() ) );
		Assert.Throws<InvalidOperationException>( () => stack.Blend( .5f ) );
		Assert.Equal( 1, stack.Count );
		Assert.Equal( 7f, stack.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void TooManyBlendInputsAreRejectedBeforeChangingTheStack()
	{
		var stack = new AnimationPoseStack( 1, 6 );
		for ( var i = 0; i < 6; i++ )
			stack.Push( Pose( i ) );
		Assert.Throws<ArgumentException>( () => stack.BlendMany( new[] { .2f, .2f, .2f, .2f, .1f, .1f } ) );
		Assert.Equal( 6, stack.Count );
		Assert.Equal( 0f, stack.Pose.Positions[ 0 ].X );
	}

	[Fact]
	public void MismatchedBoneCountsAreRejectedBeforeWritingTheDestination()
	{
		var result = Pose( 7f );
		Assert.Throws<ArgumentException>( () => result.BlendMany(
			new[] { Pose( 1f ), new SkeletonPose( 2 ) }, new[] { .5f, .5f } ) );
		Assert.Equal( 7f, result.Positions[ 0 ].X );
	}

	[Fact]
	public void RepeatedPushBlendAndClearReuseStorage()
	{
		var stack = new AnimationPoseStack( 1, 3 );
		var pose = Pose( 7f );
		var weights = new[] { .25f, .25f, .5f };
		void Step()
		{
			stack.Clear();
			stack.Push( pose );
			stack.Push( pose );
			stack.Push( pose );
			stack.BlendMany( weights );
		}
		for ( var i = 0; i < 4096; i++ ) Step();
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 1024; i++ ) Step();
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.Equal( 0, allocated );
		Assert.Equal( 7f, stack.Pose.Positions[ 0 ].X );
	}

	private static SkeletonPose Pose( float position, float mask = 1f, Quaternion? rotation = null, float scale = 1f )
	{
		var pose = new SkeletonPose( 1 );
		pose.SetTransforms( new[] { new AnimationBoneTransform( new Vector4( position, position * 2f, position * 3f, mask ),
			rotation ?? Quaternion.Identity, new Vector4( scale, scale + 1f, scale + 2f, scale + 3f ) ) } );
		return pose;
	}
}
