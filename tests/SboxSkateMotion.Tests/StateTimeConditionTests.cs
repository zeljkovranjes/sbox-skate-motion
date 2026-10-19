using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class StateTimeConditionTests
{
	private static StateBehaviorLifecycle Timers() => new( [new( 0, 0 ), new( 0, 0 ), new( 0, 0 )], [],
		new( static _ => -1, static _ => { }, static _ => { }, static ( _, _ ) => { },
			static ( _, _ ) => { }, static _ => { } ), true );

	private static StateTimeCondition Direct( StateBehaviorLifecycle timers, ScalarConditionComparison comparison ) =>
		new( comparison, true, _ => throw new Exception(), _ => throw new Exception(), timers.GetElapsedByHandle );

	[Fact]
	public void EncodedOwnerUsesLifecycleTimeAndPublishesOwnerOnEveryEvaluation()
	{
		var timers = Timers();
		timers.Enter( [0, 1] );
		timers.AdvanceTimers( .5f );
		var condition = Direct( timers, new( ConditionComparisonMode.Equal, .5f ) );
		Assert.Equal( 0u, condition.OwnerState );
		Assert.True( condition.Evaluate( ~1u ) );
		Assert.Equal( ~1u, condition.OwnerState );
		Assert.False( condition.Evaluate( ~2u ) );
		Assert.Equal( ~2u, condition.OwnerState );
		timers.Exit( [1] );
		Assert.False( condition.Evaluate( ~1u ) );
	}

	[Fact]
	public void OwnerChainStopsAtTheFirstHandleAndPublishesBeforeCallingGetters()
	{
		StateTimeCondition condition = null!;
		var calls = new List<string>();
		condition = new( new( ConditionComparisonMode.Greater, .25f ), false, owner =>
		{
			Assert.Equal( 1u, condition.OwnerState );
			calls.Add( $"state:{owner}" );
			return owner == 2 ? ~1u : 0u;
		}, owner => { calls.Add( $"parent:{owner}" ); return owner + 1; }, handle =>
		{
			Assert.Equal( ~1u, handle );
			calls.Add( "elapsed" );
			return .5f;
		} );
		Assert.True( condition.Evaluate( 1 ) );
		Assert.Equal( new[] { "state:1", "parent:1", "state:2", "elapsed" }, calls );
	}

	[Fact]
	public void EmptyOwnerAndExhaustedChainStillQueryTheAbsentTimer()
	{
		var visits = new List<uint>();
		var handles = new List<uint>();
		var condition = new StateTimeCondition( new( ConditionComparisonMode.Greater, -2f ), false,
			owner => { visits.Add( owner ); return 0; }, owner => owner == 1 ? 2u : 0u,
			handle => { handles.Add( handle ); return -1f; } );
		Assert.False( condition.Evaluate( 0 ) );
		Assert.Empty( visits );
		Assert.False( condition.Evaluate( 1 ) );
		Assert.Equal( new uint[] { 1, 2 }, visits );
		Assert.Equal( new uint[] { 0, 0 }, handles );
		condition.OwnerIsHandle = true;
		Assert.False( condition.Evaluate( 0 ) );
		Assert.Equal( 3, handles.Count );
	}

	[Fact]
	public void NegativeTimeIsRejectedBeforeAbsoluteComparison()
	{
		var timers = Timers();
		timers.Enter( [1] );
		timers.AdvanceTimers( -.5f );
		var condition = Direct( timers, new( ConditionComparisonMode.Greater, .25f, true ) );
		Assert.False( condition.Evaluate( ~1u ) );
		Assert.Equal( -.5f, timers.GetElapsedByHandle( ~1u ) );
	}

	[Fact]
	public void UnorderedTimeReachesTheConfiguredScalarComparison()
	{
		var timers = Timers();
		timers.Enter( [1] );
		timers.AdvanceTimers( float.NaN );
		Assert.True( Direct( timers, new( ConditionComparisonMode.NotLess, .5f ) ).Evaluate( ~1u ) );
		Assert.False( Direct( timers, new( ConditionComparisonMode.Greater, .5f ) ).Evaluate( ~1u ) );
		Assert.False( Direct( timers, new( ConditionComparisonMode.None, 0f ) ).Evaluate( ~1u ) );
	}

	[Fact]
	public void TimerLookupUsesComplementedLowBitsAndRejectsMissingKeys()
	{
		var timers = Timers();
		timers.Enter( [0, 1] );
		timers.AdvanceTimers( .75f );
		Assert.Equal( .75f, timers.GetElapsedByHandle( uint.MaxValue ) );
		Assert.Equal( .75f, timers.GetElapsedByHandle( 0x1234fffe ) );
		Assert.Equal( -1f, timers.GetElapsedByHandle( 0 ) );
		Assert.Equal( -1f, timers.GetElapsedByHandle( 1 ) );
		Assert.Equal( -1f, timers.GetElapsedByHandle( ~2u ) );
	}

	[Fact]
	public void ClearPreservesElapsedConditionsWhileReentryResetsThem()
	{
		var timers = Timers();
		timers.Enter( [1] );
		timers.AdvanceTimers( .5f );
		var condition = Direct( timers, new( ConditionComparisonMode.Greater, .25f ) );
		timers.ClearActive();
		Assert.True( condition.Evaluate( ~1u ) );
		timers.Enter( [1] );
		Assert.False( condition.Evaluate( ~1u ) );
	}

	[Fact]
	public void OwnerModeIsReadAgainAfterAdvancingToAParent()
	{
		StateTimeCondition condition = null!;
		uint observed = 0;
		condition = new( new( ConditionComparisonMode.Equal, 1f ), false,
			_ => { condition.OwnerIsHandle = true; return 0; }, _ => ~1u,
			handle => { observed = handle; return 1f; } );
		Assert.True( condition.Evaluate( 1 ) );
		Assert.Equal( ~1u, observed );
	}

	[Fact]
	public void GraphEvaluatesElapsedGuardBeforeAdvancingTheCurrentUpdatesTimers()
	{
		var timers = Timers();
		var condition = Direct( timers, new( ConditionComparisonMode.Greater, .25f ) );
		var graph = new HierarchicalStateGraph(
			[new( -1, -1, 1, 2, 0, 0, true, StateSelectionMode.Reselect ),
			new( 0, -1, -1, 0, 0, 1, true, StateSelectionMode.Retain ),
			new( 0, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )],
			[0], [new( 3, 2, 5 )], [[]], timers,
			( _, owner, _ ) => condition.Evaluate( ~(uint)owner ), static () => { }, static _ => { } );
		graph.Update( .25f );
		Assert.Equal( 1, graph.CurrentState );
		graph.Update( .25f );
		Assert.Equal( 1, graph.CurrentState );
		graph.Update( .125f );
		Assert.Equal( 1, graph.CurrentState );
		graph.Update( 0f );
		Assert.Equal( 2, graph.CurrentState );
		Assert.Equal( ~1u, condition.OwnerState );
		Assert.Equal( 0f, timers.GetElapsedByHandle( ~2u ) );
	}

	[Fact]
	public void ReusedOwnerResolutionAndTimerComparisonDoNotAllocate()
	{
		var timers = Timers();
		timers.Enter( [1] );
		timers.AdvanceTimers( .5f );
		var condition = new StateTimeCondition( new( ConditionComparisonMode.Greater, .25f ), false,
			static owner => owner == 2 ? ~1u : 0u, static _ => 2u, timers.GetElapsedByHandle );
		for ( var index = 0; index < 1000; index++ )
			condition.Evaluate( 1 );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
			condition.Evaluate( 1 );
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
