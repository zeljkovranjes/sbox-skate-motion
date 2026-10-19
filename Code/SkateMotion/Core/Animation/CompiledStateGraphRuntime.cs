namespace SkateMotion.Core.Animation;

/// <summary>Connects indexed graph tables to retained behavior and operation bindings.</summary>
internal sealed class CompiledStateGraphRuntime
{
	internal HierarchicalStateGraph Hierarchy { get; }
	internal StateBehaviorLifecycle Lifecycle { get; }
	internal StateBehaviorScheduler Scheduler { get; }

	// Tables and delegate arrays are immutable for this runtime's lifetime. No playback
	// clock is owned here: behavior callbacks feed the caller's existing animation owner.
	internal CompiledStateGraphRuntime( CompiledStateGraphDefinition definition,
		CompiledStateBehaviorBinding[] behaviors, Action<int> retain, Action<int> release,
		Func<int, bool, bool>[] guards, Action<int, int>[] operations,
		Action invalidateConditions, bool callbacksEnabled, int initialInstance = -1 )
	{
		Validate( definition, behaviors, guards, operations );
		ArgumentNullException.ThrowIfNull( retain );
		ArgumentNullException.ThrowIfNull( release );
		ArgumentNullException.ThrowIfNull( invalidateConditions );
		var callbacks = new StateBehaviorCallbacks( id => behaviors[ id ].Create(), retain, release,
			( id, instance ) => behaviors[ id ].Enter( instance ),
			( id, instance ) => behaviors[ id ].Exit( instance ), id => behaviors[ id ].Transition() );
		Lifecycle = new( definition.StateBehaviors, definition.Behaviors, callbacks, callbacksEnabled );
		Scheduler = new( definition.Operations,
			( operation, behavior, instance ) => operations[ operation ]( behavior, instance ), initialInstance );
		Hierarchy = new( definition.States, definition.TransitionIds, definition.Transitions,
			definition.TransitionActions, Lifecycle,
			( guard, state, staying ) => guards[ guard ]( state, staying ), invalidateConditions,
			state => Scheduler.Run( state, Lifecycle.Storage, Lifecycle.CallbacksEnabled ) );
	}

	internal int Update( float delta ) => Hierarchy.Update( delta );

	private static void Validate( CompiledStateGraphDefinition definition,
		CompiledStateBehaviorBinding[] behaviors, Func<int, bool, bool>[] guards,
		Action<int, int>[] operations )
	{
		ArgumentNullException.ThrowIfNull( definition );
		ArgumentNullException.ThrowIfNull( definition.States );
		ArgumentNullException.ThrowIfNull( definition.TransitionIds );
		ArgumentNullException.ThrowIfNull( definition.Transitions );
		ArgumentNullException.ThrowIfNull( definition.TransitionActions );
		ArgumentNullException.ThrowIfNull( definition.StateBehaviors );
		ArgumentNullException.ThrowIfNull( definition.Behaviors );
		ArgumentNullException.ThrowIfNull( definition.Operations );
		ArgumentNullException.ThrowIfNull( behaviors );
		ArgumentNullException.ThrowIfNull( guards );
		ArgumentNullException.ThrowIfNull( operations );
		if ( definition.StateBehaviors.Length != definition.States.Length
			|| definition.Operations.Length != definition.States.Length
			|| definition.TransitionActions.Length != definition.Transitions.Length
			|| behaviors.Length != definition.Behaviors.Length )
			throw new ArgumentException( "Compiled tables and bindings must cover the same identities." );
		foreach ( var behavior in behaviors )
		{
			if ( behavior.Create is null || behavior.Enter is null || behavior.Exit is null || behavior.Transition is null )
				throw new ArgumentException( "Every behavior requires an explicit lifecycle binding.", nameof( behaviors ) );
		}
		foreach ( var state in definition.States )
			ValidateGuard( state.Guard, guards );
		foreach ( var transition in definition.Transitions )
			ValidateGuard( transition.Guard, guards );
		foreach ( var actions in definition.TransitionActions )
		{
			ArgumentNullException.ThrowIfNull( actions );
			foreach ( var action in actions )
			{
				if ( action < 0 || action >= behaviors.Length )
					throw new ArgumentException( "A transition references an unbound behavior." );
			}
		}
		foreach ( var schedule in definition.Operations )
		{
			ArgumentNullException.ThrowIfNull( schedule );
			if ( schedule.Length > behaviors.Length )
				throw new ArgumentException( "A schedule exceeds retained behavior storage." );
			foreach ( var operation in schedule )
			{
				if ( operation < 0 || operation >= operations.Length || operations[ operation ] is null )
					throw new ArgumentException( "Every scheduled operation requires an explicit binding.", nameof( operations ) );
			}
		}
	}

	private static void ValidateGuard( int guard, Func<int, bool, bool>[] guards )
	{
		if ( guard < -1 || (guard >= 0 && (guard >= guards.Length || guards[ guard ] is null)) )
			throw new ArgumentException( "Every referenced guard requires an explicit binding.", nameof( guards ) );
	}
}
