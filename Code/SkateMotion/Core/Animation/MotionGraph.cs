using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

/// <summary>
/// Plays the highest-priority state whose conditions hold, crossfading on every change. A state also restarts
/// when a tag its steps select by changes (for example a different trick), and each entry rotates variants.
/// </summary>
internal sealed class MotionGraph
{
	private readonly ClipSet _clips;
	private readonly MotionCatalog _catalog;
	private readonly AnimationStateDefinition[] _states;
	private readonly AnimationStep[][] _steps;
	private readonly AnimationStep[] _exits;
	private int _pending = -1;
	private float _exitTime;
	private readonly string _transitionTag;
	private readonly AnimationStep _transition;
	private bool _transitioning;
	private string _lastFamily = "";
	private string _lastValue = "";
	private readonly SkeletonPose _from;
	private readonly SkeletonPose _scratch;
	private int _current = -1;
	private int _step;
	private float _time;
	private float _fade;
	private float _fadeDuration;
	private readonly int[] _entries;

	internal MotionGraph( ClipSet clips, AnimationGraphDefinition definition )
	{
		_clips = clips;
		_clips.AddBlends( definition.Blends );
		_catalog = new MotionCatalog( clips, definition.Motions );
		_states = definition.States.ToArray();
		_steps = _states.Select( state => state.Steps.Select( step => new AnimationStep( step ) ).ToArray() ).ToArray();
		_exits = _states.Select( state => state.Exit is null ? null : new AnimationStep( state.Exit ) ).ToArray();
		_transitionTag = definition.TransitionTag;
		if ( !string.IsNullOrEmpty( _transitionTag ) )
		{
			_transition = new AnimationStep( new AnimationStepDefinition
			{
				Select = new() { [ "family" ] = "$family", [ _transitionTag ] = "$transition" },
				Optional = true
			} );
		}
		_entries = new int[ _states.Length ];
		_from = new SkeletonPose( clips.Skeleton.Count );
		_scratch = new SkeletonPose( clips.Skeleton.Count );
	}

	internal ClipSkeleton Skeleton => _clips.Skeleton;
	internal int MotionCount => _catalog.Count;

	internal IReadOnlyList<string> Actions( string family, string style = null ) => _catalog.Actions( family, style );

	/// <summary>Name of the playing state, or null before the first update.</summary>
	internal string State => _current >= 0 ? _states[ _current ].Name : null;

	/// <summary>Name of the clip playing now, including exit motions.</summary>
	internal string ClipName => _pending >= 0 ? (_transitioning ? _transition.ClipName : _exits[ _current ]?.ClipName)
		: _current >= 0 ? _steps[ _current ][ _step ].ClipName : null;

	/// <summary>True once a non-looping state has played all its steps.</summary>
	internal bool Finished { get; private set; }

	/// <summary>Advances the graph and writes the resulting local pose.</summary>
	internal void Update( AnimationParameters parameters, float delta, SkeletonPose output )
	{
		var requested = Choose( parameters, out var change );
		var restart = change == 1;
		var newActivity = requested >= 0 && (requested != _current || change == 2);
		if ( _pending >= 0 )
		{
			// An exit motion is playing; it finishes before the requested state starts unless the new
			// activity is urgent (for example leaving the ground).
			_pending = requested >= 0 ? requested : _pending;
			if ( parameters.Number( "urgent" ) <= 0.5f )
			{
				PlayExit( parameters, delta, output );
				return;
			}
			var next = _pending;
			_pending = -1;
			_transitioning = false;
			Enter( next, output, false );
			newActivity = false;
			restart = false;
		}
		var transition = newActivity && BeginTransition( requested, parameters, output );
		RememberActivity( parameters );
		if ( transition )
		{
			PlayExit( parameters, delta, output );
			return;
		}
		if ( newActivity && parameters.Number( "urgent" ) <= 0.5f && BeginExit( requested, output ) )
		{
			PlayExit( parameters, delta, output );
			return;
		}
		if ( newActivity )
			Enter( requested, output, false );
		else if ( restart )
			Reselect( parameters, output );
		if ( _current < 0 )
		{
			output.SetRest( _clips.Skeleton );
			return;
		}
		if ( _exits[ _current ] is { } exit )
			exit.Resolve( parameters, _catalog );
		Advance( parameters, delta );
		var steps = _steps[ _current ];
		var state = _states[ _current ];
		var looping = state.Loop && _step == steps.Length - 1;
		steps[ _step ].Sample( _clips.Skeleton, parameters, _time, looping, output, _scratch );
		Fade( delta, output );
	}

	// A joining motion such as "walk-stand" when the transition tag changes within the same family.
	private bool BeginTransition( int requested, AnimationParameters parameters, SkeletonPose output )
	{
		if ( _transition is null || _current < 0 || parameters.Number( "urgent" ) > 0.5f )
			return false;
		var family = parameters.Text( "family" );
		var value = parameters.Text( _transitionTag );
		if ( !string.Equals( family, _lastFamily, StringComparison.OrdinalIgnoreCase ) || string.IsNullOrEmpty( _lastValue )
			|| string.Equals( value, _lastValue, StringComparison.OrdinalIgnoreCase ) )
			return false;
		parameters.Set( "transition", _lastValue + "-" + value );
		_transition.Resolve( parameters, _catalog );
		_transition.Choose( 0 );
		if ( !_transition.Playable )
			return false;
		_pending = requested;
		_transitioning = true;
		_exitTime = 0f;
		StartFade( output, _states[ _current ].BlendTime );
		return true;
	}

	private void RememberActivity( AnimationParameters parameters )
	{
		_lastFamily = parameters.Text( "family" );
		_lastValue = string.IsNullOrEmpty( _transitionTag ) ? "" : parameters.Text( _transitionTag );
	}

	private bool BeginExit( int requested, SkeletonPose output )
	{
		if ( _current < 0 || _exits[ _current ] is not { Available: true } exit )
			return false;
		exit.Choose( _entries[ _current ] );
		if ( !exit.Playable )
			return false;
		_pending = requested;
		_transitioning = false;
		_exitTime = 0f;
		StartFade( output, _states[ _current ].BlendTime );
		return true;
	}

	private void PlayExit( AnimationParameters parameters, float delta, SkeletonPose output )
	{
		var exit = _transitioning ? _transition : _exits[ _current ];
		_exitTime += delta;
		if ( _exitTime >= exit.Duration( parameters ) )
		{
			var next = _pending;
			_pending = -1;
			_transitioning = false;
			Enter( next, output, false );
			_steps[ next ][ _step ].Sample( _clips.Skeleton, parameters, 0f, false, output, _scratch );
			Fade( 0f, output );
			return;
		}
		exit.Sample( _clips.Skeleton, parameters, _exitTime, false, output, _scratch );
		Fade( delta, output );
	}

	private void StartFade( SkeletonPose output, float duration )
	{
		_from.CopyFrom( output );
		_fade = 0f;
		_fadeDuration = Math.Max( duration, 0f );
	}

	private void Fade( float delta, SkeletonPose output )
	{
		if ( _fade >= _fadeDuration )
			return;
		_fade += delta;
		var weight = Math.Clamp( _fade / _fadeDuration, 0f, 1f );
		_scratch.CopyFrom( output );
		output.CopyFrom( _from );
		output.BlendToward( _scratch, weight );
	}

	private int Choose( AnimationParameters parameters, out int change )
	{
		change = 0;
		// A locked state plays to its end; its selection is fixed on entry.
		if ( _current >= 0 && _states[ _current ].Lock && !Finished )
			return _current;
		for ( var i = 0; i < _states.Length; i++ )
		{
			if ( !Applies( _states[ i ], parameters ) )
				continue;
			var changed = Resolve( i, parameters );
			if ( !Playable( i ) )
				continue;
			change = i == _current ? changed : 0;
			return i;
		}
		return _current;
	}

	private static bool Applies( AnimationStateDefinition state, AnimationParameters parameters )
	{
		foreach ( var condition in state.When )
		{
			if ( !parameters.Matches( condition ) )
				return false;
		}
		return true;
	}

	private bool Playable( int state )
	{
		foreach ( var step in _steps[ state ] )
		{
			if ( !step.Available )
				return false;
		}
		return _steps[ state ].Length > 0;
	}

	private int Resolve( int state, AnimationParameters parameters )
	{
		var changed = 0;
		foreach ( var step in _steps[ state ] )
			changed = Math.Max( changed, step.Resolve( parameters, _catalog ) );
		return changed;
	}

	private void Enter( int state, SkeletonPose output, bool restart )
	{
		StartFade( output, _current < 0 ? 0f : _states[ state ].BlendTime );
		_current = state;
		_time = 0f;
		Finished = false;
		var variant = _entries[ state ]++;
		foreach ( var step in _steps[ state ] )
			step.Choose( variant );
		_step = restart ? _steps[ state ].Length - 1 : 0;
		while ( _step < _steps[ state ].Length - 1 && !_steps[ state ][ _step ].Playable )
			_step++;
	}

	// A preference changed within the same activity (lean, speed band). Keep the cycle's phase so the motion
	// does not jump back to its start, and crossfade only when the chosen clip really changes.
	private void Reselect( AnimationParameters parameters, SkeletonPose output )
	{
		var steps = _steps[ _current ];
		var last = steps.Length - 1;
		var variant = Math.Max( _entries[ _current ] - 1, 0 );
		if ( _step == last && steps[ last ].Keeps( variant ) )
			return;
		var duration = steps[ _step ].Duration( parameters );
		var phase = duration > 0f ? _time / duration : 0f;
		StartFade( output, _states[ _current ].BlendTime );
		foreach ( var step in steps )
			step.Choose( variant );
		_step = last;
		_time = phase * steps[ last ].Duration( parameters );
		Finished = false;
	}

	private void Advance( AnimationParameters parameters, float delta )
	{
		var state = _states[ _current ];
		var steps = _steps[ _current ];
		var rate = state.Speed * (string.IsNullOrEmpty( state.SpeedParameter ) ? 1f : parameters.Number( state.SpeedParameter ));
		_time += delta * rate;
		while ( true )
		{
			var duration = steps[ _step ].Duration( parameters );
			if ( _time < duration )
				return;
			if ( _step < steps.Length - 1 )
			{
				_time -= duration;
				_step++;
				continue;
			}
			if ( state.Loop )
			{
				_time = duration > 0f ? _time % duration : 0f;
				return;
			}
			_time = duration;
			Finished = true;
			return;
		}
	}
}
