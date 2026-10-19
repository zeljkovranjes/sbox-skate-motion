using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class StateBehaviorLifecycleTests
{
	private static StateBehaviorCallbacks NoCallbacks => new(
		static definition => definition, static _ => { }, static _ => { },
		static ( _, _ ) => { }, static ( _, _ ) => { }, static _ => { } );

	[Fact]
	public void EntryFollowsStateRangesWhileStorageStaysSorted()
	{
		var events = new List<string>();
		var callbacks = NoCallbacks with
		{
			Create = definition => { events.Add( $"create:{definition}" ); return definition; },
			Enter = ( definition, instance ) => events.Add( $"enter:{definition}:{instance}" )
		};
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 2 ), new( 2, 2 )],
			[new( 0, true ), new( 0, true ), new( 1, true ), new( 1, true )], callbacks, true );
		lifecycle.Enter( [1, 0] );
		Assert.Equal( new[] { "create:2", "enter:2:2", "create:3", "enter:3:3",
			"create:0", "enter:0:0", "create:1", "enter:1:1" }, events );
		Assert.Equal( new[] { new ActiveStateBehavior( 0, 0 ), new( 1, 1 ), new( 2, 2 ), new( 3, 3 ) }, lifecycle.Active.ToArray() );
		Assert.True( lifecycle.TryGetElapsed( 0, out var elapsed ) );
		Assert.Equal( 0f, elapsed );
	}

	[Fact]
	public void ExitUsesReverseBehaviorOrderAndPreservesUnownedDefinitions()
	{
		var exits = new List<int>();
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 2 ), new( 2, 2 )],
			[new( 0, true ), new( 0, true ), new( 1, true ), new( -1, true )],
			NoCallbacks with { Exit = ( definition, _ ) => exits.Add( definition ) }, true );
		lifecycle.Enter( [0, 1] );
		lifecycle.Exit( [0, 1] );
		Assert.Equal( new[] { 2, 1, 0 }, exits );
		Assert.Equal( new[] { new ActiveStateBehavior( 3, 3 ) }, lifecycle.Active.ToArray() );
		Assert.False( lifecycle.TryGetElapsed( 0, out _ ) );
		Assert.False( lifecycle.TryGetElapsed( 1, out _ ) );
	}

	[Fact]
	public void DisablingCallbacksStillCreatesInstancesAndRunsExplicitActions()
	{
		var created = new List<int>();
		var actions = new List<int>();
		var callbacks = NoCallbacks with
		{
			Create = definition => { created.Add( definition ); return definition; },
			Enter = ( _, _ ) => throw new Exception( "Entry callback was disabled." ),
			Exit = ( _, _ ) => throw new Exception( "Exit callback was disabled." ),
			Transition = actions.Add
		};
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 2 )], [new( 0, true ), new( 0, false )], callbacks, false );
		lifecycle.Enter( [0] );
		lifecycle.Transition( [1, 0, 1] );
		lifecycle.Exit( [0] );
		Assert.Equal( new[] { 0 }, created );
		Assert.Equal( new[] { 1, 0, 1 }, actions );
		Assert.Empty( lifecycle.Active.ToArray() );
	}

	[Fact]
	public void NullInstancesRemainActiveAndReachCallbacksWithoutReferenceOperations()
	{
		var instances = new List<int>();
		var callbacks = NoCallbacks with
		{
			Create = _ => -1,
			Retain = _ => throw new Exception( "Null instance was retained." ),
			Release = _ => throw new Exception( "Null instance was released." ),
			Enter = ( _, instance ) => instances.Add( instance ),
			Exit = ( _, instance ) => instances.Add( instance )
		};
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 1 )], [new( 0, true )], callbacks, true );
		lifecycle.Enter( [0] );
		Assert.Equal( new ActiveStateBehavior( 0, -1 ), lifecycle.Active[ 0 ] );
		lifecycle.Exit( [0] );
		Assert.Equal( new[] { -1, -1 }, instances );
	}

	[Fact]
	public void RepeatedEntryResetsTimerAndPassesTheFirstStoredInstance()
	{
		var entries = new List<(int, int)>();
		var created = 0;
		var callbacks = NoCallbacks with
		{
			Create = definition => { created++; return definition; },
			Enter = ( definition, instance ) => entries.Add( (definition, instance) )
		};
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 1 ), new( 1, 1 )],
			[new( 0, true ), new( 1, true )], callbacks, true );
		lifecycle.Enter( [0, 1] );
		lifecycle.AdvanceTimers( .5f );
		lifecycle.Enter( [1] );
		Assert.Equal( 3, created );
		Assert.Equal( (1, 0), entries[ 2 ] );
		Assert.Equal( 2, lifecycle.Active.Length );
		Assert.True( lifecycle.TryGetElapsed( 0, out var retained ) );
		Assert.True( lifecycle.TryGetElapsed( 1, out var restarted ) );
		Assert.Equal( .5f, retained );
		Assert.Equal( 0f, restarted );
	}

	[Fact]
	public void RemovedStorageRetainsItsFinalSlotUntilOverwritten()
	{
		var references = new int[ 2 ];
		var callbacks = NoCallbacks with
		{
			Create = definition => { references[ definition ]++; return definition; },
			Retain = instance => references[ instance ]++,
			Release = instance => references[ instance ]--
		};
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 1 ), new( 1, 1 )],
			[new( 0, true ), new( 1, true )], callbacks, true );
		lifecycle.Enter( [0, 1] );
		Assert.Equal( new[] { 1, 1 }, references );
		lifecycle.Exit( [0] );
		Assert.Equal( new[] { 0, 2 }, references );
		lifecycle.Exit( [1] );
		Assert.Equal( new[] { 0, 2 }, references );
		lifecycle.Enter( [0] );
		Assert.Equal( new[] { 1, 1 }, references );
	}

	[Fact]
	public void CallbackEnableChangesAffectSubsequentEntriesInTheSamePhase()
	{
		StateBehaviorLifecycle lifecycle = null!;
		var entered = 0;
		var created = 0;
		var callbacks = NoCallbacks with
		{
			Create = definition => { created++; return definition; },
			Enter = ( _, _ ) => { entered++; lifecycle.CallbacksEnabled = false; }
		};
		lifecycle = new( [new( 0, 2 )], [new( 0, true ), new( 0, true )], callbacks, true );
		lifecycle.Enter( [0] );
		Assert.Equal( 1, entered );
		Assert.Equal( 2, created );
	}

	[Fact]
	public void InvalidExitPathsAreRejectedBeforeStateChanges()
	{
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 1 ), new( 1, 0 )], [new( 0, true )], NoCallbacks, true );
		lifecycle.Enter( [0] );
		Assert.Throws<ArgumentException>( () => lifecycle.Exit( [0, 1] ) );
		Assert.Throws<ArgumentException>( () => lifecycle.Exit( [0, 0] ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => lifecycle.Enter( [0, 2] ) );
		Assert.Equal( new ActiveStateBehavior( 0, 0 ), lifecycle.Active[ 0 ] );
		Assert.True( lifecycle.TryGetElapsed( 0, out _ ) );
		Assert.Throws<ArgumentException>( () => new StateBehaviorLifecycle( [new( 1, 1 )], [new( 0, true )], NoCallbacks, true ) );
	}

	[Fact]
	public void RepeatedPhasesDoNotAllocateAfterConstruction()
	{
		var lifecycle = new StateBehaviorLifecycle( [new( 0, 1 )], [new( 0, true )], NoCallbacks, true );
		var states = new[] { 0 };
		for ( var i = 0; i < 100; i++ )
		{
			lifecycle.Enter( states );
			lifecycle.AdvanceTimers( .125f );
			lifecycle.Exit( states );
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var i = 0; i < 1000; i++ )
		{
			lifecycle.Enter( states );
			lifecycle.AdvanceTimers( .125f );
			lifecycle.Exit( states );
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
