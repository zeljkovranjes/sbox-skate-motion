namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>World frame, contact plane and state flags supplied to postsolve feedback.</summary>
internal readonly record struct PhysicalPoseFeedbackInput(
	LimbRigidTransform ModelToWorld, Vector3 ContactNormal, float DeltaTime, bool DirectFeedback, bool ResetHistory );
