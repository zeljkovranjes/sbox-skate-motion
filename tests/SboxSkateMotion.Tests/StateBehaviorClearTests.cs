using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class StateBehaviorClearTests
{
	private static StateBehaviorLifecycle Create( StateBehaviorCallbacks callbacks, bool enabled = true ) =>
		new( [new( 0, 2 ), new( 2, 2 )],
			[new( 0, true ), new( -1, true ), new( 1, true ), new( 1, true )], callbacks, enabled );

	private static StateBehaviorCallbacks Quiet() => new( static id => id, static _ => { },
		static _ => { }, static ( _, _ ) => { }, static ( _, _ ) => { }, static _ => { } );

	[Fact]
	public void ClearExitsEveryDefinitionInReverseIncludingUnownedAndAbsentInstances()
	{
		var events = new List<(int, int)>();
		var lifecycle = Create( Quiet() with
		{
			Create = id => id == 2 ? -1 : id,
			Exit = ( id, instance ) => events.Add( (id, instance) )
		} );
		lifecycle.Enter( [1, 0] );
		lifecycle.ClearActive();
		Assert.Equal( new[] { (3, 3), (2, -1), (1, 1), (0, 0) }, events );
		Assert.Empty( lifecycle.Active.ToArray() );
	}

	[Fact]
	public void ClearRetainsTimersStorageAndReferenceOwnership()
	{
		var references = new int[ 4 ];
		var lifecycle = Create( Quiet() with
		{
			Retain = id => references[ id ]++,
			Release = id => references[ id ]--
		} );
		lifecycle.Enter( [0, 1] );
		lifecycle.Exit( [1] );
		lifecycle.AdvanceTimers( .75f );
		var stored = lifecycle.Storage.ToArray();
		var before = references.ToArray();
		lifecycle.ClearActive();
		Assert.Equal( stored, lifecycle.Storage.ToArray() );
		Assert.Equal( before, references );
		Assert.True( lifecycle.TryGetElapsed( 0, out var elapsed ) );
		Assert.Equal( .75f, elapsed );
		Assert.False( lifecycle.TryGetElapsed( 1, out _ ) );
	}

	[Fact]
	public void RepeatedClearIsEmptyAndReentryResetsOnlyTheEnteredTimer()
	{
		var exits = new List<int>();
		var factories = 0;
		var lifecycle = Create( Quiet() with
		{
			Create = id => { factories++; return id; },
			Exit = ( id, _ ) => exits.Add( id )
		} );
		lifecycle.Enter( [0, 1] );
		lifecycle.AdvanceTimers( .25f );
		lifecycle.ClearActive();
		lifecycle.ClearActive();
		Assert.Equal( new[] { 3, 2, 1, 0 }, exits );
		lifecycle.AdvanceTimers( .5f );
		lifecycle.Enter( [1] );
		Assert.Equal( 6, factories );
		Assert.Equal( new[] { 2, 3 }, lifecycle.Active.ToArray().Select( value => value.Definition ) );
		Assert.True( lifecycle.TryGetElapsed( 0, out var retained ) );
		Assert.Equal( .75f, retained );
		Assert.True( lifecycle.TryGetElapsed( 1, out var entered ) );
		Assert.Equal( 0f, entered );
	}

	[Fact]
	public void DisabledCallbacksStillClearAllActiveDefinitions()
	{
		var lifecycle = Create( Quiet() with { Exit = ( _, _ ) => throw new Exception() }, false );
		lifecycle.Enter( [0, 1] );
		lifecycle.ClearActive();
		Assert.Empty( lifecycle.Active.ToArray() );
		Assert.Equal( 4, lifecycle.Storage.Length );
		Assert.True( lifecycle.TryGetElapsed( 0, out _ ) );
		Assert.True( lifecycle.TryGetElapsed( 1, out _ ) );
	}

	[Fact]
	public void CallbackCanDisableRemainingExitsWithoutPreventingRemoval()
	{
		StateBehaviorLifecycle lifecycle = null!;
		var exits = new List<int>();
		lifecycle = Create( Quiet() with
		{
			Exit = ( id, _ ) =>
			{
				exits.Add( id );
				lifecycle.CallbacksEnabled = false;
			}
		} );
		lifecycle.Enter( [0, 1] );
		lifecycle.ClearActive();
		Assert.Equal( new[] { 3 }, exits );
		Assert.Empty( lifecycle.Active.ToArray() );
	}

	[Fact]
	public void ClearAndReentryDoNotAllocate()
	{
		var lifecycle = Create( Quiet() );
		int[] states = [0, 1];
		for ( var index = 0; index < 1000; index++ )
		{
			lifecycle.Enter( states );
			lifecycle.ClearActive();
		}
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var index = 0; index < 1000; index++ )
		{
			lifecycle.Enter( states );
			lifecycle.ClearActive();
		}
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
