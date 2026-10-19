namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained solved walking frame and movement from physical position history.</summary>
internal readonly record struct PhysicalLocomotionMotionState(
	Vector3 FrameRow0, Vector3 FrameRow1, Vector3 FrameRow2, Vector3 Position, Vector3 Motion );
