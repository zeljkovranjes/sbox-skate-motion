using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationTreeAttributeQueryTests
{
	private static readonly AnimationAttributeKey Key = new( 1, 2, 3, 4, 5 );

	[Fact]
	public void ChannelQueriesOverlayEvenWhenEnvelopeWeightIsZero()
	{
		var node = Channel( Leaf( 2f ), Leaf( 9f ) );
		Assert.Equal( 9f, Scalar( node ) );
		Assert.Equal( 2f, Scalar( Channel( Leaf( 2f ), Empty() ) ) );
	}

	[Fact]
	public void SequenceQueryFollowsHandoffAndReplacement()
	{
		var node = new AnimationSequencePosePlayback( Leaf( 2f ), Leaf( 9f ) );
		Assert.Equal( 2f, Scalar( node ) );
		var completion = AnimationPlaybackCompletion.Empty;
		node.Advance( 3f, 0f, ref completion );
		Assert.True( node.Playback.ActiveNext );
		Assert.Equal( 9f, Scalar( node ) );
		node.Reset( Leaf( 4f ), Leaf( 8f ) );
		Assert.Equal( 4f, Scalar( node ) );
	}

	[Fact]
	public void TransitionKeepsOutgoingQueriesAfterClockAdvancementAndReplacement()
	{
		var node = new AnimationTransitionPosePlayback( Leaf( 2f ), Leaf( 9f ), .25f, AnimationTransitionMode.Both, 0 );
		var completion = AnimationPlaybackCompletion.Empty;
		node.Advance( 1f, 0f, ref completion );
		Assert.Equal( 2f, Scalar( node ) );
		node.Reset( Leaf( 4f ), Leaf( 8f ), .25f, AnimationTransitionMode.Both );
		Assert.Equal( 4f, Scalar( node ) );
	}

	[Theory]
	[InlineData( true, 2f )]
	[InlineData( false, 9f )]
	public void CompositionQueriesOnlyItsActiveChild( bool firstActive, float expected )
	{
		Assert.Equal( expected, Scalar( new AnimationCompositionPosePlayback( Leaf( 2f ), Leaf( 9f ), firstActive ) ) );
	}

	[Fact]
	public void ReferenceMissPreservesEveryResultField()
	{
		var node = new AnimationReferencePosePlayback( Array.Empty<AnimationBoneTransform>() );
		var result = new AnimationAttributeQueryResult( new( 1, 2, 3, 4, 5, 6 ), Key, 7f, 8f, 9, 10 );
		var before = result;
		Assert.False( node.TryGetAuthoredAttribute( Key, 31, ref result ) );
		Assert.Equal( before, result );
	}

	[Fact]
	public void DirectionalThresholdIsStrictAndFailedPairRetainsFirstLookup()
	{
		var node = Directional( Leaf( 2f ), Empty(), .25f );
		node.Playback.UpdateParameter( .125f, true );
		Assert.Equal( 2f, Scalar( node ) );
		node.Playback.UpdateParameter( .25f, true );
		var result = default( AnimationAttributeQueryResult );
		Assert.False( node.TryGetAuthoredAttribute( Key, 31, ref result ) );
		Assert.Equal( 2f, result.Payload.Scalar );
	}

	[Fact]
	public void ChannelFallbackPreservesPartialOverlayOutputWhenBaseAlsoMisses()
	{
		var overlay = Directional( Leaf( 7f ), Empty(), 0f );
		overlay.Playback.UpdateParameter( .5f, true );
		var result = default( AnimationAttributeQueryResult );
		Assert.False( Channel( Empty(), overlay ).TryGetAuthoredAttribute( Key, 31, ref result ) );
		Assert.Equal( 7f, result.Payload.Scalar );
	}

	[Fact]
	public void DirectionalSecondQueryUsesBroaderFilterAndBlendsIntervals()
	{
		var first = Leaf( 2f, start: .1f, end: .9f );
		var second = Leaf( 10f, start: .8f, end: 1f );
		var node = Directional( first, second, 0f );
		node.Playback.UpdateParameter( .25f, true );
		first.Seek( .75f );
		second.Seek( .75f );
		var result = default( AnimationAttributeQueryResult );
		Assert.True( node.TryGetAuthoredAttribute( Key, 5, ref result ) );
		Assert.Equal( 4f, result.Payload.Scalar );
		Assert.Equal( .75f * .2f + .25f * 1.6f, result.Start );
		Assert.Equal( .75f * 1.8f + .25f * 2f, result.End );
		Assert.Equal( 5, result.Flags );
		Assert.Equal( .75f, first.Clock.Position );
		Assert.Equal( .75f, second.Clock.PreviousPosition );
	}

	[Theory]
	[InlineData( 0, 0x80000000u )]
	[InlineData( 1, 0u )]
	public void VectorAndScalarZeroUseTheirDistinctArithmetic( int kind, uint expected )
	{
		var node = Directional( Leaf( -0f, kind ), Leaf( -0f, kind ), 0f );
		node.Playback.UpdateParameter( .5f, true );
		var result = default( AnimationAttributeQueryResult );
		Assert.True( node.TryGetAuthoredAttribute( Key, 31, ref result ) );
		Assert.Equal( expected, result.Payload.A );
	}

	[Fact]
	public void SixWordValuesBlendOnlyTheFinalLane()
	{
		var node = Directional( Leaf( 2f, 3 ), Leaf( 10f, 3 ), 0f );
		node.Playback.UpdateParameter( .25f, true );
		var result = default( AnimationAttributeQueryResult );
		Assert.True( node.TryGetAuthoredAttribute( Key, 31, ref result ) );
		Assert.Equal( Bits( 2f ), result.Payload.A );
		Assert.Equal( Bits( 4f ), result.Payload.F );
		Assert.Equal( -1f, result.Start );
		Assert.Equal( -1f, result.End );
	}

	private static uint Bits( float value ) => BitConverter.SingleToUInt32Bits( value );
	private static float Scalar( IAnimationAttributePlaybackNode node )
	{
		var result = default( AnimationAttributeQueryResult );
		Assert.True( node.TryGetAuthoredAttribute( Key, 31, ref result ) );
		return result.Payload.Scalar;
	}

	private static AnimationChannelPosePlayback Channel( IAnimationPosePlaybackNode first, IAnimationPosePlaybackNode second ) =>
		new( first, second, new AnimationChannelBlend( default ) );

	private static AnimationDirectionalPosePlayback Directional( IAnimationPosePlaybackNode first, IAnimationPosePlaybackNode second, float threshold ) =>
		new( new[] { first, second }, new[] { 0f, 1f } ) { PoseThreshold = threshold };

	private static AnimationClipPlayback Empty() => Create( Array.Empty<AnimationClipAttribute>() );

	private static AnimationClipPlayback Leaf( float value, int kind = 0, float start = -1f, float end = -1f )
	{
		var bits = Bits( value );
		return Create( new[] { new AnimationClipAttribute( Key, start, end, (sbyte)kind,
			new AnimationAttributePayload( bits, bits, bits, bits, bits, bits ) ) } );
	}

	private static AnimationClipPlayback Create( AnimationClipAttribute[] attributes )
	{
		var metadata = new AnimationClipAttributeSet( "sample", new uint[ 6 ], attributes );
		var clip = new RawAnimationClip( "sample", 30f, 1f, 61, 0, 0, new int[ 3 ], new int[ 3 ],
			Array.Empty<Vector4>(), null, null, (AnimationClipSamplingMode)1, metadata );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}
}
