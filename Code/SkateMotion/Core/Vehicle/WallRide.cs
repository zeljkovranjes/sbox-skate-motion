namespace SkateMotion.Core.Vehicle;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Wall rides. A board in the air heading for a steep wall at a glancing angle, high enough off the floor, turns its
/// wheels to the wall and gets a small lift (more for a low contact); on the wall it is held against it while it still
/// has the speed to ride along it and is not yet sliding down it, for at most <see cref="RideTime"/>. Then it steps off
/// the wall and turns back upright to land on its wheels. Metres, z up.
/// </summary>
internal sealed class WallRide
{
	private enum Phase { None, Approach, Riding, Leaving }

	private Phase _phase;
	private float _time;
	private Vector3 _normal;

	/// <summary>True while the board is turning to a wall, riding it or turning back from it.</summary>
	internal bool Active => _phase != Phase.None;

	/// <summary>True while the board rides along a wall.</summary>
	internal bool Riding => _phase == Phase.Riding;

	/// <summary>Lowest contact (m above the floor) a wall ride starts at, the slowest travel along the wall (m/s) that
	/// starts or keeps one, how long (seconds) ahead a wall is turned to, and the longest ride (seconds).</summary>
	internal const float MinimumHeight = 0.6f;
	internal const float MinimumAlong = 3f;
	internal const float KeepAlong = 2f;
	internal const float Reach = 0.45f;
	internal const float RideTime = 1.5f;

	/// <summary>Speed (m/s) down the wall at which the ride ends, and the speed (m/s) the board steps off the wall at.</summary>
	internal const float SlideDown = 1f;
	internal const float StepOff = 1f;

	// Acceleration (m/s^2) holding the board to the wall, the fastest the board turns (rad/s) and how quickly (seconds)
	// it follows, and how long (seconds) turning back upright takes.
	private const float Press = 6f;
	private const float MaximumTurn = 20f;
	private const float Response = 0.05f;
	private const float Upright = 0.12f;

	/// <summary>Lift (m/s) given toward a wall met this high (m) above the floor: half a metre per second low down,
	/// nothing from a metre up.</summary>
	internal static float Boost( float height ) => 0.5f * Math.Clamp( 1f - (height - MinimumHeight) / 0.4f, 0f, 1f );

	/// <summary>
	/// In the air: the change of angular velocity turning the board toward the wall (or back upright after it), and a
	/// change of velocity, the lift, given once as the board starts toward a wall.
	/// </summary>
	internal (Vector3 Spin, Vector3 Lift) Air( in BoardBody body, WallSight? wall, float step )
	{
		var lift = Vector3.Zero;
		if ( _phase == Phase.Riding )
			_phase = Phase.Leaving;
		if ( _phase is Phase.None or Phase.Approach )
		{
			if ( wall is { } sight && Approaching( body, sight, out var time ) )
			{
				if ( _phase == Phase.None )
					lift = Vector3.UnitZ * Boost( sight.Height );
				_phase = Phase.Approach;
				_normal = sight.Normal;
				return (Turn( body, sight.Normal, Along( body, sight.Normal ), MathF.Max( time, 0.08f ), step ), lift);
			}
			_phase = Phase.None;
			return (Vector3.Zero, lift);
		}
		// Leaving the wall: turn back upright, facing the way it travels across the ground.
		var travel = new Vector3( body.Velocity.X, body.Velocity.Y, 0f );
		var facing = new Vector3( body.Forward.X, body.Forward.Y, 0f );
		if ( travel.LengthSquared() < 0.01f )
			travel = facing.LengthSquared() > 0.0001f ? facing : Vector3.UnitX;
		travel = Vector3.Normalize( travel );
		if ( Vector3.Dot( travel, facing ) < 0f )
			travel = -travel;
		return (Turn( body, Vector3.UnitZ, travel, Upright, step ), lift);
	}

	/// <summary>
	/// On a surface with this normal: while riding a wall, the acceleration holding the board to it and the change of
	/// angular velocity keeping its wheels flat to it. A surface that is not steep ends the wall ride.
	/// </summary>
	internal (Vector3 Hold, Vector3 Spin, Vector3 Away) OnSurface( in BoardBody body, Vector3 normal, float step )
	{
		if ( MathF.Abs( normal.Z ) >= 0.5f )
		{
			_phase = Phase.None;
			return (Vector3.Zero, Vector3.Zero, Vector3.Zero);
		}
		if ( _phase is Phase.Approach )
		{
			_phase = Phase.Riding;
			_time = 0f;
		}
		if ( _phase != Phase.Riding )
			return (Vector3.Zero, Vector3.Zero, Vector3.Zero);
		_time += step;
		var along = body.Velocity - normal * Vector3.Dot( body.Velocity, normal );
		if ( _time > RideTime || along.Length() < KeepAlong || body.Velocity.Z < -SlideDown )
		{
			_phase = Phase.Leaving;
			return (Vector3.Zero, Vector3.Zero, normal * StepOff);
		}
		_normal = normal;
		return (-normal * Press, Turn( body, normal, Along( body, normal ), Response, step ), Vector3.Zero);
	}

	internal void Reset()
	{
		_phase = Phase.None;
		_time = 0f;
	}

	// Heading for the wall at a glancing angle, high enough, and soon enough; with the seconds until it gets there.
	private static bool Approaching( in BoardBody body, WallSight wall, out float time )
	{
		time = 0f;
		if ( MathF.Abs( wall.Normal.Z ) >= 0.5f || wall.Height < MinimumHeight )
			return false;
		// Riding along the wall means travelling across it, not up or down it (as on a quarterpipe's face).
		var into = -Vector3.Dot( body.Velocity, wall.Normal );
		var along = body.Velocity - wall.Normal * Vector3.Dot( body.Velocity, wall.Normal );
		var across = new Vector3( along.X, along.Y, 0f ).Length();
		if ( into <= 0f || across < MinimumAlong || into > across || MathF.Abs( along.Z ) > across )
			return false;
		time = MathF.Max( 0f, Vector3.Dot( body.Position - wall.Point, wall.Normal ) ) / into;
		return time <= Reach;
	}

	// The travel along the wall, the way the board faces.
	private static Vector3 Along( in BoardBody body, Vector3 normal )
	{
		var along = body.Velocity - normal * Vector3.Dot( body.Velocity, normal );
		if ( along.LengthSquared() < 0.0001f )
			along = body.Forward - normal * Vector3.Dot( body.Forward, normal );
		along = Vector3.Normalize( along );
		return Vector3.Dot( along, body.Forward ) < 0f ? -along : along;
	}

	// Change of angular velocity turning the board to face forward with its up axis along up, over about the given time.
	private static Vector3 Turn( in BoardBody body, Vector3 up, Vector3 forward, float time, float step )
	{
		forward = Vector3.Normalize( forward - up * Vector3.Dot( forward, up ) );
		var left = Vector3.Cross( up, forward );
		var target = Quaternion.CreateFromRotationMatrix( new System.Numerics.Matrix4x4(
			forward.X, forward.Y, forward.Z, 0f,
			left.X, left.Y, left.Z, 0f,
			up.X, up.Y, up.Z, 0f,
			0f, 0f, 0f, 1f ) );
		var error = Quaternion.Normalize( target * Quaternion.Conjugate( body.Rotation ) );
		if ( error.W < 0f )
			error = Quaternion.Negate( error );
		var sine = MathF.Sqrt( MathF.Max( 0f, 1f - error.W * error.W ) );
		var wanted = Vector3.Zero;
		if ( sine > 0.0001f )
		{
			var angle = 2f * MathF.Atan2( sine, error.W );
			var axis = new Vector3( error.X, error.Y, error.Z ) / sine;
			wanted = axis * MathF.Min( angle / MathF.Max( time, 0.01f ), MaximumTurn );
		}
		return (wanted - body.AngularVelocity) * Math.Min( 1f, step / Response );
	}
}
