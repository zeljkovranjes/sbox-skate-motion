using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class SkeletonPoseBlendTests
{
	[Fact]
	public void OrthogonalRotationsChooseNegativeHemisphereAndLinearAngularProgress()
	{
		var first = Pose( Quaternion.Identity );
		var second = Pose( new Quaternion( 1f, 0f, 0f, 0f ) );
		first.BlendToward( second, .25f );
		var rotation = first.Rotations[ 0 ];
		Assert.InRange( rotation.X, -.316228f, -.316227f );
		Assert.InRange( rotation.W, .948683f, .948684f );
		Assert.Equal( 1f, rotation.Length(), 5 );
		Assert.Equal( new Quaternion( 1f, 0f, 0f, 0f ), second.Rotations[ 0 ] );
	}

	[Fact]
	public void EndpointsStillNormalizeAndPreserveHemisphereChoice()
	{
		var first = Pose( new Quaternion( 0f, 0f, 0f, 2f ) );
		var second = Pose( new Quaternion( 0f, 0f, 0f, -3f ) );
		first.BlendToward( second, 0f );
		Assert.Equal( Quaternion.Identity, first.Rotations[ 0 ] );
		first.BlendToward( second, 1f );
		Assert.Equal( Quaternion.Identity, first.Rotations[ 0 ] );
	}

	[Fact]
	public void ExtrapolationIsNotClampedByPoseStorage()
	{
		var first = Pose( Quaternion.Identity );
		var second = Pose( Quaternion.Identity );
		first.Positions[ 0 ] = new Vector3( 1f, 2f, 3f );
		second.Positions[ 0 ] = new Vector3( 5f, 6f, 7f );
		first.BlendToward( second, 1.5f );
		Assert.Equal( new Vector3( 7f, 8f, 9f ), first.Positions[ 0 ] );
		Assert.Equal( Quaternion.Identity, first.Rotations[ 0 ] );
	}

	[Fact]
	public void InPlacePoseBlendingPreservesNonzeroTransforms()
	{
		var pose = Pose( new Quaternion( 0f, 0f, 0f, 2f ) );
		pose.Positions[ 0 ] = new Vector3( 1f, 2f, 3f );
		pose.BlendToward( pose, .25f );
		Assert.Equal( new Vector3( 1f, 2f, 3f ), pose.Positions[ 0 ] );
		Assert.Equal( Quaternion.Identity, pose.Rotations[ 0 ] );
	}

	private static SkeletonPose Pose( Quaternion rotation )
	{
		var result = new SkeletonPose( 1 );
		result.Rotations[ 0 ] = rotation;
		return result;
	}
}
