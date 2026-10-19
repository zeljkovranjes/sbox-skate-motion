namespace SkateMotion;

/// <summary>World-space physical part state; positions/velocities use metres and angular velocity uses radians per second.</summary>
public readonly record struct SkateBodyState(
	SkateBodyPart Part, Vector3 Position, Rotation Rotation, Vector3 Velocity, Vector3 AngularVelocity );
