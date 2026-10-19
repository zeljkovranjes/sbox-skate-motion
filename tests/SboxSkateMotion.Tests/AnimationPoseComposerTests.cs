using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationPoseComposerTests
{
	[Theory]
	[InlineData( false, false, 9f )]
	[InlineData( false, true, 7f )]
	[InlineData( true, false, 9f )]
	[InlineData( true, true, 7f )]
	public void TranslationMetadataSelectionDoesNotDependOnCompositionOrder( bool reverse, bool first, float expected )
	{
		var a = new AnimationBoneTransform( new Vector4( 1f, 2f, 3f, 7f ), Quaternion.Identity, new Vector4( 2f ) );
		var b = new AnimationBoneTransform( new Vector4( 4f, 5f, 6f, 9f ), Quaternion.Identity, new Vector4( 3f ) );
		var result = AnimationPoseComposer.Compose( a, b, reverse, first );
		Assert.Equal( new Vector4( 5f, 7f, 9f, expected ), result.Position );
		Assert.Equal( new Vector4( 6f ), result.Scale );
	}

	[Fact]
	public void RotationChangesTranslationWithoutMultiplyingItByScale()
	{
		var a = new AnimationBoneTransform( new Vector4( 10f, 20f, 30f, 1f ), new Quaternion( 0f, 0f, 1f, 0f ), new Vector4( 3f ) );
		var b = new AnimationBoneTransform( new Vector4( 2f, 4f, 6f, .5f ), Quaternion.Identity, new Vector4( 2f ) );
		Assert.Equal( new Vector4( 8f, 16f, 36f, 1f ), AnimationPoseComposer.Compose( a, b, false, true ).Position );
		Assert.Equal( new Vector4( 12f, 24f, 36f, 1f ), AnimationPoseComposer.Compose( a, b, true, true ).Position );
	}

	[Fact]
	public void QuaternionProductRetainsItsMagnitude()
	{
		var a = new AnimationBoneTransform( Vector4.Zero, new Quaternion( 0f, 0f, 0f, 2f ), Vector4.One );
		var b = a with { Rotation = new Quaternion( 0f, 0f, 0f, 3f ) };
		Assert.Equal( new Quaternion( 0f, 0f, 0f, 6f ), AnimationPoseComposer.Compose( a, b, false, true ).Rotation );
	}

	[Fact]
	public void SpanValidationPrecedesWritesAndInPlaceCompositionPreservesBothInputs()
	{
		var a = new AnimationBoneTransform( new Vector4( 1f, 2f, 3f, 7f ), Quaternion.Identity, Vector4.One );
		var b = new AnimationBoneTransform( new Vector4( 4f, 5f, 6f, 9f ), Quaternion.Identity, Vector4.One );
		var first = new[] { a };
		Assert.Throws<ArgumentException>( () => AnimationPoseComposer.Compose( first, Array.Empty<AnimationBoneTransform>(), false, true, first ) );
		Assert.Equal( a, first[ 0 ] );
		AnimationPoseComposer.Compose( first, new[] { b }, true, false, first );
		Assert.Equal( new Vector4( 5f, 7f, 9f, 9f ), first[ 0 ].Position );
		AnimationPoseComposer.Compose( Array.Empty<AnimationBoneTransform>(), Array.Empty<AnimationBoneTransform>(), false, true, Array.Empty<AnimationBoneTransform>() );
	}
}
