using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public sealed class CompiledStateGraphRuntimeTests
{
	private static CompiledStateGraphDefinition Definition() => new(
		[
			new( -1, -1, 1, 2, 0, 1, true, StateSelectionMode.Reselect ),
			new( 0, -1, -1, 0, 1, 1, true, StateSelectionMode.Retain ),
			new( 0, -1, -1, 0, 0, 0, true, StateSelectionMode.Retain )
		], [0, 1], [new( 3, 2, 0 ), new( 1, 1, 0 )], [[3], []],
		[new( 0, 1 ), new( 1, 1 ), new( 2, 1 )],
		[new( 0, true ), new( 1, true ), new( 2, true ), new( -1, false )],
		[[0], [0, 1], [0, 2]] );

	private static CompiledStateBehaviorBinding[] Bindings( List<string> events ) =>
		Enumerable.Range( 0, 4 ).Select( id => new CompiledStateBehaviorBinding(
			() => 100 + id, instance => events.Add( $"enter:{id}:{instance}" ),
			instance => events.Add( $"exit:{id}:{instance}" ), () => events.Add( $"action:{id}" ) ) ).ToArray();

	[Fact]
	public void OneHierarchyDispatchesEntryTransitionAndOrderedUpdatesThroughExplicitBindings()
	{
		var events = new List<string>();
		var transition = false;
		var operations = Enumerable.Range( 0, 3 ).Select( id =>
			new Action<int, int>( ( behavior, instance ) => events.Add( $"update:{id}:{behavior}:{instance}" ) ) ).ToArray();
		var runtime = new CompiledStateGraphRuntime( Definition(), Bindings( events ), _ => { }, _ => { },
			[( _, _ ) => transition], operations, () => events.Add( "invalidate" ), true );
		Assert.Equal( -1, runtime.Update( .5f ) );
		Assert.Equal( new[] { "invalidate", "enter:0:100", "enter:1:101", "update:0:0:100", "update:1:1:101" }, events );
		events.Clear();
		transition = true;
		Assert.Equal( 0, runtime.Update( .25f ) );
		Assert.Equal( 2, runtime.Hierarchy.CurrentState );
		Assert.Equal( 1, runtime.Hierarchy.PreviousState );
		Assert.Equal( new[] { "invalidate", "exit:1:101", "action:3", "enter:2:102", "update:0:0:100", "update:2:2:102" }, events );
		Assert.Equal( 102, runtime.Scheduler.CurrentInstance );
		Assert.True( runtime.Lifecycle.TryGetElapsed( 0, out var elapsed ) );
		Assert.Equal( .25f, elapsed );
		Assert.False( runtime.Lifecycle.TryGetElapsed( 1, out _ ) );
	}

	[Fact]
	public void MissingBindingsAndMismatchedIdentitySpacesFailBeforeAnyCallback()
	{
		var events = new List<string>();
		var definition = Definition();
		var behaviors = Bindings( events );
		Func<int, bool, bool>[] guards = [( _, _ ) => false];
		Action<int, int>[] operations = [( _, _ ) => { }, ( _, _ ) => { }, ( _, _ ) => { }];
		CompiledStateGraphRuntime Create( CompiledStateGraphDefinition data, CompiledStateBehaviorBinding[] bindings,
			Func<int, bool, bool>[] conditions, Action<int, int>[] updates ) =>
			new( data, bindings, _ => { }, _ => { }, conditions, updates, () => events.Add( "invalidate" ), true );
		Assert.Throws<ArgumentException>( () => Create( definition, behaviors, [], operations ) );
		Assert.Throws<ArgumentException>( () => Create( definition, behaviors, guards, operations[..2] ) );
		Assert.Throws<ArgumentException>( () => Create( definition, new CompiledStateBehaviorBinding[4], guards, operations ) );
		Assert.Throws<ArgumentException>( () => Create( definition with { Operations = [[]] }, behaviors, guards, operations ) );
		Assert.Throws<ArgumentException>( () => Create( definition with { TransitionActions = [[4], []] }, behaviors, guards, operations ) );
		Assert.Empty( events );
	}
}
