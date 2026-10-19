using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PoseBlendReductionTests
{
	[Fact]
	public void ConvertedLaneCancellationRetainsThePositiveSignBranch()
	{
		var result = AnimationPoseBlender.BlendRotation( new Quaternion( 1f, 1f, 1f, 1f ),
			new Quaternion( 33554432f, -33554432f, 1f, 1f ), 1f );
		Assert.True( result.X > 0f );
		Assert.True( result.Y < 0f );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void NormalizationPreservesConvertedLaneRounding( bool multiple )
	{
		var input = new AnimationBoneTransform( Vector4.One, new Quaternion( .1f, .2f, .3f, .4f ), Vector4.One );
		var result = multiple
			? AnimationPoseMultiBlender.Blend( new[] { input }, new[] { 1f } ).Rotation
			: AnimationPoseBlender.BlendRotation( input.Rotation, input.Rotation, 0f );
		Assert.Equal( 0x3e3af4ba, BitConverter.SingleToInt32Bits( result.X ) );
		Assert.Equal( 0x3ebaf4ba, BitConverter.SingleToInt32Bits( result.Y ) );
		Assert.Equal( 0x3f0c378c, BitConverter.SingleToInt32Bits( result.Z ) );
		Assert.Equal( 0x3f3af4ba, BitConverter.SingleToInt32Bits( result.W ) );
	}
}
