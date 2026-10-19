using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickOutputLifetimeTests
{
	private static readonly AnimationAttributeKey Height = AnimationAttributeKey.Encode( "TRICKHEIGHT" );

	[Fact]
	public void PublicationSelectsNormalizedHeightAndDrainsOnlyChannelRecords()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 2, [] );
		var sink = new TrickHeightOutputSink( buffer, 2, _ => Height );
		var parameters = new Parameters();
		var playback = Playback();
		var consumer = new AnimationDirectionalOverrideConsumer( playback, Height, parameters );
		sink.Publish( 0, new( .25f, .1f ) );
		buffer.Flush( consumer );
		Assert.Equal( .25f, playback.Weight );
		Assert.Equal( 0, buffer.Count );
		Assert.Equal( 1, sink.QueuedCount );
		Assert.Equal( .1f, sink.Queued[ 0 ].Value );
		Assert.Equal( 2, parameters.Updates );
		Assert.Equal( 2, parameters.Queries );
	}

	[Fact]
	public void EmptyFlushDoesNotTraverseAndSelectionSurvivesTheDrain()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		var parameters = new Parameters();
		var playback = Playback();
		var consumer = new AnimationDirectionalOverrideConsumer( playback, Height, parameters );
		buffer.WriteScalar( Height, .75f, true, -1 );
		buffer.Flush( consumer );
		buffer.Flush( consumer );
		Assert.Equal( .75f, playback.Weight );
		Assert.Equal( 2, parameters.Updates );
	}

	[Fact]
	public void FirstMatchingKeyWinsRegardlessOfOwner()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 3, [] );
		buffer.WriteScalar( Height with { E = Height.E ^ 1 }, 99f, true, -1 );
		buffer.WriteScalar( Height, .25f, true, 31 );
		buffer.WriteScalar( Height, .75f, true, -1 );
		var playback = Playback();
		buffer.Flush( new AnimationDirectionalOverrideConsumer( playback, Height, new Parameters() ) );
		Assert.Equal( .25f, playback.Weight );
		Assert.Equal( 0, buffer.Count );
	}

	[Fact]
	public void NormalizedBitChangesCoordinateInterpretationWithoutChangingScalar()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		var playback = Playback();
		var consumer = new AnimationDirectionalOverrideConsumer( playback, Height, new Parameters() );
		buffer.WriteScalar( Height, .5f, false, -1 );
		buffer.Flush( consumer );
		Assert.Equal( .005f, playback.Weight );
		buffer.WriteScalar( Height, .5f, true, -1 );
		buffer.Flush( consumer );
		Assert.Equal( .5f, playback.Weight );
	}

	[Fact]
	public void UnrelatedParameterStillRefreshesDirtyChildrenWithRetainedParentMode()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		var playback = Playback();
		var parameters = new Parameters();
		var consumer = new AnimationDirectionalOverrideConsumer( playback, Height, parameters );
		buffer.WriteScalar( Height, .25f, true, -1 );
		buffer.Flush( consumer );
		parameters.Dirty = true;
		parameters.End = 200f;
		buffer.WriteScalar( AnimationAttributeKey.Encode( "OTHER" ), 999f, false, -1 );
		buffer.Flush( consumer );
		Assert.Equal( .25f, playback.Weight );
		Assert.Equal( 4, parameters.Updates );
		Assert.Equal( 4, parameters.Queries );
	}

	[Fact]
	public void QueueResetPreservesPendingAttributesAndReusesCapacity()
	{
		var sink = new TrickHeightOutputSink( new( 1, [] ), 1, _ => Height );
		sink.Publish( 0, new( .25f, .1f ) );
		sink.ResetQueued();
		Assert.Equal( 0, sink.QueuedCount );
		Assert.Equal( 1, sink.Attributes.Count );
		sink.Publish( 0, new( .75f, .2f ) );
		Assert.Equal( 1, sink.QueuedCount );
		Assert.Equal( .2f, sink.Queued[ 0 ].Value );
		Assert.Equal( .75f, sink.Attributes.Values[ 0 ].Attribute.Payload.Scalar );
	}

	[Fact]
	public void FailedConsumerDoesNotDiscardPendingParameters()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		buffer.WriteScalar( Height, .25f, true, -1 );
		Assert.Throws<InvalidOperationException>( () => buffer.Flush( new FailingConsumer() ) );
		Assert.Equal( 1, buffer.Count );
		Assert.True( buffer.Values[ 0 ].Normalized );
	}

	[Fact]
	public void UnchangedParameterDrainsWithoutRepeatingSampleQueries()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		var parameters = new Parameters();
		var consumer = new AnimationDirectionalOverrideConsumer( Playback(), Height, parameters );
		buffer.WriteScalar( Height, .25f, true, -1 );
		buffer.Flush( consumer );
		buffer.WriteScalar( Height, .25f, true, -1 );
		buffer.Flush( consumer );
		Assert.Equal( 0, buffer.Count );
		Assert.Equal( 4, parameters.Updates );
		Assert.Equal( 2, parameters.Queries );
	}

	private static AnimationDirectionalPlayback Playback() => new(
		[ new AnimationClipClock( 31, 30, 1, false, false ),
			new AnimationClipClock( 61, 30, 1, false, false ) ], [ 0f, 0f ] );

	private sealed class Parameters : IAnimationDirectionalParameters
	{
		internal int Updates;
		internal int Queries;
		internal bool Dirty;
		internal float End = 100f;
		public bool UpdateChild( int child )
		{
			Updates++;
			return Dirty;
		}

		public float QuerySample( int child )
		{
			Queries++;
			return child == 0 ? 0f : End;
		}
	}

	private sealed class FailingConsumer : IAnimationAttributeOverrideConsumer
	{
		public bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> attributes ) =>
			throw new InvalidOperationException();
	}
}
