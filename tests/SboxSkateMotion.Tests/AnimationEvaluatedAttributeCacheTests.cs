using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class AnimationEvaluatedAttributeCacheTests
{
	[Fact]
	public void CachedLookupUsesFirstKeyWithoutTimingFilterAndRetainsUnusedPayload()
	{
		var key = new AnimationAttributeKey( 1, 2, 3, 4, 5 );
		AnimationAttributeQueryResult[] records =
		[
			new( new( 10, 20, 30, 40, 50, 60 ), key, .4f, .8f, 0, 1 ),
			new( new( 99, 99, 99, 99, 99, 99 ), key, -1f, -1f, 6, 3 )
		];
		var cache = new AnimationEvaluatedAttributeCache( 2 );
		cache.Publish( records );
		records[ 0 ] = records[ 1 ];
		var result = new AnimationAttributeQueryResult( new( 1, 2, 3, 4, 5, 6 ), default, -1f, -1f, 6, 0 );
		Assert.True( cache.TryGet( key, ref result ) );
		Assert.Equal( new AnimationAttributePayload( 10, 20, 30, 40, 5, 6 ), result.Payload );
		Assert.Equal( (byte)0, result.Flags );
		Assert.Equal( .4f, result.Start );
		var retained = result;
		Assert.False( cache.TryGet( key with { E = 6 }, ref result ) );
		Assert.Equal( retained, result );
		cache.Publish( [] );
		Assert.False( cache.TryGet( key, ref result ) );
		Assert.Equal( retained, result );
	}

	[Fact]
	public void EntryActionsReadPublishedCacheAndQueueParametersUntilExplicitFlush()
	{
		var key = AnimationAttributeKey.Encode( "HEIGHT" );
		var cache = new AnimationEvaluatedAttributeCache( 1 );
		cache.Publish( [new( new( BitConverter.SingleToUInt32Bits( .75f ), 0, 0, 0, 0, 0 ), key, -1f, -1f, 6, 0 )] );
		var parameters = new AnimationAttributeOverrideBuffer( 2, [] );
		var table = new AnimationActionValueTable( [-1, -1], [] );
		var context = new AnimationEntryActionOwnerContext( cache, parameters, _ => table, default );
		AnimationEntryAction[] actions = [new( 0, default, key, default, 9f, 0x80000000 )];
		AnimationEntryActionProcessor.Execute( actions, true, context );
		Assert.Equal( 1, parameters.Count );
		Assert.Equal( .75f, parameters.Values[ 0 ].Attribute.Payload.Scalar );
		Assert.False( parameters.Values[ 0 ].Normalized );
		Assert.Equal( -1, parameters.Values[ 0 ].Owner );
		cache.Publish( [] );
		AnimationEntryActionProcessor.Execute( actions, false, context );
		Assert.Equal( .75f, parameters.Values[ 0 ].Attribute.Payload.Scalar );
		AnimationEntryActionProcessor.Execute( actions, true, context );
		Assert.Equal( 1, parameters.Count );
		Assert.Equal( 9f, parameters.Values[ 0 ].Attribute.Payload.Scalar );
		var consumer = new Consumer();
		parameters.Flush( consumer );
		Assert.Equal( 9f, consumer.Value );
		Assert.Equal( 0, parameters.Count );
	}

	private sealed class Consumer : IAnimationAttributeOverrideConsumer
	{
		internal float Value;
		public bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> values )
		{
			Value = values[ 0 ].Attribute.Payload.Scalar;
			return true;
		}
	}
}
