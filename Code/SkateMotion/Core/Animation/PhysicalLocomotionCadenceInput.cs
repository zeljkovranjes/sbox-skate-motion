namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Physical and authored animation inputs in a common, Y-up controller frame.</summary>
internal readonly record struct PhysicalLocomotionCadenceInput(
	Vector3 Motion, Vector3 MotionReference, Vector3 Up, Vector3 RejectionAxis, bool RejectMotion,
	Vector3 FrameRow0, Vector3 FrameRow1, Vector3 FrameRow2, Vector3 AnimationMotion,
	float RequestedPhase, float RequestedDuration, bool SuppressContactAdjustment, uint ContactFlags );
