namespace SkateMotion.Core.Animation;

/// <summary>Runs selection and retained behavior lifecycle in one ordered graph update.</summary>
internal sealed class HierarchicalStateGraph
{
	private readonly StateSelectionRecord[] _states;
	private readonly TransitionSelectionRecord[] _transitions;
	private readonly int[][] _transitionActions;
	private readonly HierarchicalStateSelector _selector;
	private readonly StatePathPlanner _paths;
	private readonly StateBehaviorLifecycle _lifecycle;
	private readonly Action _invalidateConditions;
	private readonly Action<int> _updateState;

	internal int CurrentState { get; private set; } = -1;
	internal int PreviousState { get; private set; } = -1;
	internal float DeltaTime { get; private set; }
	internal bool IsSelecting { get; private set; }

	// The caller retains immutable tables and binds the final state callback to its scheduler.
	internal HierarchicalStateGraph( StateSelectionRecord[] states, int[] transitionIds,
		TransitionSelectionRecord[] transitions, int[][] transitionActions, StateBehaviorLifecycle lifecycle,
		Func<int, int, bool, bool> evaluateGuard, Action invalidateConditions, Action<int> updateState )
	{
		ArgumentNullException.ThrowIfNull( transitionActions );
		ArgumentNullException.ThrowIfNull( lifecycle );
		ArgumentNullException.ThrowIfNull( invalidateConditions );
		ArgumentNullException.ThrowIfNull( updateState );
		_selector = new( states, transitionIds, transitions, evaluateGuard );
		if ( transitionActions.Length != transitions.Length )
			throw new ArgumentException( "Every transition needs an action list.", nameof( transitionActions ) );
		foreach ( var actions in transitionActions )
			ArgumentNullException.ThrowIfNull( actions );
		_states = states;
		_transitions = transitions;
		_transitionActions = transitionActions;
		_lifecycle = lifecycle;
		_invalidateConditions = invalidateConditions;
		_updateState = updateState;
		_paths = new( states );
	}

	internal int Update( float delta )
	{
		_invalidateConditions();
		DeltaTime = delta;
		IsSelecting = true;
		(int State, int Transition) selected;
		try
		{
			selected = _selector.Select( CurrentState );
		}
		finally
		{
			IsSelecting = false;
		}
		_paths.Plan( CurrentState, selected.State, ResetBoundary( selected.Transition ) );
		_lifecycle.AdvanceTimers( delta );
		PreviousState = CurrentState;
		CurrentState = selected.State;
		_lifecycle.Exit( _paths.ExitedStates );
		if ( selected.Transition != -1 )
			_lifecycle.Transition( _transitionActions[ selected.Transition ] );
		_lifecycle.Enter( _paths.EnteredStates );
		if ( _lifecycle.CallbacksEnabled )
			_updateState( CurrentState );
		return selected.Transition;
	}

	private int ResetBoundary( int transition )
	{
		if ( transition == -1 )
			return -1;
		var target = _transitions[ transition ].Target;
		for ( var state = target; state != -1; state = _states[ state ].Parent )
		{
			if ( state == CurrentState )
				return _states[ target ].Parent;
		}
		return target;
	}
}
