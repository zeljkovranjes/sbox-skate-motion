namespace SkateMotion.Core.Vehicle;

/// <summary>
/// A kick turn: rolling slowly (or standing) with the steering pushed over, the rider lifts the nose and pivots the
/// board round on its back wheels. It starts below <see cref="StartSpeed"/> with the steering past
/// <see cref="Steering"/>, and keeps turning below <see cref="KeepSpeed"/> while the steering stays over. The
/// steering threshold is the original's; the start speed stands in for its turn-torque test (a board too slow to carve),
/// which allowed up to 4 m/s only where the slope also takes the carve away.
/// </summary>
internal sealed class KickTurn
{
	/// <summary>Steering past which a slow board kick turns.</summary>
	internal const float Steering = 0.3f;

	/// <summary>Speed (m/s) below which a kick turn starts.</summary>
	internal const float StartSpeed = 1f;

	/// <summary>Speed (m/s) below which a kick turn under way carries on.</summary>
	internal const float KeepSpeed = 1.5f;

	/// <summary>Turn rate (radians per second) at full steering, about the back wheels.</summary>
	internal const float Rate = 4f;

	/// <summary>True while the board kick turns.</summary>
	internal bool Turning { get; private set; }

	/// <summary>Whether the board kick turns this step, at this speed along it and this steering; <paramref name="allowed"/>
	/// is false while it cannot (in the air, on a rail, in a manual or a powerslide).</summary>
	internal bool Step( float speed, float steer, bool allowed )
	{
		Turning = allowed && MathF.Abs( steer ) > Steering && MathF.Abs( speed ) < (Turning ? KeepSpeed : StartSpeed);
		return Turning;
	}

	internal void Reset() => Turning = false;
}
