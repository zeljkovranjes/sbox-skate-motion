using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationRuntimePosturePoseTransformTests
{
	[Theory]
	[InlineData( 1, 0u, -.125f, -.375f, .5f, .25f, 3.75f, -1.25f )]
	[InlineData( 1, 1u, -.375f, .125f, .25f, -.5f, 3.75f, -1.25f )]
	[InlineData( 2, 0u, .375f, -.125f, .25f, -.5f, -3.75f, 1.25f )]
	public void RuntimeAxesPreserveTheTransformAndWholeRecordSwap( int mode, uint rootKind,
		float qx, float qy, float qz, float qw, float px, float py )
	{
		var pose = new SkeletonPose( 3 );
		for ( var bone = 0; bone < 3; bone++ )
		{
			pose.Positions[ bone ] = new Vector3( 3.75f + bone, 1.25f + bone, -2.5f - bone );
			pose.Rotations[ bone ] = new Quaternion( .375f, .125f, -.25f, -.5f );
			pose.Scales[ bone ] = new Vector4( bone + 3, bone + 1, bone + 2, bone + 4 );
			pose.BoneWeights[ bone ] = .25f + bone * .125f;
		}
		var map = new AnimationPostureBoneMap( new[] { rootKind, 1u, 1u }, new[] { 0, 2, 1 } );

		AnimationRuntimePosturePoseTransform.Apply( pose, map, mode );

		Assert.Equal( new Quaternion( qx, qy, qz, qw ), pose.Rotations[ 0 ] );
		Assert.Equal( new Vector3( px, py, -2.5f ), pose.Positions[ 0 ] );
		Assert.Equal( new Vector3( -5.75f, 3.25f, -4.5f ), pose.Positions[ 1 ] );
		Assert.Equal( new Vector3( -4.75f, 2.25f, -3.5f ), pose.Positions[ 2 ] );
		Assert.Equal( new Vector4( 5, 3, 4, 6 ), pose.Scales[ 1 ] );
		Assert.Equal( new Vector4( 4, 2, 3, 5 ), pose.Scales[ 2 ] );
		Assert.Equal( .5f, pose.BoneWeights[ 1 ] );
		Assert.Equal( .375f, pose.BoneWeights[ 2 ] );
	}
}
