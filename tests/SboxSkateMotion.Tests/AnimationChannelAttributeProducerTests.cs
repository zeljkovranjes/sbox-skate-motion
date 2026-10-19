using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationChannelAttributeProducerTests
{
	[Fact]
	public void EnabledOverlayWinsEqualFullKeysAndPreservesUnmatchedDuplicates()
	{
		var key = new AnimationAttributeKey( 1, 0, 0, 0, 1 );
		var first = Leaf( Attribute( new( 1, 0, 0, 0, 0 ), 1f ), Attribute( key, 2f ),
			Attribute( key, 3f ), Attribute( new( 3, 0, 0, 0, 0 ), 4f ) );
		var second = Leaf( Attribute( key, 20f ), Attribute( new( 2, 0, 0, 0, 0 ), 30f ) );
		first.Seek( .75f );
		second.Seek( .5f );
		var node = new AnimationChannelPosePlayback( first, second,
			new AnimationChannelBlend( new( 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0x02000000 ) ), true );
		var cache = new AnimationEvaluatedAttributeCache( 8 );
		var publisher = new AnimationEvaluatedAttributePublisher( cache, 2, 8 );
		publisher.Publish( node );
		Assert.Equal( new[] { 1f, 20f, 3f, 30f, 4f },
			cache.Values.ToArray().Select( record => record.Payload.Scalar ) );
		Assert.Equal( .75f, first.Clock.PreviousPosition );
		Assert.Equal( .5f, second.Clock.PreviousPosition );
		publisher.Publish( node );
		Assert.Equal( 5, cache.Values.Length );
	}

	[Fact]
	public void DisabledFlagAndEmptyOverlayUseBaseWithTheirDistinctScratchRequirements()
	{
		var first = Leaf( Attribute( new( 1, 0, 0, 0, 0 ), 2f ) );
		var output = new AnimationEvaluatedAttributeBuffer( 4 );
		var disabled = new AnimationChannelPosePlayback( first, Leaf(), new AnimationChannelBlend( default ), true );
		disabled.SampleEvaluatedAttributes( new AnimationEvaluatedAttributeContext( 0, 4 ), output, 15 );
		Assert.Equal( 2f, Assert.Single( output.Values.ToArray() ).Payload.Scalar );
		var enabled = new AnimationChannelPosePlayback( first, Leaf(),
			new AnimationChannelBlend( new( 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0x02000000 ) ), true );
		var context = new AnimationEvaluatedAttributeContext( 1, 4 );
		enabled.SampleEvaluatedAttributes( context, output, 0 );
		enabled.SampleEvaluatedAttributes( context, output, 15 );
		Assert.Equal( 3, output.Values.Length );
		Assert.All( output.Values.ToArray(), record => Assert.Equal( 2f, record.Payload.Scalar ) );
	}

	private static AnimationClipAttribute Attribute( AnimationAttributeKey key, float value ) =>
		new( key, -1f, -1f, 0, new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ) );

	private static AnimationClipPlayback Leaf( params AnimationClipAttribute[] records )
	{
		var metadata = new AnimationClipAttributeSet( "sample", new uint[ 6 ], records );
		var clip = new RawAnimationClip( "sample", 30f, 1f, 61, 0, 0, new int[ 3 ], new int[ 3 ],
			Array.Empty<Vector4>(), null, null, (AnimationClipSamplingMode)1, metadata );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}
}
