namespace SkateMotion.Core.Vehicle;

/// <summary>
/// Handling response curves and rules (SI units: metres, seconds, radians unless named otherwise). Each describes
/// how one part of riding responds to speed, input or time; the board pilot and rider use them when
/// <see cref="SkateSettings.Recovered"/> handling is selected.
/// </summary>
internal static class HandlingCurves
{
	// Pop strength against how long the pop was wound up (frames at 60 Hz): slow at first, then quickly to full.
	private static readonly LinearCurve Windup = new(
		new[] { 0f, 2f, 4f, 6f, 8f, 10f, 12f, 14f, 16f, 20f, 30f },
		new[] { 0f, 0.004f, 0.054f, 0.19f, 0.39f, 0.56f, 0.73f, 0.85f, 0.93f, 0.984f, 1f } );

	// Share of the pop kept on a slope (degrees from flat): full to about 40 degrees, little on a wall.
	private static readonly LinearCurve SlopePop = new(
		new[] { 0f, 40f, 52f, 64f, 90f },
		new[] { 1f, 1f, 0.83f, 0.44f, 0.05f } );

	// Share of the pop height kept against riding speed (m/s).
	private static readonly LinearCurve SpeedPop = new(
		new[] { 11.1f, 15.3f, 18f, 27.8f },
		new[] { 1f, 0.8f, 0.52f, 0.46f } );

	// Steepest deck tilt against the ground a landing survives (radians), against speed along the ground (m/s).
	private static readonly LinearCurve LandingAngle = new(
		new[] { 9.26f, 11.74f, 14.13f, 17.05f, 20.42f, 23.63f, 26.28f, 27.77f },
		new[] { 1.6f, 1.297f, 1.032f, 0.784f, 0.573f, 0.411f, 0.303f, 0.263f } );

	// Steering response to the stick: a small dead zone, then roughly quadratic.
	private static readonly LinearCurve SteerInput = new(
		new[] { 0.0586f, 0.25f, 0.375f, 0.509f, 0.69f, 0.858f, 1f },
		new[] { 0f, 0.034f, 0.078f, 0.172f, 0.334f, 0.564f, 0.834f } );

	// Steering response against speed (m/s): trucks turn less the faster the board goes.
	private static readonly LinearCurve SteerSpeed = new(
		new[] { 2.85f, 6.19f, 8.65f, 10.95f, 14.9f, 20f, 25f, 50f },
		new[] { 1f, 0.97f, 0.848f, 0.608f, 0.395f, 0.3f, 0.28f, 0.27f } );

	// Rolling friction on smooth ground against speed (m/s to m/s^2): none at riding speeds, rising when very fast.
	private static readonly LinearCurve Rolling = new(
		new[] { 8.12f, 10.42f, 12.05f, 12.86f, 14.33f, 18.4f, 26.98f },
		new[] { 0f, 0.03f, 0.163f, 0.6f, 0.904f, 1.05f, 1.2f } );

	// Extra slowing while balancing a manual (m/s to m/s^2).
	private static readonly LinearCurve Manual = new(
		new[] { 0f, 8f },
		new[] { 0.132f, 0.5f } );

	// Pump gain against speed (m/s): strong when slow, nothing when very fast.
	private static readonly LinearCurve PumpSpeed = new(
		new[] { 0f, 2.4495f, 4.5603f, 7.0098f, 8.8078f, 10.684f, 12.1954f, 14.202f },
		new[] { 4f, 2.4429f, 1.1f, 1f, 0.9643f, 0.5714f, 0.1571f, 0.0071f } );

	// Share of a pump's effect against how long (seconds) the rider has been rising: all of it at first, little by a second.
	private static readonly LinearCurve PumpTime = new(
		new[] { 0f, 0.4023f, 0.5065f, 0.5717f, 0.6531f, 0.741f, 0.8616f, 0.9951f },
		new[] { 1f, 1f, 0.8964f, 0.7321f, 0.4321f, 0.2464f, 0.1143f, 0.0964f } );

	// Least crouch (share of a full crouch) against how steep the ground is (share of upright): none on the flat, most
	// of a crouch up a near-vertical wall.
	private static readonly LinearCurve MinimumCrouch = new(
		new[] { 0f, 0.0896f, 0.1612f, 0.3404f, 0.4609f, 0.6075f, 0.7915f, 1f },
		new[] { 0f, 0f, 0.0304f, 0.3649f, 0.5372f, 0.6655f, 0.75f, 0.7939f } );

	// Size of one push against speed (m/s to m/s added): large from a standstill, small near the push limit.
	private static readonly LinearCurve PushGain = new(
		new[] { 0f, 8.5f },
		new[] { 4.5f, 0.75f } );

	/// <summary>Share of full grind friction this many seconds into a grind: about two thirds at first, all of it after four seconds.</summary>
	internal static float GrindFrictionShare( float seconds ) => 0.633f + (1f - 0.633f) * Math.Clamp( seconds / 4f, 0f, 1f );

	/// <summary>Furthest (radians) the deck may turn from its grind balance before the grind is a crash.</summary>
	internal const float GrindDeckLimit = 1.3f;

	/// <summary>Fastest a board can be pushed (m/s).</summary>
	internal const float PushLimit = 8.5f;

	/// <summary>Pop strength (0 to 1) after winding up for the given seconds.</summary>
	internal static float PopStrength( float windup ) => Windup.Evaluate( windup * 60f );

	/// <summary>
	/// Launch speed (m/s, along the ground normal) of a pop: nearly the same for any windup, less on steep ground
	/// and when riding very fast.
	/// </summary>
	internal static float PopSpeed( float strength, float slopeDegrees, float speed,
		HandlingDifficulty difficulty = HandlingDifficulty.Normal )
	{
		// The quickest pop: full height on easy, lower on hard (its centre of mass rising 0.26 m rather than 0.38).
		var lowest = difficulty switch
		{
			HandlingDifficulty.Easy => 3.46f,
			HandlingDifficulty.Hard => 2.73f,
			_ => 3.3f
		};
		var launch = lowest + (3.46f - lowest) * Math.Clamp( strength, 0f, 1f );
		// Height scales with the square of launch speed, so the height share becomes a speed share by its root.
		return launch * MathF.Max( SlopePop.Evaluate( MathF.Abs( slopeDegrees ) ), 0.1f )
			* MathF.Sqrt( SpeedPop.Evaluate( MathF.Abs( speed ) ) );
	}

	/// <summary>Steepest deck tilt (radians) against the ground a landing at this speed along the ground survives.</summary>
	internal static float MaxLandingAngle( float tangentialSpeed ) => LandingAngle.Evaluate( MathF.Abs( tangentialSpeed ) );

	/// <summary>Share (0 to 1) of full truck angle for a steering input, keeping its sign.</summary>
	internal static float SteerResponse( float steer ) => MathF.Sign( steer ) * SteerInput.Evaluate( MathF.Abs( steer ) ) / 0.834f;

	/// <summary>Share (0 to 1) of full truck angle available at a speed (m/s).</summary>
	internal static float SteerAtSpeed( float speed ) => SteerSpeed.Evaluate( MathF.Abs( speed ) );

	/// <summary>Rolling deceleration (m/s^2) at a speed on smooth ground: none at riding speeds, rising when very fast.</summary>
	internal static float RollingDeceleration( float speed ) => Rolling.Evaluate( MathF.Abs( speed ) );

	/// <summary>Extra deceleration (m/s^2) while balancing a manual at a speed.</summary>
	internal static float ManualDeceleration( float speed ) => Manual.Evaluate( MathF.Abs( speed ) );

	/// <summary>Pump gain at a speed (m/s).</summary>
	internal static float PumpAtSpeed( float speed ) => PumpSpeed.Evaluate( MathF.Abs( speed ) );

	/// <summary>Share of a pump's effect this many seconds into a rise.</summary>
	internal static float PumpOverTime( float seconds ) => PumpTime.Evaluate( seconds );

	/// <summary>Least crouch (0 to 1) the rider holds on ground tilted this far (radians) from level.</summary>
	internal static float CrouchOnSlope( float tilt ) => MinimumCrouch.Evaluate( Math.Clamp( tilt / (MathF.PI / 2f), 0f, 1f ) );

	/// <summary>Share of a pump's effect when the rider rises without pumping on purpose.</summary>
	internal static float UnheldPump( HandlingDifficulty difficulty ) => difficulty switch
	{
		HandlingDifficulty.Easy => 1f,
		HandlingDifficulty.Hard => 0f,
		_ => 0.7f
	};

	/// <summary>Speed (m/s) one push adds at a speed, never past <see cref="PushLimit"/>.</summary>
	internal static float PushTarget( float speed )
	{
		var magnitude = MathF.Abs( speed );
		return MathF.Min( magnitude + PushGain.Evaluate( magnitude ), PushLimit );
	}

	/// <summary>Largest speed change (m/s) a push makes per second at a speed: 36 when slow, 30 at the push limit.</summary>
	internal static float PushRate( float speed, HandlingDifficulty difficulty = HandlingDifficulty.Normal )
	{
		var share = Math.Clamp( MathF.Abs( speed ) / PushLimit, 0f, 1f );
		// Speed change per sixtieth of a second, from a standstill to the push limit.
		return 60f * difficulty switch
		{
			HandlingDifficulty.Easy => 1.5f,
			HandlingDifficulty.Hard => 0.5f - 0.25f * share,
			_ => 0.6f - 0.1f * share
		};
	}

	/// <summary>Farthest (m) to the side a rail is locked onto from the air.</summary>
	internal static float GrindReach( HandlingDifficulty difficulty ) => difficulty switch
	{
		HandlingDifficulty.Easy => 1.35f,
		HandlingDifficulty.Hard => 0.15f,
		_ => Grinding.GrindAssist.Reach
	};

	/// <summary>Fastest (rad/s) the board turns itself to line up with its landing.</summary>
	internal static float AlignRate( HandlingDifficulty difficulty ) => difficulty == HandlingDifficulty.Hard ? 0.5f : 8f;
}
