using System;
using System.Collections.Generic;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CompositePoseControlTests
{
	[Fact]
	public void SequencePoseFollowsPlaybackHandoff()
	{
		var clock = new AnimationSequencePlayback( Leaf(), Leaf() );
		var operations = new Operations();
		var pose = new AnimationSequencePoseControl( clock, operations );
		Assert.True( pose.Prepare( false ) );
		pose.Sample();
		Assert.Equal( [ "prepare0:False", "sample0" ], operations.Events );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( 1.1f, 0f, ref completion );
		operations.Events.Clear();
		Assert.True( pose.Prepare( true ) );
		pose.Sample();
		Assert.Equal( [ "prepare1:True", "sample1" ], operations.Events );
	}

	[Theory]
	[InlineData( 0, "blend" )]
	[InlineData( 1073741824, "blend" )]
	[InlineData( -2147483648, "weighted:False" )]
	[InlineData( -1073741824, "weighted:True" )]
	[InlineData( -1, "weighted:True" )]
	public void TransitionMaskSourceUsesItsOwnBit( int mode, string expected )
	{
		var clock = Transition( .2f, .05f );
		var operations = new Operations();
		var pose = new AnimationTransitionPoseControl( clock, operations, mode );
		Assert.True( pose.Prepare( true ) );
		Assert.True( pose.HasPose );
		Assert.Equal( [ "prepare0:True", "prepare1:True", expected ], operations.Events );
		Assert.Equal( .15625f, operations.Weight );
	}

	[Theory]
	[InlineData( false, true, true, true )]
	[InlineData( true, false, true, true )]
	[InlineData( true, true, false, true )]
	[InlineData( true, false, false, false )]
	public void TransitionPreparesBothChildrenWithoutBlendingWhenUnavailable( bool enabled, bool first, bool second, bool ready )
	{
		var operations = new Operations { FirstReady = first, SecondReady = second };
		var pose = new AnimationTransitionPoseControl( Transition( .2f, .1f ), operations, 0 );
		Assert.Equal( ready, pose.Prepare( enabled ) );
		Assert.Equal( ready, pose.HasPose );
		Assert.Equal( [ $"prepare0:{enabled}", $"prepare1:{enabled}" ], operations.Events );
	}

	[Theory]
	[InlineData( 0f, "sample0" )]
	[InlineData( .2f, "sample1" )]
	public void TransitionSampleSelectsOnlyEndpointChild( float elapsed, string expected )
	{
		var operations = new Operations();
		new AnimationTransitionPoseControl( Transition( .2f, elapsed ), operations, 0 ).Sample();
		Assert.Equal( [ expected ], operations.Events );
	}

	[Fact]
	public void TransitionCapturesWeightBeforeChildCallbacks()
	{
		var clock = Transition( .2f, .05f );
		var operations = new Operations { OnSample = _ => clock.RestartBlend() };
		new AnimationTransitionPoseControl( clock, operations, 0 ).Sample();
		Assert.Equal( [ "sample0", "sample1", "queue" ], operations.Events );
		Assert.Equal( .15625f, operations.Weight );
		Assert.Equal( 0f, clock.Weight );
	}

	[Fact]
	public void ChannelRechecksWeightAfterBasePreparation()
	{
		var blend = Channel( 0f, .8f );
		var operations = new Operations { OnPrepare = child => { if ( child == 0 ) blend.Update( 0f, 10f, 0f ); } };
		Assert.True( new AnimationChannelPoseControl( blend, operations ).Prepare( true ) );
		Assert.Equal( [ "prepare0:True", "prepare1:True", "weighted:False" ], operations.Events );
		Assert.Equal( .8f, operations.Weight );
	}

	[Fact]
	public void ChannelRechecksWeightAfterOverlayPreparation()
	{
		var blend = Channel( .5f, 0f );
		var operations = new Operations { OnPrepare = child => { if ( child == 1 ) blend.Update( 0f, 10f, 0f ); } };
		Assert.True( new AnimationChannelPoseControl( blend, operations ).Prepare( true ) );
		Assert.Equal( [ "prepare0:True", "prepare1:True" ], operations.Events );
	}

	[Fact]
	public void ChannelQueueUsesWeightAfterOverlaySampling()
	{
		var blend = Channel( .2f, .8f );
		var operations = new Operations { OnSample = child => { if ( child == 1 ) blend.Update( 0f, 10f, 0f ); } };
		new AnimationChannelPoseControl( blend, operations ).Sample();
		Assert.Equal( [ "sample0", "sample1", "queue" ], operations.Events );
		Assert.Equal( .8f, operations.Weight );
	}

	private static AnimationClipClock Leaf() => new( 2f, 1f, 1f, false, false );

	private static AnimationTransitionPlayback Transition( float duration, float elapsed )
	{
		var clock = new AnimationTransitionPlayback( Leaf(), Leaf(), duration, AnimationTransitionMode.Both );
		var completion = AnimationPlaybackCompletion.Empty;
		clock.Advance( elapsed, 0f, ref completion );
		return clock;
	}

	private static AnimationChannelBlend Channel( float weight, float target ) =>
		new( new AnimationChannelBlendState( weight, target, 0f, 0f, 0f, 0f, 0f, 0f, 0u ) );

	private sealed class Operations : IAnimationLayerPoseOperations
	{
		internal readonly List<string> Events = new();
		internal bool FirstReady = true;
		internal bool SecondReady = true;
		internal float Weight;
		internal Action<int> OnPrepare;
		internal Action<int> OnSample;

		public bool Prepare( int child, bool enabled )
		{
			Events.Add( $"prepare{child}:{enabled}" );
			OnPrepare?.Invoke( child );
			return child == 0 ? FirstReady : SecondReady;
		}

		public void Sample( int child )
		{
			Events.Add( $"sample{child}" );
			OnSample?.Invoke( child );
		}

		public void Blend( float weight )
		{
			Events.Add( "blend" );
			Weight = weight;
		}

		public void QueueBlend( float weight )
		{
			Events.Add( "queue" );
			Weight = weight;
		}

		public void BlendWeighted( float weight, bool maskFromFirst )
		{
			Events.Add( $"weighted:{maskFromFirst}" );
			Weight = weight;
		}
	}
}
