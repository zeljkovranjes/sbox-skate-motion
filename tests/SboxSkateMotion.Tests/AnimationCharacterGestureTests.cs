using System;
using System.Collections.Generic;
using System.Text;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AnimationCharacterGestureTests
{
	private static InputCommandKey Key( uint value ) => new( value, 0, 0, 0, 0, 0 );
	private static AnimationCharacterGestureDefinition Definition() => new(
		[Key( 1 ), Key( 2 ), Key( 3 )], [Key( 4 ), Key( 5 ), Key( 6 ), Key( 7 )],
		[Key( 8 ), Key( 9 ), Key( 10 ), Key( 11 )], [Key( 12 ), Key( 13 ), Key( 14 )],
		Key( 15 ), Key( 16 ), Key( 17 ), new( 18, 0, 0, 0, 0 ) );

	[Fact]
	public void RetainedGestureCyclesExitsAndKeepsItsPhaseWhenThePhysicalModeChanges()
	{
		var definition = Definition();
		var context = new Context();
		var instance = new AnimationCharacterGestureInstance();
		context.Requests.Add( definition.Start( 0 ) );
		instance.Update( definition, context );
		Assert.True( instance.Active );
		Assert.Equal( 0, instance.Phase );
		context.Requests.Clear();
		context.Requests.Add( definition.Held( 0 ) );
		instance.Update( definition, context );
		Assert.Equal( 1, instance.Phase );
		instance.Update( definition, context );
		Assert.Equal( 1, instance.Phase );
		context.Requests.Clear();
		instance.Update( definition, context );
		Assert.Equal( 2, instance.Phase );
		instance.Update( definition, context );
		Assert.False( instance.Active );
		Assert.Equal( -1, instance.Phase );
		context.Channels.Clear();
		context.Mode = new( 6, false, true, 0, 0 );
		context.Requests.Add( definition.Start( 3 ) );
		instance.Update( definition, context );
		context.Requests.Clear();
		context.Requests.Add( definition.Held( 3 ) );
		instance.Update( definition, context );
		context.Mode = new( 6, false, false, 0, 0 );
		instance.Update( definition, context );
		Assert.False( instance.Active );
		Assert.Equal( 1, instance.Phase );
		Assert.Equal( -1, instance.Gesture );
		Assert.Equal( -1, instance.Mode );
		Assert.Equal( new[] { false, true, false, false, true }, context.QueuedCycles );
	}

	[Fact]
	public void CompiledBindingSharesTheActorAndRealChannelOwnerWithExplicitMotionSuffixes()
	{
		var definition = Definition();
		var requests = new InputRequestBuffer( 4 );
		var factory = new Factory();
		var channels = new AnimationOverlayOwner( 3, factory );
		channels.PublishBase( new Node() );
		var attributes = new AnimationAttributeOverrideBuffer( 2, [] );
		var actor = new AnimationGestureActorState( 0, 0, -1, false, false );
		var baseName = InputCommandKey.Encode( Encoding.ASCII.GetBytes( "WAVE" ) );
		var motions = new AnimationGestureMotionTable( [baseName],
			["BOTH"u8.ToArray(), "RIGHT"u8.ToArray(), "LEFT"u8.ToArray()],
			["INTO"u8.ToArray(), "CYC"u8.ToArray(), "OUT"u8.ToArray()] );
		var resolved = new List<InputCommandKey>();
		var context = new AnimationCharacterGestureOwnerContext( requests, channels, motions,
			key => { resolved.Add( key ); return resolved.Count; }, attributes, () => new( 0, 0, 0, 0 ),
			() => new( 2, false, false, true, .2f ), () => false, actor );
		var binding = new AnimationCharacterGestureBinding( definition, context );
		var lifecycle = binding.Bind();
		var handle = lifecycle.Create();
		binding.Retain( handle );
		lifecycle.Enter( handle );
		requests.Add( definition.Start( 0 ), 0f );
		binding.Update( handle );
		Assert.True( actor.GesturePublished );
		Assert.False( actor.PublishedDown );
		Assert.Equal( 0, actor.PublishedGesture );
		Assert.Equal( 1f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.Equal( new AnimationChannelReplacement( 1, 1f, false, true, .2f, .1f, .1f, 0xc0000000, true ), factory.Requests[ 0 ] );
		Assert.Equal( InputCommandKey.Encode( "WAVE_BOTH_INTO"u8 ), resolved[ 0 ] );
		Assert.True( channels.Contains( definition.Channel( 0 ) ) );
		Assert.Equal( .05f, channels.Remaining( definition.Channel( 0 ) ) );
		requests.Clear();
		requests.Add( definition.Held( 0 ), 1f );
		binding.Update( handle );
		Assert.True( channels.HasPending( definition.Channel( 0 ) ) );
		Assert.Equal( new AnimationChannelReplacement( 2, 1f, false, false, 0f, .1f, .1f, 0x50000000, true ), factory.Requests[ 1 ] );
		Assert.Equal( InputCommandKey.Encode( "WAVE_BOTH_CYC"u8 ), resolved[ 1 ] );
		lifecycle.Exit( handle );
		lifecycle.Transition();
		binding.Release( handle );
		binding.Update( handle );
		binding.Release( handle );
		Assert.Throws<InvalidOperationException>( () => binding.Update( handle ) );
		Assert.Equal( handle, lifecycle.Create() );
	}

	private sealed class Context : IAnimationCharacterGestureContext
	{
		internal readonly HashSet<InputCommandKey> Requests = new();
		internal readonly HashSet<InputCommandKey> Channels = new();
		internal readonly List<bool> QueuedCycles = new();
		internal AnimationGestureModeInput Mode = new( 2, false, false, 0, 0 );
		public bool ContainsRequest( InputCommandKey key ) => Requests.Contains( key );
		public bool HasChannel( InputCommandKey channel ) => Channels.Contains( channel );
		public void StopChannel( InputCommandKey channel ) => Channels.Remove( channel );
		public AnimationGestureSelection ReadSelection() => new( 0, 1, 2, 3 );
		public AnimationGestureModeInput ReadModeInput() => Mode;
		public bool IsUpBlocked() => false;
		public float Remaining( InputCommandKey channel ) => .05f;
		public bool HasPending( InputCommandKey channel ) => false;
		public InputCommandKey Motion( int configuration, int mode, int phase ) => Key( (uint)configuration );
		public void StartChannel( InputCommandKey channel, InputCommandKey motion ) { Channels.Add( channel ); QueuedCycles.Add( false ); }
		public void QueueChannel( InputCommandKey channel, InputCommandKey motion, bool cycle ) => QueuedCycles.Add( cycle );
		public void PublishGesture( int configuration, bool down ) { }
		public float ReadDistance() => .5f;
		public void SetDistance( AnimationAttributeKey key, float value ) { }
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
		public float Duration => .05f;
		public float Elapsed => 0f;
		public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => true;
		public void SamplePoseStreams( AnimationPoseStreamContext context ) { }
		public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) { }
		public void Seek( float time ) { }
	}
}
