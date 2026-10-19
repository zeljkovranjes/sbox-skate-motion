namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Spin control in the air: the steering input sets a target spin rate about world up (strongest just after
/// take-off, easing to under half within half a second), and toward the landing the board turns itself to line up
/// with its direction of travel, forwards or backwards whichever is nearer. The deck's spin is moved toward that
/// rate each step; pitch and roll are left to physics.
/// </summary>
internal sealed class AirControl
{
	private float _airTime;
	private float _takeoffHeight;
	private bool _airborne;

	/// <summary>Fastest spin the input asks for, radians per second.</summary>
	internal const float SpinRate = 10.47f;

	/// <summary>Fastest spin overall, including the landing alignment, radians per second.</summary>
	internal const float MaximumRate = 8f;

	// Seconds over which the spin closes on its target rate.
	private const float Response = 0.1f;

	/// <summary>Change of angular velocity (world, radians per second) to apply to the deck this step.</summary>
	internal Vector3 Step( in BoardBody body, float steer, float gravity, float step, float alignRate = MaximumRate )
	{
		if ( !_airborne )
		{
			_airborne = true;
			_airTime = 0f;
			_takeoffHeight = body.Position.Z;
		}
		_airTime += step;
		var input = Math.Clamp( steer, -1f, 1f ) * SpinRate * InputShare( _airTime );
		var align = Math.Clamp( Alignment( body, gravity ), -alignRate, alignRate );
		var target = Math.Clamp( input + align, -MaximumRate, MaximumRate );
		var current = body.AngularVelocity.Z;
		return Vector3.UnitZ * ((target - current) * Math.Min( 1f, step / Response ));
	}

	/// <summary>Forgets the flight, so the next airborne step counts as a new take-off.</summary>
	internal void Land() => _airborne = false;

	// Share of the full spin rate the input gives this long after take-off.
	internal static float InputShare( float airTime ) => 1f - 0.58f * Math.Clamp( airTime / 0.5f, 0f, 1f );

	// Spin that brings the deck in line with its travel by the time it lands.
	private float Alignment( in BoardBody body, float gravity )
	{
		var travel = new Vector3( body.Velocity.X, body.Velocity.Y, 0f );
		var forward = new Vector3( body.Forward.X, body.Forward.Y, 0f );
		if ( travel.LengthSquared() < 0.25f || forward.LengthSquared() < 0.01f )
			return 0f;
		var heading = MathF.Atan2( forward.Y, forward.X );
		var wanted = MathF.Atan2( travel.Y, travel.X );
		var error = Wrap( wanted - heading );
		// Riding backwards is as good a landing as forwards.
		if ( MathF.Abs( error ) > MathF.PI / 2f )
			error = Wrap( error - MathF.PI );
		var vertical = body.Velocity.Z;
		var drop = body.Position.Z - _takeoffHeight;
		var landing = gravity > 0f ? (vertical + MathF.Sqrt( MathF.Max( vertical * vertical + 2f * gravity * drop, 0f ) )) / gravity : 1f;
		return 1.2f * error / MathF.Max( landing - 0.032f, 0.032f );
	}

	private static float Wrap( float angle )
	{
		while ( angle > MathF.PI ) angle -= 2f * MathF.PI;
		while ( angle < -MathF.PI ) angle += 2f * MathF.PI;
		return angle;
	}
}
