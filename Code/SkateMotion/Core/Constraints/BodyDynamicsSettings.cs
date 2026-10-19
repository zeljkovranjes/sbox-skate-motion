namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct BodyDynamicsSettings(
	Vector3 LocalInverseInertia, float LinearDamping, float AngularDamping,
	float MaximumSpeed, float MaximumAngularSpeed, float AngularMotionWeight,
	float QuietThreshold, int MaximumQuietSteps );
