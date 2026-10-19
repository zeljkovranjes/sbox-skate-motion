namespace SkateMotion.Core.Animation;

/// <summary>Retains state timers and sorted behavior instances across entry and exit phases.</summary>
internal sealed class StateBehaviorLifecycle
{
	private readonly StateBehaviorRange[] _states;
	private readonly BehaviorLifecycleDefinition[] _definitions;
	private readonly StateBehaviorCallbacks _callbacks;
	private readonly ActiveStateBehavior[] _storage;
	private readonly float[] _elapsed;
	private readonly bool[] _hasTimer;
	private int _count;

	internal bool CallbacksEnabled { get; set; }
	internal ReadOnlySpan<ActiveStateBehavior> Active => _storage.AsSpan( 0, _count );
	internal ReadOnlySpan<ActiveStateBehavior> Storage => _storage;

	// Tables remain immutable. Storage retains unused slots until they are overwritten.
	internal StateBehaviorLifecycle( StateBehaviorRange[] states, BehaviorLifecycleDefinition[] definitions,
		StateBehaviorCallbacks callbacks, bool callbacksEnabled )
	{
		ArgumentNullException.ThrowIfNull( states );
		ArgumentNullException.ThrowIfNull( definitions );
		ArgumentNullException.ThrowIfNull( callbacks.Create );
		ArgumentNullException.ThrowIfNull( callbacks.Retain );
		ArgumentNullException.ThrowIfNull( callbacks.Release );
		ArgumentNullException.ThrowIfNull( callbacks.Enter );
		ArgumentNullException.ThrowIfNull( callbacks.Exit );
		ArgumentNullException.ThrowIfNull( callbacks.Transition );
		foreach ( var range in states )
		{
			if ( range.First < 0 || range.Count < 0 || range.First > definitions.Length - range.Count )
				throw new ArgumentException( "Invalid behavior range.", nameof( states ) );
		}
		foreach ( var definition in definitions )
		{
			if ( definition.OwnerState < -1 || definition.OwnerState >= states.Length )
				throw new ArgumentException( "Invalid behavior owner.", nameof( definitions ) );
		}
		_states = states;
		_definitions = definitions;
		_callbacks = callbacks;
		CallbacksEnabled = callbacksEnabled;
		_storage = new ActiveStateBehavior[ definitions.Length ];
		Array.Fill( _storage, new ActiveStateBehavior( -1, -1 ) );
		_elapsed = new float[ states.Length ];
		_hasTimer = new bool[ states.Length ];
	}

	internal bool TryGetElapsed( int state, out float elapsed )
	{
		ValidateState( state );
		elapsed = _hasTimer[ state ] ? _elapsed[ state ] : 0f;
		return _hasTimer[ state ];
	}

	internal float GetElapsedByHandle( uint stateHandle )
	{
		if ( stateHandle == 0 )
			return -1f;
		var state = (int)(~stateHandle & 0xffff);
		return state < _states.Length && _hasTimer[ state ] ? _elapsed[ state ] : -1f;
	}

	internal void AdvanceTimers( float delta )
	{
		for ( var state = 0; state < _states.Length; state++ )
		{
			if ( _hasTimer[ state ] )
				_elapsed[ state ] += delta;
		}
	}

	internal void Enter( ReadOnlySpan<int> states )
	{
		foreach ( var state in states )
			ValidateState( state );
		foreach ( var state in states )
		{
			_hasTimer[ state ] = true;
			_elapsed[ state ] = 0f;
			var range = _states[ state ];
			for ( var offset = 0; offset < range.Count; offset++ )
			{
				var definition = range.First + offset;
				if ( _definitions[ definition ].Enabled )
					EnterBehavior( definition );
			}
		}
	}

	internal void Exit( ReadOnlySpan<int> states )
	{
		for ( var index = 0; index < states.Length; index++ )
		{
			var state = states[ index ];
			ValidateState( state );
			if ( !_hasTimer[ state ] || Contains( states.Slice( 0, index ), state ) )
				throw new ArgumentException( "Each exited state needs a distinct active timer.", nameof( states ) );
		}
		for ( var index = _count - 1; index >= 0; index-- )
		{
			var behavior = _storage[ index ];
			var owner = _definitions[ behavior.Definition ].OwnerState;
			if ( owner == -1 || !Contains( states, owner ) )
				continue;
			if ( CallbacksEnabled )
				_callbacks.Exit( behavior.Definition, behavior.Instance );
			for ( var destination = index; destination < _count - 1; destination++ )
				Assign( destination, _storage[ destination + 1 ] );
			_count--;
		}
		foreach ( var state in states )
			_hasTimer[ state ] = false;
	}

	internal void Transition( ReadOnlySpan<int> definitions )
	{
		foreach ( var definition in definitions )
		{
			if ( definition < 0 || definition >= _definitions.Length )
				throw new ArgumentOutOfRangeException( nameof( definitions ) );
		}
		foreach ( var definition in definitions )
			_callbacks.Transition( definition );
	}

	// Clearing active definitions preserves timers and every unused retained slot.
	internal void ClearActive()
	{
		for ( var index = _count - 1; index >= 0; index-- )
		{
			var behavior = _storage[ index ];
			if ( CallbacksEnabled )
				_callbacks.Exit( behavior.Definition, behavior.Instance );
			for ( var destination = index; destination < _count - 1; destination++ )
				Assign( destination, _storage[ destination + 1 ] );
			_count--;
		}
	}

	private void EnterBehavior( int definition )
	{
		var instance = _callbacks.Create( definition );
		if ( instance < -1 )
			throw new InvalidOperationException( "A factory must return an instance handle or -1." );
		var slot = 0;
		while ( slot < _count && _storage[ slot ].Definition < definition )
			slot++;
		if ( slot < _count && _storage[ slot ].Definition == definition )
		{
			// Repeated entry uses the first retained instance for the entry callback.
			slot = 0;
		}
		else
		{
			for ( var destination = _count; destination > slot; destination-- )
				Assign( destination, _storage[ destination - 1 ] );
			Assign( slot, new( definition, instance ) );
			_count++;
		}
		if ( CallbacksEnabled )
			_callbacks.Enter( definition, _storage[ slot ].Instance );
		if ( instance != -1 )
			_callbacks.Release( instance );
	}

	private void Assign( int index, ActiveStateBehavior value )
	{
		var previous = _storage[ index ].Instance;
		if ( previous != value.Instance && value.Instance != -1 )
			_callbacks.Retain( value.Instance );
		_storage[ index ] = value;
		if ( previous != value.Instance && previous != -1 )
			_callbacks.Release( previous );
	}

	private static bool Contains( ReadOnlySpan<int> states, int state )
	{
		foreach ( var candidate in states )
		{
			if ( candidate == state )
				return true;
		}
		return false;
	}

	private void ValidateState( int state )
	{
		if ( state < 0 || state >= _states.Length )
			throw new ArgumentOutOfRangeException( nameof( state ) );
	}
}
