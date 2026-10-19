namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Completed offboard job output and physical-owner inputs for character adoption.</summary>
internal readonly record struct PhysicalOffboardCharacterInput(
	PhysicalLocomotionMotionState Solved, Vector3 FeedbackReference,
	ulong ActionFlags, ulong RigFlags, ulong MotionFlags, float Control,
	PhysicalRigInput RigInput );
