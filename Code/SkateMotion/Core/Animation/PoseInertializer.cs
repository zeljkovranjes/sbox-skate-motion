namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Keeps every bone of a pose moving continuously. A bone follows the pose it is given exactly while that pose's motion
/// changes speed by no more than a budget a second; a sharper change of speed (a clip handing over to the next, a key's
/// corner shown at a high frame rate, a layer switching in, a pose jumping in a frame) is taken up into an offset from
/// the given pose, and the shown bone's speed changes by no more than the budget allows in any frame while the offset
/// goes back to nothing: as fast as its half-life asks, no faster than a set speed, and slow enough near the pose to
/// stop on it without passing it. A bone the pose stops dead so carries on a little and settles back, never crossing to
/// the pose's other side; a pose that jumps is caught up with smoothly; every bone is back on the pose within a few
/// half-lives.
/// A single frame's move past the jump limits (a looping clip's root coming back round, a body put somewhere else) is
/// not a snap of the motion but a new start, and is followed at once.
/// Works on the local transforms of any skeleton (positions in its own units, rotations against the parent).
/// </summary>
internal sealed class PoseInertializer
{
	/// <summary>Seconds in which an offset near the pose halves.</summary>
	internal const float HalfLife = 0.04f;

	/// <summary>
	/// Sharpest change of a bone's turning speed shown, radians per second per second: a board caught out of a flip
	/// slows about this hard, and a bone stopped at this rate from 20 rad/s carries on about 15 degrees.
	/// </summary>
	internal const float TurnBudget = 600f;

	/// <summary>Sharpest change of a bone's speed shown, units per second per second (a board's pop is about 100).</summary>
	internal const float MoveBudget = 150f;

	/// <summary>Fastest an offset turns a bone as it dies away, radians per second (about 460 degrees).</summary>
	internal const float FastestTurn = 8f;

	/// <summary>Fastest an offset moves a bone as it dies away, units per second.</summary>
	internal const float FastestMove = 1.5f;

	/// <summary>A turn in one frame past this (radians) starts the bone again rather than easing.</summary>
	internal const float TurnJump = 2.1f;

	/// <summary>A move in one frame past this (units) starts the bone again rather than easing.</summary>
	internal const float MoveJump = 0.3f;

	// The longest frame eased; a longer one (a hitch, a pause) starts every bone again.
	private const float LongestStep = 0.2f;

	private Vector3[] _given;
	private Vector3[] _givenRate;
	private Vector3[] _offset;
	private Vector3[] _offsetRate;
	private Quaternion[] _givenTurn;
	private Vector3[] _givenTurnRate;
	private Vector3[] _turnOffset;
	private Vector3[] _turnOffsetRate;
	private int _seen;

	/// <summary>Forgets the motion followed so far: the next pose is followed as given.</summary>
	internal void Reset() => _seen = 0;

	/// <summary>
	/// Carries what has been followed of these bones into a new frame: their transforms are against a parent frame (the
	/// whole model's, for a skeleton's roots) that has just turned by <paramref name="turn"/> and moved by
	/// <paramref name="shift"/>, a point given in the old frame being <c>turn * point + shift</c> in the new. Their places,
	/// turns, speeds and offsets are taken across, so a pose given in the new frame is followed on from where it was shown
	/// rather than eased back to where the old frame had it.
	/// </summary>
	internal void Reframe( ReadOnlySpan<int> bones, Quaternion turn, Vector3 shift )
	{
		if ( _given is null || _seen == 0 )
			return;
		turn = Quaternion.Normalize( turn );
		foreach ( var bone in bones )
		{
			if ( bone < 0 || bone >= _given.Length )
				continue;
			_given[ bone ] = Vector3.Transform( _given[ bone ], turn ) + shift;
			_givenRate[ bone ] = Vector3.Transform( _givenRate[ bone ], turn );
			_offset[ bone ] = Vector3.Transform( _offset[ bone ], turn );
			_offsetRate[ bone ] = Vector3.Transform( _offsetRate[ bone ], turn );
			// A turn's offset and speeds are rotation vectors in the parent's frame: turned with it.
			_givenTurn[ bone ] = Quaternion.Normalize( turn * _givenTurn[ bone ] );
			_givenTurnRate[ bone ] = Vector3.Transform( _givenTurnRate[ bone ], turn );
			_turnOffset[ bone ] = Vector3.Transform( _turnOffset[ bone ], turn );
			_turnOffsetRate[ bone ] = Vector3.Transform( _turnOffsetRate[ bone ], turn );
		}
	}

	/// <summary>The largest offset any bone is turned by this frame, radians.</summary>
	internal float LargestTurn { get; private set; }

	/// <summary>Follows <paramref name="pose"/> <paramref name="delta"/> seconds after the last, writing the shown pose into it.</summary>
	internal void Apply( SkeletonPose pose, float delta )
	{
		var count = pose.Positions.Length;
		if ( _given is null || _given.Length != count )
			Allocate( count );
		if ( _seen > 0 && !(delta > 0f) )
		{
			Show( pose );
			return;
		}
		if ( _seen == 0 || delta > LongestStep )
		{
			Start( pose );
			return;
		}
		var y = Damping( HalfLife );
		var turnStep = TurnBudget * delta;
		var moveStep = MoveBudget * delta;
		var largest = 0f;
		for ( var bone = 0; bone < count; bone++ )
		{
			var position = pose.Positions[ bone ];
			var rotation = Quaternion.Normalize( pose.Rotations[ bone ] );
			if ( !Finite( position ) || !Finite( rotation ) )
				continue;
			// Moves.
			// A jump past the limit starts the bone again, keeping the speed it had (a looping root carries on round).
			var moved = position - _given[ bone ];
			if ( moved.Length() > MoveJump )
			{
				_offset[ bone ] = Vector3.Zero;
				_offsetRate[ bone ] = Vector3.Zero;
			}
			else
			{
				var rate = moved / delta;
				if ( _seen > 1 )
					Follow( ref _offset[ bone ], ref _offsetRate[ bone ], rate - _givenRate[ bone ], moveStep, MoveBudget, FastestMove, y, delta );
				_givenRate[ bone ] = rate;
			}
			_given[ bone ] = position;
			// Turns, as rotation vectors in the parent's frame.
			var turned = Log( rotation * Quaternion.Conjugate( _givenTurn[ bone ] ) );
			if ( turned.Length() > TurnJump )
			{
				_turnOffset[ bone ] = Vector3.Zero;
				_turnOffsetRate[ bone ] = Vector3.Zero;
			}
			else
			{
				var rate = turned / delta;
				if ( _seen > 1 )
				{
					// The shown bone turns on from where it was shown at its new rate, and the offset is what then stands
					// between it and the given turn. Added as rotation vectors instead, an offset easing over one turn and a
					// given turn about another axis composed wrongly: a grab's forearm turned 103 degrees in a frame as the
					// other hand's grab joined it, with an offset still out from the first, and the shown arm jumped 29.
					var shown = Quaternion.Normalize( Exp( _turnOffset[ bone ] ) * _givenTurn[ bone ] );
					var offset = _turnOffset[ bone ];
					Follow( ref offset, ref _turnOffsetRate[ bone ], rate - _givenTurnRate[ bone ], turnStep, TurnBudget, FastestTurn, y, delta );
					shown = Quaternion.Normalize( Exp( (rate + _turnOffsetRate[ bone ]) * delta ) * shown );
					_turnOffset[ bone ] = offset == Vector3.Zero && _turnOffsetRate[ bone ] == Vector3.Zero
						? Vector3.Zero
						: Log( shown * Quaternion.Conjugate( rotation ) );
				}
				_givenTurnRate[ bone ] = rate;
			}
			_givenTurn[ bone ] = rotation;
			largest = MathF.Max( largest, _turnOffset[ bone ].Length() );
		}
		_seen++;
		LargestTurn = largest;
		Show( pose );
	}

	private void Allocate( int count )
	{
		_given = new Vector3[ count ];
		_givenRate = new Vector3[ count ];
		_offset = new Vector3[ count ];
		_offsetRate = new Vector3[ count ];
		_givenTurn = new Quaternion[ count ];
		_givenTurnRate = new Vector3[ count ];
		_turnOffset = new Vector3[ count ];
		_turnOffsetRate = new Vector3[ count ];
		_seen = 0;
	}

	private void Start( SkeletonPose pose )
	{
		for ( var bone = 0; bone < _given.Length; bone++ )
		{
			_given[ bone ] = pose.Positions[ bone ];
			_givenTurn[ bone ] = Quaternion.Normalize( pose.Rotations[ bone ] );
			_givenRate[ bone ] = _offset[ bone ] = _offsetRate[ bone ] = Vector3.Zero;
			_givenTurnRate[ bone ] = _turnOffset[ bone ] = _turnOffsetRate[ bone ] = Vector3.Zero;
		}
		_seen = 1;
		LargestTurn = 0f;
	}

	// Writes the given pose with each bone's offset into the pose.
	private void Show( SkeletonPose pose )
	{
		for ( var bone = 0; bone < _given.Length; bone++ )
		{
			if ( _offset[ bone ] != Vector3.Zero )
				pose.Positions[ bone ] = _given[ bone ] + _offset[ bone ];
			if ( _turnOffset[ bone ] != Vector3.Zero )
				pose.Rotations[ bone ] = Quaternion.Normalize( Exp( _turnOffset[ bone ] ) * _givenTurn[ bone ] );
		}
	}

	// One frame of an offset (its rate is the shown speed less the given one). The given pose's speed changed by
	// givenChange; the offset heads back to nothing at the rate it wants, and the shown speed (the given one plus the
	// offset's) changes by no more than allowed: whatever of the given change may not show, the offset takes up.
	private static void Follow( ref Vector3 offset, ref Vector3 rate, Vector3 givenChange, float allowed, float budget,
		float fastest, float y, float delta )
	{
		var size = offset.Length();
		// Back toward the pose: in proportion near it, no faster than the fastest, and no faster than the budget can stop
		// from in the distance left (the square root of twice the budget times the distance), so it arrives without passing.
		var wanted = size > 1e-7f
			? offset * -MathF.Min( MathF.Min( y, fastest / size ), MathF.Sqrt( 2f * budget / size ) )
			: Vector3.Zero;
		var change = givenChange + (wanted - rate);
		var length = change.Length();
		if ( length > allowed && float.IsFinite( length ) )
			change *= allowed / length;
		rate += change - givenChange;
		offset += rate * delta;
		if ( offset.LengthSquared() < 1e-14f && rate.LengthSquared() < 1e-10f )
			offset = rate = Vector3.Zero;
	}

	// The rate an offset near the pose shrinks at for it to halve in the given time (first order: e^(-y t)).
	private static float Damping( float halfLife ) => MathF.Log( 2f ) / halfLife;

	// Rotation vector (axis times angle, radians) of the shorter way round.
	private static Vector3 Log( Quaternion rotation )
	{
		if ( rotation.W < 0f )
			rotation = Quaternion.Negate( rotation );
		var axis = new Vector3( rotation.X, rotation.Y, rotation.Z );
		var sine = axis.Length();
		return sine < 1e-7f ? axis * 2f : axis * (2f * MathF.Atan2( sine, rotation.W ) / sine);
	}

	private static Quaternion Exp( Vector3 turn )
	{
		var angle = turn.Length();
		return angle < 1e-7f ? Quaternion.Identity : Quaternion.CreateFromAxisAngle( turn / angle, angle );
	}

	private static bool Finite( Vector3 value ) => float.IsFinite( value.X ) && float.IsFinite( value.Y ) && float.IsFinite( value.Z );

	private static bool Finite( Quaternion value ) =>
		float.IsFinite( value.X ) && float.IsFinite( value.Y ) && float.IsFinite( value.Z ) && float.IsFinite( value.W );
}
