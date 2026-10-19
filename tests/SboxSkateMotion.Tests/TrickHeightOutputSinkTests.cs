using System;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickHeightOutputSinkTests
{
	private static readonly AnimationAttributeKey Height = AnimationAttributeKey.Encode( "TRICKHEIGHT" );

	[Fact]
	public void RepeatedEntriesQueueEveryOverrideAndReplaceTheOwnedHeight()
	{
		var attributes = new AnimationAttributeOverrideBuffer( 2, [] );
		var sink = new TrickHeightOutputSink( attributes, 3, _ => Height );
		sink.Publish( 0, new( .5f, .1f ) );
		sink.Publish( 0, new( .75f, .15f ) );
		Assert.Equal( 2, sink.QueuedCount );
		Assert.Equal( .1f, sink.Queued[ 0 ].Value );
		Assert.Equal( .15f, sink.Queued[ 1 ].Value );
		Assert.Equal( sink.Queued[ 0 ].Name, sink.Queued[ 1 ].Name );
		Assert.Equal( 1, attributes.Count );
		Assert.Equal( .75f, attributes.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.True( attributes.Values[ 0 ].Persistent );
		Assert.Equal( -1, attributes.Values[ 0 ].Owner );
	}

	[Fact]
	public void MatchingKeyWithAnotherOwnerIsNotOverwritten()
	{
		var initial = Record( Height, 5, 1u );
		var buffer = new AnimationAttributeOverrideBuffer( 2, [ initial ] );
		buffer.WriteScalar( Height, .5f, true, -1 );
		Assert.Equal( initial, buffer.Values[ 0 ] );
		Assert.Equal( 2, buffer.Count );
		Assert.Equal( -1, buffer.Values[ 1 ].Owner );
	}

	[Fact]
	public void FirstMatchingRecordPreservesItsOtherPayloadAndMetadata()
	{
		var initial = Record( Height, 7, 0x3f800000 );
		var buffer = new AnimationAttributeOverrideBuffer( 2, [ initial, initial ] );
		buffer.WriteScalar( Height, -.5f, true, 7 );
		var expected = initial with { Persistent = true, Attribute = initial.Attribute with
		{
			Payload = initial.Attribute.Payload with { A = 0xbf000000 }
		} };
		Assert.Equal( expected, buffer.Values[ 0 ] );
		Assert.Equal( initial, buffer.Values[ 1 ] );
	}

	[Fact]
	public void NewScalarMetadataHasUnboundedIntervalAndFixedStatus()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		buffer.WriteScalar( Height, 0f, false, 9 );
		var record = buffer.Values[ 0 ];
		Assert.Equal( -1f, record.Attribute.Start );
		Assert.Equal( -1f, record.Attribute.End );
		Assert.Equal( (byte)6, record.Attribute.Flags );
		Assert.Equal( (sbyte)0, record.Attribute.Kind );
		Assert.False( record.Persistent );
	}

	[Fact]
	public void ScalarWriteRetainsSignedZeroAndQuietNanBits()
	{
		var buffer = new AnimationAttributeOverrideBuffer( 1, [] );
		buffer.WriteScalar( Height, BitConverter.UInt32BitsToSingle( 0x80000000 ), false, -1 );
		Assert.Equal( 0x80000000u, buffer.Values[ 0 ].Attribute.Payload.A );
		buffer.WriteScalar( Height, BitConverter.UInt32BitsToSingle( 0x7fc00037 ), false, -1 );
		Assert.Equal( 0x7fc00037u, buffer.Values[ 0 ].Attribute.Payload.A );
	}

	[Fact]
	public void BindingPublishesToConcreteSinkOnlyOnEntry()
	{
		var requests = new InputRequestBuffer( 1 );
		requests.Add( InputCommandKey.Encode( "GESTURESPEED"u8 ), .75f );
		var sink = new TrickHeightOutputSink( new( 1, [] ), 2, _ => Height );
		var external = new StateBehaviorCallbacks( _ => throw new InvalidOperationException(),
			_ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
			( _, _ ) => throw new InvalidOperationException(), ( _, _ ) => throw new InvalidOperationException(),
			_ => throw new InvalidOperationException() );
		var binding = new TrickBehaviorBinding( [ TrickBehaviorKind.Height ], [ TrickBehaviorKind.Height ],
			[], default, requests, () => [], _ => new( default, null, true, false, false, false ),
			sink.Publish, external, ( _, _, _ ) => throw new InvalidOperationException() );
		binding.Callbacks.Enter( 0, -1 );
		binding.Update( 0, 0, -1 );
		binding.Callbacks.Exit( 0, -1 );
		Assert.Equal( 1, sink.QueuedCount );
		Assert.Equal( .75f, sink.Attributes.Values[ 0 ].Attribute.Payload.Scalar );
	}

	private static AnimationAttributeOverride Record( AnimationAttributeKey name, int owner, uint scalar ) =>
		new( new( new( scalar, 2, 3, 4, 5, 6 ), name, .2f, .8f, 0x79, 3 ), owner, false );
}
