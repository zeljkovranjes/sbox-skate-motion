using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationEvaluatedAttributeProducerTests
{
	private static readonly AnimationAttributeKey Key = new( 1, 0, 0, 0, 0 );

	[Fact]
	public void UnconditionalRecordsBypassFilterAndDuplicatesKeepTheirDistinctMetadata()
	{
		var clip = Leaf( Attribute( Key, 2f ), Attribute( Key, 4f, .1f, .6f ) );
		clip.Seek( .75f );
		var context = new AnimationEvaluatedAttributeContext( 0, 4 );
		var output = new AnimationEvaluatedAttributeBuffer( 4 );
		clip.SampleEvaluatedAttributes( context, output, 0 );
		Assert.Single( output.Values.ToArray() );
		Assert.Equal( 4, output.Values[ 0 ].Flags );
		Assert.Equal( -2f, output.Values[ 0 ].Start );
		Assert.Equal( -2f, output.Values[ 0 ].End );
		output.Clear();
		clip.SampleEvaluatedAttributes( context, output, 15 );
		Assert.Equal( 2, output.Values.Length );
		Assert.Equal( 2f, output.Values[ 0 ].Payload.Scalar );
		Assert.Equal( 4f, output.Values[ 1 ].Payload.Scalar );
		Assert.Equal( 5, output.Values[ 1 ].Flags );
		Assert.Equal( .75f, clip.Clock.Position );
		Assert.Equal( .75f, clip.Clock.PreviousPosition );
	}

	[Fact]
	public void RetainedOwnerPublishesOrderedIntersectionForEntryActionsWithoutConsumingClocks()
	{
		var first = Leaf( Attribute( Key, 2f ), Attribute( Key, 4f ),
			Attribute( new( 2, 0, 0, 0, 0 ), 99f ) );
		var second = Leaf( Attribute( Key, 20f ) );
		var pair = new AnimationDirectionalPosePlayback( [first, second], [0f, 1f] ) { PoseThreshold = .01f };
		pair.Playback.UpdateParameter( .25f, false );
		var owner = new AnimationBasePoseTreeOwner( new Factory( pair ) );
		Assert.True( owner.Replace( new( 0, AnimationBaseTransition.Play ) ) );
		first.Seek( .75f );
		second.Seek( .75f );
		var cache = new AnimationEvaluatedAttributeCache( 4 );
		var publisher = new AnimationEvaluatedAttributePublisher( cache, 2, 4 );
		publisher.Publish( owner );
		Assert.Equal( 2, cache.Values.Length );
		Assert.Equal( 6.5f, cache.Values[ 0 ].Payload.Scalar );
		Assert.Equal( 8f, cache.Values[ 1 ].Payload.Scalar );
		var overrides = new AnimationAttributeOverrideBuffer( 4, [] );
		var actionContext = new AnimationEntryActionOwnerContext( cache, overrides,
			_ => throw new InvalidOperationException( "This action uses the evaluated cache." ), default );
		AnimationEntryActionProcessor.Execute( [new( 0, default, Key, default, 0f, 0 )], true, actionContext );
		Assert.Equal( 1, overrides.Count );
		Assert.Equal( 6.5f, overrides.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.Equal( .75f, first.Clock.PreviousPosition );
		Assert.Equal( .75f, second.Clock.PreviousPosition );
		publisher.Publish( null );
		Assert.Empty( cache.Values.ToArray() );
		Assert.Throws<NotSupportedException>( () => publisher.Publish(
			new AnimationChannelPosePlayback( first, second, new AnimationChannelBlend( default ) ) ) );
	}

	private static AnimationClipAttribute Attribute( AnimationAttributeKey key, float value,
		float start = -1f, float end = -1f ) =>
		new( key, start, end, 0, new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ) );

	private static AnimationClipPlayback Leaf( params AnimationClipAttribute[] records )
	{
		var metadata = new AnimationClipAttributeSet( "sample", new uint[ 6 ], records );
		var clip = new RawAnimationClip( "sample", 30f, 1f, 61, 0, 0, new int[ 3 ], new int[ 3 ],
			Array.Empty<Vector4>(), null, null, (AnimationClipSamplingMode)1, metadata );
		return new AnimationClipPlayback( clip, 1, new AnimationPoseSamplingContext( 0, false ) );
	}

	private sealed class Factory( IAnimationPosePlaybackNode node ) : IAnimationBasePoseTreeFactory
	{
		public IAnimationPlaybackTree Create( int storageSlot, int definition ) => new Tree( node );
		public void Publish( IAnimationPosePlaybackNode root ) { }
	}

	private sealed class Tree( IAnimationPosePlaybackNode node ) : IAnimationPlaybackTree
	{
		public IAnimationPlaybackNode Root => node;
	}
}
