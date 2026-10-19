using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPosturePoseTransformTests
{
	[Theory]
	[InlineData( 1, 0u, -.375f, .5f, -.125f, .25f, -1.25f, 3.75f )]
	[InlineData( 1, 1u, .125f, .25f, -.375f, -.5f, -1.25f, 3.75f )]
	[InlineData( 2, 0u, -.125f, .25f, .375f, -.5f, 1.25f, -3.75f )]
	public void PostureUsesTheRootKindAndSwapsWholePairedRecords( int mode, uint rootKind,
		float qx, float qy, float qz, float qw, float px, float pz )
	{
		var pose = new SkeletonPose( 3 );
		for ( var bone = 0; bone < 3; bone++ )
		{
			pose.Positions[ bone ] = new Vector3( 1.25f + bone, -2.5f - bone, 3.75f + bone );
			pose.Rotations[ bone ] = new Quaternion( .125f, -.25f, .375f, -.5f );
			pose.Scales[ bone ] = new Vector4( bone + 1, bone + 2, bone + 3, bone + 4 );
			pose.BoneWeights[ bone ] = .25f + bone * .125f;
		}
		var kinds = new[] { rootKind, 1u, 1u };
		var partners = new[] { 0, 2, 1 };
		var map = new AnimationPostureBoneMap( kinds, partners );
		kinds[ 0 ] = 99;
		partners[ 1 ] = 1;

		AnimationPosturePoseTransform.Apply( pose, map, mode );

		Assert.Equal( 3, map.BoneCount );
		Assert.Equal( new Quaternion( qx, qy, qz, qw ), pose.Rotations[ 0 ] );
		Assert.Equal( new Vector3( px, -2.5f, pz ), pose.Positions[ 0 ] );
		Assert.Equal( new Vector3( 3.25f, -4.5f, -5.75f ), pose.Positions[ 1 ] );
		Assert.Equal( new Vector3( 2.25f, -3.5f, -4.75f ), pose.Positions[ 2 ] );
		Assert.Equal( new Vector4( 3, 4, 5, 6 ), pose.Scales[ 1 ] );
		Assert.Equal( new Vector4( 2, 3, 4, 5 ), pose.Scales[ 2 ] );
		Assert.Equal( .5f, pose.BoneWeights[ 1 ] );
		Assert.Equal( .375f, pose.BoneWeights[ 2 ] );
	}
}
