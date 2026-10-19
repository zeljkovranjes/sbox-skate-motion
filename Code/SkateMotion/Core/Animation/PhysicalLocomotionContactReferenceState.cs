namespace SkateMotion.Core.Animation;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

/// <summary>Retained contact frame and linear motion history used by physical gait.</summary>
internal readonly record struct PhysicalLocomotionContactReferenceState(
	Matrix4x4 Frame, uint Identity, Vector3 Velocity, Vector3 Reference,
	Vector3 PreviousVelocity, Vector3 Acceleration );
