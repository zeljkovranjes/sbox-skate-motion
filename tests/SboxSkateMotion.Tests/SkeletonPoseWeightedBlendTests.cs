using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SkeletonPoseWeightedBlendTests
{
	[Fact]
	public void SampledOverlayMasksPreserveUnselectedBonesAndBlendSelectedBones()
	{
		var skeleton = new ClipSkeleton( [ "root", "child" ], [ -1, 0 ],
			[ Vector3.Zero, Vector3.Zero ], [ Quaternion.Identity, Quaternion.Identity ] );
		var clip = new AnimationClip( "overlay", 30f, 2,
			[ 0, 0 ], [ 0, 0 ], [], [ 0, 0 ], [ 0, 0 ], [] );
		clip.SetBoneWeights( [ 0f, .5f ] );
		var overlay = new SkeletonPose( 2 );
		overlay.Sample( clip, skeleton, 0f, false );
		overlay.Positions[ 0 ] = overlay.Positions[ 1 ] = new Vector3( 8f, 4f, 2f );
		var pose = new SkeletonPose( 2 );
		pose.SetRest( skeleton );

		pose.BlendWeightedToward( overlay, .5f, false );

		Assert.Equal( Vector3.Zero, pose.Positions[ 0 ] );
		Assert.Equal( new Vector3( 2f, 1f, .5f ), pose.Positions[ 1 ] );
		Assert.Equal( new[] { 1f, .875f }, pose.BoneWeights );
		Assert.Equal( new[] { 0f, .5f }, overlay.BoneWeights );
		Assert.Equal( new Vector3( 8f, 4f, 2f ), overlay.Positions[ 1 ] );
	}

	[Theory]
	[InlineData( true, 2f, .125f, 1f )]
	[InlineData( false, 8f, .5f, 1f )]
	public void MaskSelectorUsesTheChosenInputAndRetainsTheInterpolatedMask(
		bool maskFromFirst, float expectedPosition, float expectedRotationZ, float expectedRotationW )
	{
		var pose = Pose( 0f, .25f, Quaternion.Identity );
		var overlay = Pose( 8f, 1f, new Quaternion( 0f, 0f, .5f, 1f ) );

		pose.BlendWeightedToward( overlay, 1f, maskFromFirst );

		Assert.Equal( expectedPosition, pose.Positions[ 0 ].X );
		Assert.Equal( maskFromFirst ? .4375f : 1f, pose.BoneWeights[ 0 ] );
		// The selected weight affects rotation as well as translation.
		var expected = Quaternion.Normalize( new Quaternion( 0f, 0f, expectedRotationZ, expectedRotationW ) );
		Assert.True( Quaternion.Dot( pose.Rotations[ 0 ], expected ) > .999999f );
	}

	[Theory]
	[InlineData( -2f, 1f, 0f, 1f )]
	[InlineData( 2f, 1f, 8f, 1f )]
	[InlineData( 1f, -1f, 0f, 1f )]
	[InlineData( .75f, 2f, 8f, 2f )]
	public void MultipliedWeightClampsBeforeBlending(
		float weight, float mask, float expectedPosition, float expectedMask )
	{
		var pose = Pose( 0f, 1f, Quaternion.Identity );
		pose.BlendWeightedToward( Pose( 8f, mask, Quaternion.Identity ), weight, false );
		Assert.Equal( expectedPosition, pose.Positions[ 0 ].X );
		Assert.Equal( expectedMask, pose.BoneWeights[ 0 ] );
	}

	[Fact]
	public void OppositeQuaternionSignsDoNotCancelDuringMaskedBlend()
	{
		var pose = Pose( 0f, 1f, Quaternion.Identity );
		pose.BlendWeightedToward( Pose( 8f, .5f, new Quaternion( 0f, 0f, 0f, -1f ) ), 1f, false );
		Assert.Equal( Quaternion.Identity, pose.Rotations[ 0 ] );
	}

	[Fact]
	public void SelfBlendPreservesTransformAndMask()
	{
		var pose = Pose( 3f, .5f, Quaternion.Identity );
		pose.BlendWeightedToward( pose, .75f, false );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), pose.Positions[ 0 ] );
		Assert.Equal( Quaternion.Identity, pose.Rotations[ 0 ] );
		Assert.Equal( .5f, pose.BoneWeights[ 0 ] );
	}

	[Fact]
	public void DifferentBoneCountsFailBeforeAnyPoseMutation()
	{
		var pose = Pose( 3f, .5f, Quaternion.Identity );
		Assert.Throws<ArgumentException>( () => pose.BlendWeightedToward( new SkeletonPose( 2 ), 1f, false ) );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), pose.Positions[ 0 ] );
		Assert.Equal( Quaternion.Identity, pose.Rotations[ 0 ] );
		Assert.Equal( .5f, pose.BoneWeights[ 0 ] );
	}

	private static SkeletonPose Pose( float x, float mask, Quaternion rotation )
	{
		var pose = new SkeletonPose( 1 );
		pose.Positions[ 0 ] = new Vector3( x, 0f, 0f );
		pose.Rotations[ 0 ] = rotation;
		pose.BoneWeights[ 0 ] = mask;
		return pose;
	}
}
