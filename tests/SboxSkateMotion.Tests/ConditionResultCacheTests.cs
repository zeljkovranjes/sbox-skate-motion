using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class ConditionResultCacheTests
{
	[Fact]
	public void FalseAndTrueResultsAreSharedAcrossNodeAndOwnerChanges()
	{
		var calls = new List<(int, uint, uint)>();
		var answer = false;
		var cache = new ConditionResultCache( 2, ( index, node, owner ) =>
		{
			calls.Add( (index, node, owner) );
			return answer;
		} );
		Assert.False( cache.Evaluate( 0, 10, 20 ) );
		answer = true;
		Assert.False( cache.Evaluate( 0, 30, 40 ) );
		Assert.True( cache.Evaluate( 1, 50, 60 ) );
		answer = false;
		Assert.True( cache.Evaluate( 1, 70, 80 ) );
		Assert.Equal( new[] { (0, 10u, 20u), (1, 50u, 60u) }, calls );
	}

	[Fact]
	public void InvalidationAllowsFreshValuesAndKeepsIndicesAcrossWordBoundariesIndependent()
	{
		var answer = false;
		var calls = 0;
		var cache = new ConditionResultCache( 65, ( _, _, _ ) => { calls++; return answer; } );
		int[] indices = [0, 31, 32, 63, 64];
		foreach ( var index in indices )
			Assert.False( cache.Evaluate( index, 0, 0 ) );
		answer = true;
		foreach ( var index in indices )
			Assert.False( cache.Evaluate( index, 1, 2 ) );
		Assert.Equal( 5, calls );
		cache.Invalidate();
		foreach ( var index in indices )
			Assert.True( cache.Evaluate( index, 3, 4 ) );
		Assert.Equal( 10, calls );
	}

	[Fact]
	public void NestedEvaluationPreservesOtherResultsPublishedDuringTheCallback()
	{
		ConditionResultCache cache = null!;
		var calls = new List<int>();
		cache = new( 2, ( index, node, owner ) =>
		{
			calls.Add( index );
			return index == 0 ? !cache.Evaluate( 1, node, owner ) : true;
		} );
		Assert.False( cache.Evaluate( 0, 0, 0 ) );
		Assert.True( cache.Evaluate( 1, 0, 0 ) );
		Assert.Equal( new[] { 0, 1 }, calls );
	}

	[Fact]
	public void CallbackInvalidationClearsEarlierResultsButPublishesTheCurrentResultAfterward()
	{
		ConditionResultCache cache = null!;
		var calls = new List<int>();
		cache = new( 2, ( index, _, _ ) =>
		{
			calls.Add( index );
			if ( index == 1 )
				cache.Invalidate();
			return true;
		} );
		Assert.True( cache.Evaluate( 0, 0, 0 ) );
		Assert.True( cache.Evaluate( 1, 0, 0 ) );
		Assert.True( cache.Evaluate( 0, 0, 0 ) );
		Assert.True( cache.Evaluate( 1, 0, 0 ) );
		Assert.Equal( new[] { 0, 1, 0 }, calls );
	}

	[Fact]
	public void FailedEvaluationDoesNotPublishAValidResult()
	{
		var attempts = 0;
		var cache = new ConditionResultCache( 1, ( _, _, _ ) =>
		{
			if ( ++attempts == 1 )
				throw new InvalidOperationException();
			return true;
		} );
		Assert.Throws<InvalidOperationException>( () => cache.Evaluate( 0, 0, 0 ) );
		Assert.True( cache.Evaluate( 0, 0, 0 ) );
		Assert.True( cache.Evaluate( 0, 0, 0 ) );
		Assert.Equal( 2, attempts );
	}

	[Fact]
	public void InvalidIndicesFailWithoutCallingThePredicate()
	{
		var cache = new ConditionResultCache( 2, ( _, _, _ ) => throw new Exception() );
		Assert.Throws<ArgumentOutOfRangeException>( () => cache.Evaluate( -1, 0, 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => cache.Evaluate( 2, 0, 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new ConditionResultCache( -1, static ( _, _, _ ) => false ) );
	}

	[Fact]
	public void GraphInvalidationRefreshesTimerConditionsOnEachUpdate()
	{
		var timers = new StateBehaviorLifecycle( [new( 0, 0 ), new( 0, 0 ), new( 0, 0 )], [],
			new( static _ => -1, static _ => { }, static _ => { }, static ( _, _ ) => { },
				static ( _, _ ) => { }, static _ => { } ), true );
		var time = new StateTimeCondition( new( ConditionComparisonMode.Greater, .25f ), true,
			static _ => 0, static _ => 0, timers.GetElapsedByHandle );
		var evaluations = 0;
		var cache = new ConditionResultCache( 1, ( _, _, owner ) => { evaluations++; return time.Evaluate( owner ); } );
		var graph = new HierarchicalStateGraph(
			[new( -1, -1, 1, 2, 0, 0, true, StateSelectionMode.Reselect ),
			new( 0, -1, -1, 0, 0, 1, true, StateSelectionMode.Retain ),
			new( 0, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )],
			[0], [new( 3, 2, 0 )], [[]], timers,
			( condition, owner, _ ) => cache.Evaluate( condition, 0, ~(uint)owner ), cache.Invalidate, static _ => { } );
		graph.Update( .5f );
		graph.Update( .5f );
		Assert.Equal( 1, graph.CurrentState );
		graph.Update( 0f );
		Assert.Equal( 2, graph.CurrentState );
		Assert.Equal( 2, evaluations );
	}

	[Fact]
	public void ReusedCacheAndInvalidationDoNotAllocate()
	{
		var cache = new ConditionResultCache( 65, static ( index, _, _ ) => (index & 1) == 0 );
		for ( var index = 0; index < 1000; index++ )
		{
			cache.Invalidate();
			cache.Evaluate( index % 65, 0, 0 );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
		{
			cache.Invalidate();
			cache.Evaluate( index % 65, 0, 0 );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
