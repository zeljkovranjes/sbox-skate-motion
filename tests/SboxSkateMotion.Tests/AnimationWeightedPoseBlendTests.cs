using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationWeightedPoseBlendTests
{
	[Fact]
	public void MaskSourceControlsAllTransformChannelsAndStoredMask()
	{
		var first = Transform( 0f, .25f );
		var second = Transform( 10f, .75f );
		var fromFirst = AnimationPoseBlender.BlendWeighted( first, second, .8f, true );
		var fromSecond = AnimationPoseBlender.BlendWeighted( first, second, .8f, false );
		Assert.Equal( 2f, fromFirst.Position.X, 6 );
		Assert.Equal( 6f, fromSecond.Position.X, 6 );
		Assert.Equal( 2f, fromFirst.Position.W, 6 );
		Assert.Equal( 6f, fromSecond.Scale.Z, 6 );
		Assert.Equal( .35f, fromFirst.Scale.W, 6 );
		Assert.Equal( .55f, fromSecond.Scale.W, 6 );
	}

	[Fact]
	public void ClampingHappensAfterMultiplyingChannelAndBoneWeights()
	{
		var first = Transform( 0f, 2f );
		var second = Transform( 10f, -.25f );
		var middle = AnimationPoseBlender.BlendWeighted( first, second, .25f, true );
		var upper = AnimationPoseBlender.BlendWeighted( first, second, .75f, true );
		var lower = AnimationPoseBlender.BlendWeighted( first, second, .75f, false );
		Assert.Equal( 5f, middle.Position.X );
		Assert.Equal( second.Position, upper.Position );
		Assert.Equal( first.Position, lower.Position );
		Assert.Equal( second.Scale, upper.Scale );
		Assert.Equal( first.Scale, lower.Scale );
	}

	[Fact]
	public void ZeroWeightStillNormalizesRotation()
	{
		var first = Transform( 0f, 0f ) with { Rotation = new Quaternion( 0f, 0f, 0f, 2f ) };
		var second = Transform( 10f, 1f );
		var result = AnimationPoseBlender.BlendWeighted( first, second, .75f, true );
		Assert.Equal( Quaternion.Identity, result.Rotation );
		Assert.Equal( first.Position, result.Position );
	}

	[Fact]
	public void OrthogonalRotationUsesWeightedNegativeHemisphere()
	{
		var first = Transform( 0f, .5f );
		var second = Transform( 0f, 1f ) with { Rotation = new Quaternion( 1f, 0f, 0f, 0f ) };
		var result = AnimationPoseBlender.BlendWeighted( first, second, .5f, true );
		Assert.InRange( result.Rotation.X, -.316228f, -.316227f );
		Assert.InRange( result.Rotation.W, .948683f, .948684f );
	}

	[Fact]
	public void BufferBlendSupportsBothInputAliasesAndDistinctBoneMasks()
	{
		var first = new[] { Transform( 0f, 0f ), Transform( 2f, 1f ) };
		var second = new[] { Transform( 10f, 1f ), Transform( 12f, 0f ) };
		var firstCopy = (AnimationBoneTransform[])first.Clone();
		var secondCopy = (AnimationBoneTransform[])second.Clone();
		AnimationPoseBlender.BlendWeighted( first, second, .5f, true, first );
		AnimationPoseBlender.BlendWeighted( firstCopy, secondCopy, .5f, true, secondCopy );
		Assert.Equal( 0f, first[ 0 ].Position.X );
		Assert.Equal( 7f, first[ 1 ].Position.X );
		Assert.Equal( first, secondCopy );
		Assert.Equal( second[ 0 ].Position.X, 10f );
	}

	[Fact]
	public void BufferMismatchIsRejectedBeforeWritingAndEmptyBuffersAreAllowed()
	{
		var first = new[] { Transform( 1f, 1f ) };
		var output = new[] { Transform( 9f, 1f ) };
		Assert.Throws<ArgumentException>( () => AnimationPoseBlender.BlendWeighted(
			first, Array.Empty<AnimationBoneTransform>(), .5f, false, output ) );
		Assert.Equal( 9f, output[ 0 ].Position.X );
		AnimationPoseBlender.BlendWeighted( Array.Empty<AnimationBoneTransform>(),
			Array.Empty<AnimationBoneTransform>(), .5f, false, Array.Empty<AnimationBoneTransform>() );
	}

	private static AnimationBoneTransform Transform( float value, float mask ) => new(
		new Vector4( value ), Quaternion.Identity, new Vector4( value, value, value, mask ) );
}
