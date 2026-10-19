using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPostureUnpairedBoneTests
{
	[Fact]
	public void UnpairedSentinelPreservesTheWholeRecordInBothModes()
	{
		var map = new AnimationPostureBoneMap( new[] { 0u }, new[] { -1 } );
		var pose = new SkeletonPose( 1 );
		pose.Positions[ 0 ] = new Vector3( 2, 3, 4 );
		pose.Rotations[ 0 ] = new Quaternion( .125f, -.25f, .375f, -.5f );
		pose.Scales[ 0 ] = new Vector4( 5, 6, 7, 8 );
		pose.BoneWeights[ 0 ] = .25f;
		var original = pose.Transform( 0 );

		AnimationRuntimePosturePoseTransform.Apply( pose, map, 1 );
		Assert.Equal( original, pose.Transform( 0 ) );
		AnimationRuntimePosturePoseTransform.Apply( pose, map, 2 );
		Assert.Equal( original, pose.Transform( 0 ) );
		Assert.Equal( -1, map.PairedBone( 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new AnimationPostureBoneMap( new[] { 0u }, new[] { -2 } ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new AnimationPostureBoneMap( new[] { 0u }, new[] { 1 } ) );
	}
}
