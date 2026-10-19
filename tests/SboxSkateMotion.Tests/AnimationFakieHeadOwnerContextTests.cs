using System.Collections.Generic;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationFakieHeadOwnerContextTests
{
	[Fact]
	public void RetainedBehaviorCreatesFadesAndRecoversTheActualOwnerChannel()
	{
		var factory = new Factory();
		var channels = new AnimationOverlayOwner( 1, factory );
		channels.PublishBase( new Node() );
		var channel = new InputCommandKey( 11, 22, 33, 44, 55, 66 );
		var motion = new InputCommandKey( 77, 88, 99, 111, 222, 333 );
		var parameters = new Parameters();
		var active = true;
		var secondaryCalls = 0;
		var context = new AnimationFakieHeadOwnerContext( channels,
			key => { Assert.Equal( motion, key ); return 7; }, parameters,
			() => true, () => { secondaryCalls++; return false; }, () => active );
		var behavior = new AnimationFakieHeadInstance();
		var definition = new AnimationFakieHeadDefinition( channel, motion );
		behavior.Update( definition, context );
		Assert.Single( factory.Requests );
		Assert.Equal( new AnimationChannelReplacement( 7, 1f, false, true, .1f, .3f, .3f, 0x10000000, true ),
			factory.Requests[ 0 ] );
		Assert.Equal( 1, channels.Count );
		Assert.Equal( 0, secondaryCalls );
		Assert.Equal( new[] { 1f }, parameters.Values );
		var node = channels.Root;
		active = false;
		behavior.Update( definition, context );
		var completion = AnimationPlaybackCompletion.Empty;
		channels.Root.Advance( .1f, 0f, ref completion );
		channels.RemoveFinished();
		Assert.Equal( 1, channels.Count );
		active = true;
		behavior.Update( definition, context );
		Assert.Same( node, channels.Root );
		Assert.Equal( 2, factory.Requests.Count );
		Assert.Equal( new[] { 1f, 1f }, parameters.Values );
		Assert.Equal( 0, channels.FreeTreeCount );
	}

	private sealed class Factory : IAnimationOverlayTreeFactory
	{
		internal readonly List<AnimationChannelReplacement> Requests = new();
		public IAnimationPlaybackTree Create( int storage, AnimationChannelReplacement request )
		{
			Requests.Add( request );
			return new Tree( new Node() );
		}
		public void Release( int storage, IAnimationPlaybackTree tree ) { }
	}
	private sealed record Tree( IAnimationPlaybackNode Root ) : IAnimationPlaybackTree;
	private sealed class Node : IAnimationPosePlaybackNode
	{
		public SkeletonPose Pose { get; } = new( 1 );
		public float Duration => 1f;
		public float Elapsed => 0f;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => true;
		public void SamplePoseStreams( AnimationPoseStreamContext context ) { }
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) { }
		public void Seek( float time ) { }
	}
	private sealed class Parameters : IAnimationEntryActionContext
	{
		internal readonly List<float> Values = new();
		public AnimationAttributeKey EmptyTargetKey => default;
		public bool TryGetAttribute( AnimationAttributeKey name, ref AnimationAttributeQueryResult result ) => false;
		public AnimationActionValueTable GetValueTable( uint kind ) => null;
		public void SetParameter( AnimationAttributeKey name, float value, bool normalized, int owner )
		{
			Assert.Equal( AnimationAttributeKey.Encode( "torso" ), name );
			Assert.False( normalized );
			Assert.Equal( -1, owner );
			Values.Add( value );
		}
	}
}
