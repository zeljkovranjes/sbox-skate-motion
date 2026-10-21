namespace SkateMotion.Core.Camera;

/// <summary>
/// How a <see cref="CameraSpring"/> responds: acceleration = Stiffness * error + Damping * (target velocity -
/// velocity), with optional limits on speed and acceleration.
/// </summary>
internal readonly record struct SpringTuning( float Stiffness, float Damping, float MaxSpeed = float.PositiveInfinity,
	float MaxAcceleration = float.PositiveInfinity )
{
	/// <summary>Critically damped at the given natural frequency (rad/s): the fastest settle that never overshoots.</summary>
	internal static SpringTuning Critical( float frequency ) => new( frequency * frequency, 2f * frequency );

	/// <summary>The same response with a speed limit.</summary>
	internal SpringTuning Limited( float maxSpeed ) => this with { MaxSpeed = maxSpeed };
}
