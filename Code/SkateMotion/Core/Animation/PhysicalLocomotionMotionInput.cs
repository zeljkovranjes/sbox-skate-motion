namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Current physical position and retained orientation axes prepared by the ground owner.</summary>
internal readonly record struct PhysicalLocomotionMotionInput(
	Vector3 CurrentPosition, Vector3 Forward, Vector3 Up, Vector3 OrientationAxis );
