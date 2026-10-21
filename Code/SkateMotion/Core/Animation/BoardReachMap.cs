namespace SkateMotion.Core.Animation;

/// <summary>
/// Where the pickup's reach clips hold the board, against the yaw each is blended at: measured from the clips' own board
/// bone as each reach catches the board (bearings in degrees from the rider's facing, left positive, as seen with the
/// clips unmirrored). Turns a board's bearing into the yaw whose blend reaches exactly there, and, for a bearing no reach
/// covers (one side behind the rider has no clip), the turn that brings the board to the nearest reach that does.
/// </summary>
internal sealed class BoardReachMap
{
	private readonly float[] _yaws;
	private readonly float[] _bearings;

	/// <param name="yaws">Blend yaws of the measured clips, rising.</param>
	/// <param name="bearings">The bearing each clip holds the board at; they must fall as the yaw rises (the reaches sweep
	/// from the rider's left round the front to behind on the right).</param>
	internal BoardReachMap( float[] yaws, float[] bearings )
	{
		ArgumentNullException.ThrowIfNull( yaws );
		ArgumentNullException.ThrowIfNull( bearings );
		if ( yaws.Length != bearings.Length || yaws.Length < 2 )
			throw new ArgumentException( "A reach map needs at least two measured reaches, one bearing per yaw." );
		_yaws = new float[ yaws.Length ];
		Array.Copy( yaws, _yaws, yaws.Length );
		_bearings = new float[ bearings.Length ];
		// Bearings unwrap into one sweep: each follows on from the one before by less than half a turn.
		for ( var i = 0; i < bearings.Length; i++ )
		{
			var bearing = bearings[ i ];
			if ( i > 0 )
			{
				while ( bearing > _bearings[ i - 1 ] + 180f )
					bearing -= 360f;
				while ( bearing <= _bearings[ i - 1 ] - 180f )
					bearing += 360f;
			}
			_bearings[ i ] = bearing;
			if ( i > 0 && (yaws[ i ] < yaws[ i - 1 ] || bearing > _bearings[ i - 1 ]) )
				throw new ArgumentException( "Reach bearings must fall as the yaw rises." );
		}
		if ( _bearings[ 0 ] - _bearings[ ^1 ] >= 360f )
			throw new ArgumentException( "Reaches cannot cover more than a full turn." );
	}

	/// <summary>Furthest bearing to the left a reach covers.</summary>
	internal float Left => _bearings[ 0 ];

	/// <summary>Furthest bearing to the right (unwrapped, so possibly below -180) a reach covers.</summary>
	internal float Right => _bearings[ ^1 ];

	/// <summary>
	/// The yaw to blend the reach at for a board at <paramref name="bearing"/>, and how far the rider must turn (degrees,
	/// left positive) for a reach to cover it: zero where one does. A board in the gap is brought to the nearer edge, or
	/// to the edge in <paramref name="side"/> when it is nonzero (positive: the left edge), so a turn under way keeps its
	/// way while the board's bearing passes the middle of the gap.
	/// </summary>
	internal (float Yaw, float Turn) Solve( float bearing, int side = 0 )
	{
		if ( !float.IsFinite( bearing ) )
			return (_yaws[ 0 ], 0f);
		// Into the window that starts at the right-hand edge and goes once round.
		while ( bearing < Right )
			bearing += 360f;
		while ( bearing >= Right + 360f )
			bearing -= 360f;
		if ( bearing > Left )
		{
			var toLeft = bearing - Left;
			var toRight = Right + 360f - bearing;
			var left = side > 0 || (side == 0 && toLeft <= toRight);
			return left ? (_yaws[ 0 ], toLeft) : (_yaws[ ^1 ], -toRight);
		}
		for ( var i = 0; i < _bearings.Length - 1; i++ )
		{
			var from = _bearings[ i ];
			var to = _bearings[ i + 1 ];
			if ( bearing > from || bearing < to )
				continue;
			var span = from - to;
			var t = span > 0f ? (from - bearing) / span : 0f;
			return (_yaws[ i ] + (_yaws[ i + 1 ] - _yaws[ i ]) * t, 0f);
		}
		return (_yaws[ ^1 ], 0f);
	}
}
