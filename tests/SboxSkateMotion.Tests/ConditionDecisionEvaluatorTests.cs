using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class ConditionDecisionEvaluatorTests
{
	[Fact]
	public void CompoundBranchesShortCircuitAndForwardContextInOrder()
	{
		var calls = new List<(int, uint, uint)>();
		var first = false;
		var evaluator = new ConditionDecisionEvaluator( [new( 1, -1, -2 ), new( 0, -1, 0 )], [1], [1],
			( condition, node, owner ) => { calls.Add( (condition, node, owner) ); return condition == 1 || first; } );
		Assert.False( evaluator.Evaluate( 0, 10, 20, false ) );
		Assert.Equal( new[] { (0, 10u, 20u) }, calls );
		calls.Clear();
		first = true;
		Assert.True( evaluator.Evaluate( 0, 30, 40, false ) );
		Assert.Equal( new[] { (0, 30u, 40u), (1, 30u, 40u) }, calls );
	}

	[Fact]
	public void EntryAndStayingUseSeparateRootsAndShareCachedConditions()
	{
		var calls = 0;
		var answer = true;
		var cache = new ConditionResultCache( 1, ( _, _, _ ) => { calls++; return answer; } );
		var evaluator = new ConditionDecisionEvaluator( [new( 0, -1, -2 ), new( 0, -2, -1 )], [0], [1], cache.Evaluate );
		Assert.True( evaluator.Evaluate( 0, 1, 2, false ) );
		answer = false;
		Assert.False( evaluator.Evaluate( 0, 3, 4, true ) );
		Assert.Equal( 1, calls );
		cache.Invalidate();
		Assert.True( evaluator.Evaluate( 0, 3, 4, true ) );
		Assert.Equal( 2, calls );
	}

	[Fact]
	public void TerminalRootsDoNotEvaluateConditions()
	{
		var evaluator = new ConditionDecisionEvaluator( [], [-1, -2], [-2, -1], static ( _, _, _ ) => throw new Exception() );
		Assert.False( evaluator.Evaluate( 0, 0, 0, false ) );
		Assert.True( evaluator.Evaluate( 1, 0, 0, false ) );
		Assert.True( evaluator.Evaluate( 0, 0, 0, true ) );
		Assert.False( evaluator.Evaluate( 1, 0, 0, true ) );
	}

	[Fact]
	public void EqualBranchesAndRepeatedConditionsPreserveInvalidationEffects()
	{
		var calls = new List<int>();
		var first = true;
		ConditionResultCache cache = null!;
		cache = new( 2, ( condition, _, _ ) =>
		{
			calls.Add( condition );
			if ( condition == 1 )
			{
				cache.Invalidate();
				first = false;
			}
			return first;
		} );
		var evaluator = new ConditionDecisionEvaluator(
			[new( 0, -1, -2 ), new( 1, 0, 0 ), new( 0, -1, 1 )], [2], [], cache.Evaluate );
		Assert.False( evaluator.Evaluate( 0, 0, 0, false ) );
		Assert.Equal( new[] { 0, 1, 0 }, calls );
	}

	[Fact]
	public void CallerChangesCannotMutateTheDecisionProgram()
	{
		ConditionDecision[] decisions = [new( 0, -1, -2 )];
		int[] entry = [0], staying = [-2];
		var evaluator = new ConditionDecisionEvaluator( decisions, entry, staying, static ( _, _, _ ) => true );
		decisions[ 0 ] = new( 0, -1, -1 );
		entry[ 0 ] = staying[ 0 ] = -1;
		Assert.True( evaluator.Evaluate( 0, 0, 0, false ) );
		Assert.True( evaluator.Evaluate( 0, 0, 0, true ) );
	}

	[Fact]
	public void InvalidProgramsFailBeforeAnyPredicateRuns()
	{
		Func<int, uint, uint, bool> evaluate = static ( _, _, _ ) => throw new Exception();
		Assert.Throws<ArgumentException>( () => new ConditionDecisionEvaluator( [new( -1, -1, -2 )], [0], [], evaluate ) );
		Assert.Throws<ArgumentException>( () => new ConditionDecisionEvaluator( [new( 0, 0, -2 )], [0], [], evaluate ) );
		Assert.Throws<ArgumentException>( () => new ConditionDecisionEvaluator( [new( 0, -1, 1 )], [0], [], evaluate ) );
		Assert.Throws<ArgumentException>( () => new ConditionDecisionEvaluator( [new( 0, -3, -2 )], [0], [], evaluate ) );
		Assert.Throws<ArgumentException>( () => new ConditionDecisionEvaluator( [], [0], [], evaluate ) );
		Assert.Throws<ArgumentException>( () => new ConditionDecisionEvaluator( [], [], [-3], evaluate ) );
		Assert.Throws<ArgumentNullException>( () => new ConditionDecisionEvaluator( [], [], [], null! ) );
	}

	[Fact]
	public void InvalidGuardUsesTheSelectedRootTableBounds()
	{
		var evaluator = new ConditionDecisionEvaluator( [], [-2], [], static ( _, _, _ ) => throw new Exception() );
		Assert.Throws<ArgumentOutOfRangeException>( () => evaluator.Evaluate( -1, 0, 0, false ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => evaluator.Evaluate( 1, 0, 0, false ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => evaluator.Evaluate( 0, 0, 0, true ) );
	}

	[Fact]
	public void FailedPredicateStopsBeforeLaterBranchesAndCanBeRetried()
	{
		var fail = true;
		var calls = new List<int>();
		var evaluator = new ConditionDecisionEvaluator( [new( 1, -1, -2 ), new( 0, -1, 0 )], [1], [],
			( condition, _, _ ) =>
			{
				calls.Add( condition );
				if ( fail )
					throw new InvalidOperationException();
				return true;
			} );
		Assert.Throws<InvalidOperationException>( () => evaluator.Evaluate( 0, 0, 0, false ) );
		Assert.Equal( new[] { 0 }, calls );
		calls.Clear();
		fail = false;
		Assert.True( evaluator.Evaluate( 0, 0, 0, false ) );
		Assert.Equal( new[] { 0, 1 }, calls );
	}

	[Fact]
	public void ReusedCompoundEvaluationAndCacheInvalidationDoNotAllocate()
	{
		var cache = new ConditionResultCache( 2, static ( _, _, _ ) => true );
		var evaluator = new ConditionDecisionEvaluator( [new( 1, -1, -2 ), new( 0, -1, 0 )], [1], [1], cache.Evaluate );
		for ( var index = 0; index < 1000; index++ )
		{
			cache.Invalidate();
			evaluator.Evaluate( 0, 0, 0, false );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
		{
			cache.Invalidate();
			evaluator.Evaluate( 0, 0, 0, true );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
