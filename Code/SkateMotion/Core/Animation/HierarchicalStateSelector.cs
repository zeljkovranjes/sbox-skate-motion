namespace SkateMotion.Core.Animation;

/// <summary>Selects a leaf and transition without applying state lifecycle callbacks.</summary>
internal sealed class HierarchicalStateSelector
{
	private readonly StateSelectionRecord[] _states;
	private readonly int[] _transitionIds;
	private readonly TransitionSelectionRecord[] _transitions;
	private readonly Func<int, int, bool, bool> _evaluateGuard;

	// The caller owns immutable tables and a retained guard evaluator.
	internal HierarchicalStateSelector( StateSelectionRecord[] states, int[] transitionIds,
		TransitionSelectionRecord[] transitions, Func<int, int, bool, bool> evaluateGuard )
	{
		ArgumentNullException.ThrowIfNull( states );
		ArgumentNullException.ThrowIfNull( transitionIds );
		ArgumentNullException.ThrowIfNull( transitions );
		ArgumentNullException.ThrowIfNull( evaluateGuard );
		_states = states;
		_transitionIds = transitionIds;
		_transitions = transitions;
		_evaluateGuard = evaluateGuard;
		Validate();
	}

	internal (int State, int Transition) Select( int current )
	{
		if ( current < -1 || current >= _states.Length )
		{
			throw new ArgumentOutOfRangeException( nameof( current ) );
		}
		if ( current == -1 )
		{
			return (Choose( 0, current, false ), -1);
		}

		// Priority applies across the active hierarchy, with descendants first within a priority.
		for ( var priority = 3; priority >= 0; priority-- )
		{
			for ( var node = current; node != -1; node = _states[ node ].Parent )
			{
				var state = _states[ node ];
				for ( var offset = 0; offset < state.TransitionCount; offset++ )
				{
					var index = _transitionIds[ state.FirstTransition + offset ];
					var transition = _transitions[ index ];
					if ( transition.Priority != priority || !Guard( transition.Guard, node, false )
						|| !TargetAllowed( current, transition.Target ) )
					{
						continue;
					}
					var selected = Choose( transition.Target, current, false );
					if ( selected != -1 )
					{
						return (selected, index);
					}
				}
			}
		}

		var active = _states[ current ];
		if ( active.Mode == StateSelectionMode.Retain && Stays( current ) )
		{
			return (current, -1);
		}
		if ( active.Mode == StateSelectionMode.ReselectAncestor && Stays( active.RetainedAncestor ) )
		{
			var selected = Choose( active.RetainedAncestor, current, true );
			if ( selected != -1 )
			{
				return (selected, -1);
			}
		}
		return (Choose( 0, current, true ), -1);
	}

	private int Choose( int node, int current, bool staying )
	{
		var state = _states[ node ];
		if ( !Guard( state.Guard, node, staying && IsAncestor( current, node ) ) )
		{
			return -1;
		}
		if ( state.ChildCount == 0 )
		{
			return node;
		}
		for ( var offset = 0; offset < state.ChildCount; offset++ )
		{
			var child = state.FirstChild + offset;
			if ( !_states[ child ].Active )
			{
				continue;
			}
			var selected = Choose( child, current, staying );
			if ( selected != -1 )
			{
				return selected;
			}
		}
		return -1;
	}

	private bool TargetAllowed( int current, int target )
	{
		var first = target;
		for ( ; target != -1; target = _states[ target ].Parent )
		{
			// The explicit target uses entry tests even if it is already active.
			if ( target != first && IsAncestor( current, target ) )
			{
				return Stays( target );
			}
			if ( !Guard( _states[ target ].Guard, target, false ) )
			{
				return false;
			}
		}
		return true;
	}

	private bool Stays( int node )
	{
		for ( ; node != -1; node = _states[ node ].Parent )
		{
			if ( !Guard( _states[ node ].Guard, node, true ) )
			{
				return false;
			}
		}
		return true;
	}

	private bool IsAncestor( int node, int candidate )
	{
		for ( ; node != -1; node = _states[ node ].Parent )
		{
			if ( node == candidate )
			{
				return true;
			}
		}
		return false;
	}

	private bool Guard( int guard, int state, bool staying )
	{
		return guard == -1 || _evaluateGuard( guard, state, staying );
	}

	private void Validate()
	{
		if ( _states.Length == 0 || _states[ 0 ].Parent != -1 )
		{
			throw new ArgumentException( "A graph needs a root state." );
		}
		for ( var index = 0; index < _states.Length; index++ )
		{
			var state = _states[ index ];
			if ( state.Parent < -1 || (index > 0 && (state.Parent < 0 || state.Parent >= index))
				|| state.Guard < -1 || state.Mode < StateSelectionMode.Retain
				|| state.Mode > StateSelectionMode.ReselectAncestor || state.ChildCount < 0
				|| state.TransitionCount < 0 || state.FirstTransition < 0
				|| state.FirstTransition > _transitionIds.Length - state.TransitionCount )
			{
				throw new ArgumentException( "Invalid state record." );
			}
			if ( state.ChildCount > 0 && (state.FirstChild <= index
				|| state.FirstChild > _states.Length - state.ChildCount) )
			{
				throw new ArgumentException( "Invalid child range." );
			}
			for ( var offset = 0; offset < state.ChildCount; offset++ )
			{
				if ( _states[ state.FirstChild + offset ].Parent != index )
				{
					throw new ArgumentException( "Child range disagrees with parent links." );
				}
			}
			if ( index > 0 )
			{
				var parent = _states[ state.Parent ];
				if ( index < parent.FirstChild || index - parent.FirstChild >= parent.ChildCount )
				{
					throw new ArgumentException( "State is absent from its parent's child range." );
				}
			}
			if ( state.Mode == StateSelectionMode.ReselectAncestor && (state.RetainedAncestor < 0
				|| !IsAncestor( index, state.RetainedAncestor )) )
			{
				throw new ArgumentException( "Retained ancestor must belong to the active path." );
			}
		}
		foreach ( var transition in _transitions )
		{
			if ( transition.Priority < 0 || transition.Priority > 3 || transition.Target < 0
				|| transition.Target >= _states.Length || transition.Guard < -1 )
			{
				throw new ArgumentException( "Invalid transition record." );
			}
		}
		foreach ( var index in _transitionIds )
		{
			if ( index < 0 || index >= _transitions.Length )
			{
				throw new ArgumentException( "Invalid transition reference." );
			}
		}
	}
}
