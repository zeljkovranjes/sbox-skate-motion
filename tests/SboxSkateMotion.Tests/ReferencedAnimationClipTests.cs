using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ReferencedAnimationClipTests
{
	[Fact]
	public void ReferenceRotatesAndTranslatesKeysWithoutScalingTranslationOrNormalizingRotation()
	{
		var sampler = Build();
		var value = sampler.SampleBone( 0f, 0, false );
		Assert.Equal( new Vector4( 3f, 9f, 11f, .25f ), value.Position );
		Assert.Equal( new Quaternion( 0f, 0f, 2f, 0f ), value.Rotation );
		Assert.Equal( new Vector4( 6f, 12f, 20f, 30f ), value.Scale );
	}

	[Fact]
	public void RepeatedBufferSamplesComposeOnceAndLeaveExtraCapacityUntouched()
	{
		var sampler = Build();
		var output = new AnimationBoneTransform[ 2 ];
		var sentinel = new AnimationBoneTransform( new Vector4( 90f ), Quaternion.Identity, new Vector4( 80f ) );
		output[ 1 ] = sentinel;
		sampler.Sample( 0f, false, output );
		sampler.Sample( 0f, false, output );
		Assert.Equal( new Vector4( 3f, 9f, 11f, .25f ), output[ 0 ].Position );
		Assert.Equal( sentinel.Position, output[ 1 ].Position );
		Assert.Equal( sentinel.Scale, output[ 1 ].Scale );
	}

	[Fact]
	public void ReferenceMustMatchClipBoneCount()
	{
		Assert.Throws<ArgumentException>( () => new ReferencedAnimationClip( Build().Clip, Array.Empty<AnimationBoneTransform>() ) );
	}

	[Fact]
	public void ReusedComposedOutputDoesNotAllocate()
	{
		var sampler = Build();
		var output = new AnimationBoneTransform[ 1 ];
		for ( var i = 0; i < 1000; i++ )
			sampler.Sample( 0f, false, output );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 1000; i++ )
			sampler.Sample( 0f, false, output );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}

	private static ReferencedAnimationClip Build()
	{
		var clip = new RawAnimationClip( "sample", 30f, 1f, 1, 0, 0,
			new[] { 0, 1, 2 }, new[] { 1, 1, 1 },
			new[] { new Vector4( 2f, 4f, 5f, .25f ), new Vector4( 0f, 0f, 0f, 2f ), new Vector4( 2f, 3f, 4f, 5f ) } );
		var reference = new AnimationBoneTransform( new Vector4( 5f, 13f, 6f, .75f ),
			new Quaternion( 0f, 0f, 1f, 0f ), new Vector4( 3f, 4f, 5f, 6f ) );
		return new ReferencedAnimationClip( clip, new[] { reference } );
	}
}
