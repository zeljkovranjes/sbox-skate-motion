namespace SkateMotion.Core.Animation;

/// <summary>Runs a state's ordered operations over retained behavior storage.</summary>
internal sealed class StateBehaviorScheduler
{
	private readonly int[][] _operations;
	private readonly Action<int, int, int> _update;

	internal int CurrentInstance { get; set; }

	// The caller keeps the operation tables immutable and supplies retained storage,
	// including inactive slots when a schedule refers to them.
	internal StateBehaviorScheduler( int[][] operations, Action<int, int, int> update, int initialInstance )
	{
		ArgumentNullException.ThrowIfNull( operations );
		ArgumentNullException.ThrowIfNull( update );
		foreach ( var state in operations )
		{
			ArgumentNullException.ThrowIfNull( state );
			foreach ( var operation in state )
			{
				if ( operation < 0 )
					throw new ArgumentException( "Operation IDs must be nonnegative.", nameof( operations ) );
			}
		}
		_operations = operations;
		_update = update;
		CurrentInstance = initialInstance;
	}

	internal void Run( int state, ReadOnlySpan<ActiveStateBehavior> storage, bool enabled )
	{
		if ( !enabled )
			return;
		if ( state < 0 || state >= _operations.Length )
			throw new ArgumentOutOfRangeException( nameof( state ) );
		var operations = _operations[ state ];
		if ( storage.Length < operations.Length )
			throw new ArgumentException( "The state schedule exceeds retained storage.", nameof( storage ) );
		for ( var slot = 0; slot < operations.Length; slot++ )
		{
			var behavior = storage[ slot ];
			CurrentInstance = behavior.Instance;
			_update( operations[ slot ], behavior.Definition, behavior.Instance );
		}
	}
}
