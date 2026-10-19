namespace SkateMotion.Core.Animation;

/// <summary>Partitions state paths into retained, exited and entered ancestors.</summary>
internal sealed class StatePathPlanner
{
	private readonly StateSelectionRecord[] _states;
	private readonly int[] _depths;
	private readonly int[] _previous;
	private readonly int[] _following;
	private int _previousCount;
	private int _followingCount;
	private int _retainedCount;

	internal ReadOnlySpan<int> PreviousPath => _previous.AsSpan( 0, _previousCount );
	internal ReadOnlySpan<int> RetainedStates => _previous.AsSpan( 0, _retainedCount );
	internal ReadOnlySpan<int> ExitedStates => _previous.AsSpan( _retainedCount, _previousCount - _retainedCount );
	internal ReadOnlySpan<int> EnteredStates => _following.AsSpan( _retainedCount, _followingCount - _retainedCount );

	// The caller keeps these state records immutable while the planner is in use.
	internal StatePathPlanner( StateSelectionRecord[] states )
	{
		ArgumentNullException.ThrowIfNull( states );
		if ( states.Length == 0 || states[ 0 ].Parent != -1 )
		{
			throw new ArgumentException( "A graph needs a root state.", nameof( states ) );
		}
		_states = states;
		_depths = new int[ states.Length ];
		_depths[ 0 ] = 1;
		var maximumDepth = 1;
		for ( var index = 1; index < states.Length; index++ )
		{
			var parent = states[ index ].Parent;
			if ( parent < 0 || parent >= index )
			{
				throw new ArgumentException( "Each parent must precede its child.", nameof( states ) );
			}
			_depths[ index ] = _depths[ parent ] + 1;
			maximumDepth = Math.Max( maximumDepth, _depths[ index ] );
		}
		_previous = new int[ maximumDepth ];
		_following = new int[ maximumDepth ];
	}

	// A boundary of -1 retains the entire common prefix; other boundaries compare state IDs.
	internal void Plan( int current, int selected, int resetBoundary = -1 )
	{
		if ( current < -1 || current >= _states.Length )
		{
			throw new ArgumentOutOfRangeException( nameof( current ) );
		}
		if ( selected < 0 || selected >= _states.Length )
		{
			throw new ArgumentOutOfRangeException( nameof( selected ) );
		}
		if ( resetBoundary < -1 || resetBoundary >= _states.Length )
		{
			throw new ArgumentOutOfRangeException( nameof( resetBoundary ) );
		}
		_previousCount = BuildPath( current, _previous );
		_followingCount = BuildPath( selected, _following );
		_retainedCount = 0;
		while ( _retainedCount < _previousCount && _retainedCount < _followingCount
			&& _previous[ _retainedCount ] == _following[ _retainedCount ]
			&& (resetBoundary == -1 || _previous[ _retainedCount ] < resetBoundary) )
		{
			_retainedCount++;
		}
	}

	private int BuildPath( int state, int[] destination )
	{
		var count = state == -1 ? 0 : _depths[ state ];
		for ( var index = count - 1; index >= 0; index-- )
		{
			destination[ index ] = state;
			state = _states[ state ].Parent;
		}
		return count;
	}
}
