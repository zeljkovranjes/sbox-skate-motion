namespace SkateMotion.Core;

internal sealed record SkateSettings
{
	/// <summary>How forgiving the measured handling is.</summary>
	internal HandlingDifficulty Difficulty { get; init; }

	/// <summary>Whether the rider pumps a little without the pump button (see <see cref="Vehicle.PumpControl"/>).</summary>
	internal bool AutoPump { get; init; }

	internal float Mass { get; init; } = 80f;
	internal float BoardMass { get; init; } = 3f;
	internal float Gravity { get; init; } = 9.81f;
	internal float Wheelbase { get; init; } = 0.52f;
	internal float Track { get; init; } = 0.19f;
	internal float RideHeight { get; init; } = 0.095f;
	internal float Spring { get; init; } = 32000f;
	internal float Damping { get; init; } = 1600f;
	internal float Grip { get; init; } = 0.85f;
	internal float RollingResistance { get; init; } = 0.012f;
	internal float AirDrag { get; init; } = 0.003f;
	internal float PushAcceleration { get; init; } = 5f;
	internal float PushSpeed { get; init; } = 8.5f;
	internal float PushPeriod { get; init; } = 0.65f;
	internal float BrakeAcceleration { get; init; } = 5f;
	internal float TruckAngle { get; init; } = 0.28f;
	internal float SteeringSpeedScale { get; init; } = 0.06f;

	/// <summary>Sideways acceleration a full-steer carve asks of the wheels at speed, m/s^2 (about half a g).</summary>
	internal float CarveAcceleration { get; init; } = 5f;
	internal float PopSpeed { get; init; } = 4.1f;
	internal float FlipRate { get; init; } = 9f;
	internal float SpinRate { get; init; } = 6f;
	internal float AirTorque { get; init; } = 18f;
	internal float GrindFriction { get; init; } = 0.08f;
	internal float BailSpeed { get; init; } = 8f;
	internal float RecoveryTime { get; init; } = 1f;
	internal float BalanceSpring { get; init; } = 180f;
	internal float BalanceDamping { get; init; } = 30f;

	/// <summary>
	/// Handling from measured response curves (<see cref="Vehicle.HandlingCurves"/>): pops of a nearly fixed launch
	/// speed, pushes that add a set speed while the foot is down, speed-dependent rolling friction and steering, a
	/// landing-angle limit, a held manual and rate-controlled spins in the air. Off, the simpler estimated handling
	/// these settings describe is used.
	/// </summary>
	internal bool Recovered { get; init; }

	/// <summary>Settings for the measured handling (see <see cref="Recovered"/>).</summary>
	internal static SkateSettings RecoveredDefaults => new()
	{
		Recovered = true,
		Gravity = 9.8f,
		AirDrag = 0f,
		// Kept under the wheels' grip (with room for weight shifting to the outer wheels), so a hard carve at speed
		// turns as tightly as the wheels can hold instead of sliding the board out.
		CarveAcceleration = 5f
	};
	internal void Validate()
	{
		if ( !Positive( Mass ) || !Positive( BoardMass ) || BoardMass > Mass || !Positive( Gravity )
			|| !Positive( Wheelbase ) || !Positive( Track ) || !Positive( RideHeight )
			|| !Positive( Spring ) || !Positive( Damping ) || !Positive( Grip )
			|| !Positive( PushSpeed ) || !Positive( PushPeriod ) || !Positive( PopSpeed )
			|| !Positive( BailSpeed ) || !Positive( RecoveryTime ) )
			throw new ArgumentException( "Mass, geometry and response settings must be finite and positive." );
		if ( !Nonnegative( RollingResistance ) || !Nonnegative( AirDrag ) || !Nonnegative( PushAcceleration )
			|| !Nonnegative( BrakeAcceleration ) || !Nonnegative( SteeringSpeedScale ) || !Positive( CarveAcceleration )
			|| !Nonnegative( TruckAngle ) || TruckAngle > 0.7f || !Nonnegative( GrindFriction )
			|| !Nonnegative( AirTorque ) || !Nonnegative( FlipRate ) || !Nonnegative( SpinRate )
			|| !Nonnegative( BalanceSpring ) || !Nonnegative( BalanceDamping ) )
			throw new ArgumentException( "Force settings must be finite and nonnegative; truck angle is at most 0.7 radians." );
	}

	private static bool Positive( float value ) => float.IsFinite( value ) && value > 0f;
	private static bool Nonnegative( float value ) => float.IsFinite( value ) && value >= 0f;

	/// <summary>
	/// Truck angle for a steering input at a speed (m/s). The truck geometry narrows with speed, and at speed the
	/// angle is further held to the carve the wheels can grip, so full steer never asks for more than
	/// <see cref="CarveAcceleration"/> sideways (a sharper request would only skid the wheels and scrub speed).
	/// </summary>
	internal float SteeringAngle( float steer, float speed )
	{
		var geometric = TruckAngle / (1f + MathF.Abs( speed ) * SteeringSpeedScale);
		var squared = speed * speed;
		var grip = squared > 0.0001f ? MathF.Atan( Wheelbase * CarveAcceleration / (2f * squared) ) : geometric;
		return steer * MathF.Min( geometric, grip );
	}
}
