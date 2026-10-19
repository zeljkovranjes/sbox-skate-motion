namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Supplied rig motion, board pose and world-space alignment inputs.</summary>
internal readonly record struct DescendingAttachmentInput(
	float VerticalMotion, bool Blocked, LimbRigidTransform WorldToModel,
	LimbRigidTransform TargetWorld, LimbRigidTransform Board,
	Vector3 WorldDisplacement, float ResponseParameter );
