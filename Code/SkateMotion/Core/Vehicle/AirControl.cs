namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Spin control in the air: the steering input sets a target spin rate about world up (strongest just after
/// take-off, easing to under half within half a second), and toward the landing the board turns itself to line up
/// with its direction of travel, forwards or backwards whichever is nearer. Coming down on a rail, a board turned
/// nearer across the rail than along its travel lines up across the rail instead, so a turn in the air lands a
/// slide. The deck's spin is moved toward that rate each step; pitch and roll are left to physics.
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

	/// <summary>Turn off the line of travel, in radians, past which a board coming down on a rail lines up across it
	/// for a slide instead of along it for a grind.</summary>
	internal const float SlideTurn = 0.52f;

	/// <summary>Seconds of its present turn rate a turning board is taken to carry on for, in choosing between a slide
	/// and a grind.</summary>
	internal const float TurnLookahead = 0.1f;

	/// <summary>The heading (radians about world up) a board turning at <paramref name="spin"/> is taken to come down at,
	/// from its <paramref name="heading"/> now.</summary>
	internal static float LandingHeading( float heading, float spin ) => heading + spin * TurnLookahead;

	/// <summary>Change of angular velocity (world, radians per second) to apply to the deck this step.</summary>
	/// <param name="rail">Direction of the rail the board is coming down on, or null.</param>
	internal Vector3 Step( in BoardBody body, float steer, float gravity, float step, float alignRate = MaximumRate,
		Vector3? rail = null )
	{
		if ( !_airborne )
		{
			_airborne = true;
			_airTime = 0f;
			_takeoffHeight = body.Position.Z;
		}
		_airTime += step;
		var input = Math.Clamp( steer, -1f, 1f ) * SpinRate * InputShare( _airTime );
		var align = Math.Clamp( Alignment( body, gravity, rail ), -alignRate, alignRate );
		var target = Math.Clamp( input + align, -MaximumRate, MaximumRate );
		var current = body.AngularVelocity.Z;
		return Vector3.UnitZ * ((target - current) * Math.Min( 1f, step / Response ));
	}

	/// <summary>Forgets the flight, so the next airborne step counts as a new take-off.</summary>
	internal void Land() => _airborne = false;

	// Share of the full spin rate the input gives this long after take-off.
	internal static float InputShare( float airTime ) => 1f - 0.58f * Math.Clamp( airTime / 0.5f, 0f, 1f );

	// Spin that brings the deck in line with its travel (or across the rail it comes down on) by the time it lands.
	private float Alignment( in BoardBody body, float gravity, Vector3? rail )
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
		if ( rail is { } along && new Vector3( along.X, along.Y, 0f ).LengthSquared() > 0.01f )
		{
			// Turned well off its travel (as it is turning, a moment on), either way across the rail, whichever is nearer.
			var turned = Wrap( wanted - LandingHeading( heading, body.AngularVelocity.Z ) );
			if ( MathF.Abs( turned ) > MathF.PI / 2f )
				turned = Wrap( turned - MathF.PI );
			if ( MathF.Abs( turned ) > SlideTurn )
			{
				// Measured from where its turn is carrying it, so a fast turn into the slide does not swing on past it.
				error = Wrap( MathF.Atan2( along.Y, along.X ) + MathF.PI / 2f - LandingHeading( heading, body.AngularVelocity.Z ) );
				if ( MathF.Abs( error ) > MathF.PI / 2f )
					error = Wrap( error - MathF.PI );
			}
		}
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
