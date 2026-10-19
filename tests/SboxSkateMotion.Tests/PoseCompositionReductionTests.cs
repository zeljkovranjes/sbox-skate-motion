using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PoseCompositionReductionTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void ScalarRotationPreservesStoredAxisReductionOrder( bool reverse )
	{
		var first = new AnimationBoneTransform( Vector4.Zero,
			new Quaternion( 1f, 1f, 1f, 1f ), Vector4.One );
		var second = new AnimationBoneTransform( Vector4.Zero,
			new Quaternion( 33554432f, -33554432f, 1f, 2f ), Vector4.One );

		var result = AnimationPoseComposer.Compose( first, second, reverse, true );

		Assert.Equal( 2f, result.Rotation.W );
	}
}
