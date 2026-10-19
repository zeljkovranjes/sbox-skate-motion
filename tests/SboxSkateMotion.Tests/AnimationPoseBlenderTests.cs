using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationPoseBlenderTests
{
	[Fact]
	public void BlendPreservesAllTranslationAndScaleLanes()
	{
		var first = new AnimationBoneTransform( new( 0, 4, 8, 12 ), Quaternion.Identity, new( 1, 2, 3, 4 ) );
		var second = new AnimationBoneTransform( new( 4, 8, 12, 16 ), Quaternion.Identity, new( 5, 6, 7, 8 ) );
		var result = AnimationPoseBlender.Blend( first, second, .25f );
		Assert.Equal( new Vector4( 1, 5, 9, 13 ), result.Position );
		Assert.Equal( new Vector4( 2, 3, 4, 5 ), result.Scale );
		Assert.Equal( Quaternion.Identity, result.Rotation );
	}

	[Fact]
	public void OppositeQuaternionSignsDoNotCancelAtMidpoint()
	{
		var result = AnimationPoseBlender.BlendRotation( Quaternion.Identity, new Quaternion( 0, 0, 0, -1 ), .5f );
		Assert.Equal( Quaternion.Identity, result );
	}

	[Fact]
	public void ZeroDotUsesOppositeSignBranch()
	{
		var result = AnimationPoseBlender.BlendRotation( new Quaternion( 1, 0, 0, 0 ), new Quaternion( 0, 1, 0, 0 ), .5f );
		Assert.True( result.X > .7f );
		Assert.True( result.Y < -.7f );
		Assert.Equal( 1f, result.Length(), 6 );
	}

	[Fact]
	public void RotationUsesNormalizedLinearPhaseRatherThanConstantAngularSpeed()
	{
		var second = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 2f );
		var result = AnimationPoseBlender.BlendRotation( Quaternion.Identity, second, .25f );
		var expected = Quaternion.Normalize( new Quaternion( 0, 0, second.Z * .25f, .75f + second.W * .25f ) );
		Assert.Equal( expected.Z, result.Z, 6 );
		Assert.Equal( expected.W, result.W, 6 );
		Assert.True( MathF.Abs( result.Z - Quaternion.Slerp( Quaternion.Identity, second, .25f ).Z ) > .001f );
	}

	[Fact]
	public void EndpointStillNormalizesInputRotation()
	{
		var result = AnimationPoseBlender.BlendRotation( new Quaternion( 0, 0, 0, 2 ), Quaternion.Identity, 0f );
		Assert.Equal( Quaternion.Identity, result );
	}

	[Fact]
	public void PoseBuffersCanBlendIntoEitherInputWithoutCorruptingLaterBones()
	{
		var a = new[]
		{
			new AnimationBoneTransform( new( 0, 0, 0, 1 ), Quaternion.Identity, Vector4.One ),
			new AnimationBoneTransform( new( 10, 10, 10, 1 ), Quaternion.Identity, Vector4.One )
		};
		var b = new[]
		{
			new AnimationBoneTransform( new( 4, 4, 4, 1 ), Quaternion.Identity, Vector4.One ),
			new AnimationBoneTransform( new( 14, 14, 14, 1 ), Quaternion.Identity, Vector4.One )
		};
		var expected = new AnimationBoneTransform[ 2 ];
		AnimationPoseBlender.Blend( a, b, .5f, expected );
		var inPlaceSecond = (AnimationBoneTransform[])b.Clone();
		AnimationPoseBlender.Blend( a, inPlaceSecond, .5f, inPlaceSecond );
		Assert.Equal( expected, inPlaceSecond );
		AnimationPoseBlender.Blend( a, b, .5f, a );
		Assert.Equal( expected, a );
		Assert.Equal( new Vector4( 2, 2, 2, 1 ), a[ 0 ].Position );
		Assert.Equal( new Vector4( 12, 12, 12, 1 ), a[ 1 ].Position );
	}

	[Fact]
	public void EmptyPoseLeavesOutputUntouchedAndMismatchedInputsFail()
	{
		var output = new[] { new AnimationBoneTransform( Vector4.One, Quaternion.Identity, Vector4.One ) };
		AnimationPoseBlender.Blend( Array.Empty<AnimationBoneTransform>(), Array.Empty<AnimationBoneTransform>(), .5f, output );
		Assert.Equal( Vector4.One, output[ 0 ].Position );
		Assert.Throws<ArgumentException>( () => AnimationPoseBlender.Blend( output, Array.Empty<AnimationBoneTransform>(), .5f, output ) );
	}
}
