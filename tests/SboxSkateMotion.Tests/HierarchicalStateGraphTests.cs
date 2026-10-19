using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class HierarchicalStateGraphTests
{
	private static StateSelectionRecord[] Tree( int guard = -1 ) =>
	[
		new( -1, guard, 1, 2, 0, 0, true, StateSelectionMode.Reselect ),
		new( 0, -1, 3, 1, 0, 0, true, StateSelectionMode.Reselect ),
		new( 0, -1, 4, 1, 0, 0, true, StateSelectionMode.Reselect ),
		new( 1, -1, -1, 0, 0, 5, true, StateSelectionMode.Retain ),
		new( 2, -1, -1, 0, 0, 5, true, StateSelectionMode.Retain )
	];

	private static StateBehaviorLifecycle Lifecycle( StateBehaviorCallbacks callbacks, bool enabled = true ) =>
		new( [new( 0, 1 ), new( 1, 1 ), new( 2, 1 ), new( 3, 1 ), new( 4, 1 )],
			[new( 0, true ), new( 1, true ), new( 2, true ), new( 3, true ), new( 4, true )], callbacks, enabled );

	private static StateBehaviorCallbacks Quiet() => new( static id => id, static _ => { },
		static _ => { }, static ( _, _ ) => { }, static ( _, _ ) => { }, static _ => { } );

	private static HierarchicalStateGraph Graph( StateBehaviorLifecycle lifecycle,
		Func<int, int, bool, bool> guard, Action invalidate, Action<int> update, int rootGuard = -1 ) =>
		new( Tree( rootGuard ), [0, 1, 2, 3, 4],
			[new( 3, 0, 0 ), new( 3, 1, 1 ), new( 3, 2, 2 ), new( 3, 3, 3 ), new( 3, 4, 4 )],
			[[0], [1], [2], [3], [4]], lifecycle, guard, invalidate, update );

	[Fact]
	public void UpdatePublishesFieldsAndAdvancesTimersBeforeLifecycleCallbacks()
	{
		HierarchicalStateGraph graph = null!;
		StateBehaviorLifecycle lifecycle = null!;
		var events = new List<string>();
		var target = -1;
		var recording = false;
		void Observe( string phase, int definition )
		{
			if ( !recording )
				return;
			Assert.Equal( 4, graph.CurrentState );
			Assert.Equal( 3, graph.PreviousState );
			Assert.Equal( .25f, graph.DeltaTime );
			Assert.False( graph.IsSelecting );
			Assert.True( lifecycle.TryGetElapsed( 0, out var elapsed ) );
			Assert.Equal( .25f, elapsed );
			events.Add( $"{phase}:{definition}" );
		}
		lifecycle = Lifecycle( new( id => { Observe( "factory", id ); return id; },
			_ => { }, _ => { }, ( id, _ ) =>
			{
				Observe( "enter", id );
				Assert.True( lifecycle.TryGetElapsed( id, out var elapsed ) );
				Assert.Equal( 0f, elapsed );
			}, ( id, _ ) =>
			{
				Observe( "exit", id );
				Assert.True( lifecycle.TryGetElapsed( id, out var elapsed ) );
				Assert.Equal( .25f, elapsed );
			}, id => Observe( "action", id ) ) );
		graph = Graph( lifecycle, ( id, _, _ ) =>
		{
			Assert.True( graph.IsSelecting );
			Assert.Equal( .25f, graph.DeltaTime );
			Assert.Equal( 3, graph.CurrentState );
			return id == target;
		}, () =>
		{
			Assert.False( graph.IsSelecting );
			if ( recording )
			{
				Assert.Equal( .5f, graph.DeltaTime );
				events.Add( "invalidate" );
			}
		}, state => Observe( "update", state ) );
		Assert.Equal( -1, graph.Update( .5f ) );
		recording = true;
		target = 4;
		Assert.Equal( 4, graph.Update( .25f ) );
		Assert.Equal( new[] { "invalidate", "exit:3", "exit:1", "action:4", "factory:2",
			"enter:2", "factory:4", "enter:4", "update:4" }, events );
		Assert.False( lifecycle.TryGetElapsed( 1, out _ ) );
		Assert.False( lifecycle.TryGetElapsed( 3, out _ ) );
	}

	[Fact]
	public void RetainedSelectionAccumulatesTimersWithoutReentering()
	{
		var factories = 0;
		var callbacks = Quiet() with { Create = id => { factories++; return id; } };
		var lifecycle = Lifecycle( callbacks );
		var graph = Graph( lifecycle, static ( _, _, _ ) => false, static () => { }, static _ => { } );
		graph.Update( 1f );
		graph.Update( .25f );
		graph.Update( .5f );
		Assert.Equal( 3, factories );
		Assert.Equal( 3, graph.CurrentState );
		Assert.Equal( 3, graph.PreviousState );
		foreach ( var state in new[] { 0, 1, 3 } )
		{
			Assert.True( lifecycle.TryGetElapsed( state, out var elapsed ) );
			Assert.Equal( .75f, elapsed );
		}
	}

	[Theory]
	[InlineData( 3, 1 )]
	[InlineData( 1, 1 )]
	[InlineData( 0, 0 )]
	public void ExplicitSelfAndAncestorTargetsResetTheRequiredPath( int target, int firstReset )
	{
		var entered = new List<int>();
		var lifecycle = Lifecycle( Quiet() with { Enter = ( id, _ ) => entered.Add( id ) } );
		var graph = Graph( lifecycle, ( id, _, _ ) => id == target, static () => { }, static _ => { } );
		graph.Update( .5f );
		entered.Clear();
		graph.Update( .25f );
		Assert.Equal( firstReset == 0 ? new[] { 0, 1, 3 } : new[] { 1, 3 }, entered );
		Assert.True( lifecycle.TryGetElapsed( 0, out var rootTime ) );
		Assert.Equal( firstReset == 0 ? 0f : .25f, rootTime );
	}

	[Fact]
	public void DisabledCallbacksStillSelectCreateAndRunTransitionActions()
	{
		var factories = 0;
		var actions = new List<int>();
		var callbacks = new StateBehaviorCallbacks( id => { factories++; return id; }, _ => { }, _ => { },
			( _, _ ) => throw new Exception(), ( _, _ ) => throw new Exception(), actions.Add );
		var lifecycle = Lifecycle( callbacks, false );
		var graph = Graph( lifecycle, static ( id, _, _ ) => id == 4, static () => { }, _ => throw new Exception() );
		graph.Update( .1f );
		graph.Update( .2f );
		Assert.Equal( 4, graph.CurrentState );
		Assert.Equal( 5, factories );
		Assert.Equal( new[] { 4 }, actions );
		Assert.Equal( new[] { 0, 2, 4 }, lifecycle.Active.ToArray().Select( value => value.Definition ) );
	}

	[Fact]
	public void TransitionCanEnableEntryAndFinalCallbacksWithinTheSameUpdate()
	{
		StateBehaviorLifecycle lifecycle = null!;
		var events = new List<int>();
		lifecycle = Lifecycle( Quiet() with
		{
			Transition = _ => lifecycle.CallbacksEnabled = true,
			Enter = ( id, _ ) => events.Add( id )
		}, false );
		var graph = Graph( lifecycle, static ( id, _, _ ) => id == 4, static () => { }, state => events.Add( 10 + state ) );
		graph.Update( 0f );
		graph.Update( 0f );
		Assert.Equal( new[] { 2, 4, 14 }, events );
	}

	[Fact]
	public void FinalSchedulerCanReadRetainedStorageBeyondTheActivePrefix()
	{
		var lifecycle = Lifecycle( Quiet() );
		lifecycle.Enter( [0, 1, 3, 2, 4] );
		lifecycle.Exit( [0, 1, 3, 2, 4] );
		Assert.Empty( lifecycle.Active.ToArray() );
		var observed = new List<(int, int)>();
		var scheduler = new StateBehaviorScheduler( [[], [], [], [0, 1, 2, 3, 4], []],
			( _, definition, instance ) => observed.Add( (definition, instance) ), -1 );
		var graph = Graph( lifecycle, static ( _, _, _ ) => false, static () => { },
			state => scheduler.Run( state, lifecycle.Storage, true ) );
		graph.Update( 0f );
		Assert.Equal( 3, lifecycle.Active.Length );
		Assert.Equal( new[] { (0, 0), (1, 1), (3, 3), (3, 3), (4, 4) }, observed );
		Assert.Equal( 4, scheduler.CurrentInstance );
	}

	[Fact]
	public void FailedSelectionClearsTheFlagWithoutPublishingAStateOrCreatingBehaviors()
	{
		var lifecycle = Lifecycle( Quiet() with { Create = _ => throw new Exception() } );
		var shouldThrow = false;
		var graph = Graph( lifecycle, ( _, _, _ ) => shouldThrow ? throw new InvalidOperationException() : false,
			static () => { }, _ => throw new Exception(), 8 );
		Assert.Throws<ArgumentOutOfRangeException>( () => graph.Update( .25f ) );
		Assert.False( graph.IsSelecting );
		Assert.Equal( -1, graph.CurrentState );
		Assert.Equal( -1, graph.PreviousState );
		Assert.Equal( .25f, graph.DeltaTime );
		shouldThrow = true;
		Assert.Throws<InvalidOperationException>( () => graph.Update( .5f ) );
		Assert.False( graph.IsSelecting );
		Assert.Empty( lifecycle.Active.ToArray() );
	}

	[Fact]
	public void ReusedGraphDoesNotAllocateDuringTransitions()
	{
		var target = 4;
		var lifecycle = Lifecycle( Quiet() );
		var graph = Graph( lifecycle, ( id, _, _ ) => id == target, static () => { }, static _ => { } );
		for ( var index = 0; index < 1000; index++ )
		{
			target = 3 + (index & 1);
			graph.Update( .125f );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
		{
			target = 3 + (index & 1);
			graph.Update( .125f );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
