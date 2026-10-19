namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Bound rig frames with unchanged reference and motion vectors.</summary>
internal readonly record struct EffectorRigFrameSet(
	LimbRigidTransform ModelToWorld,
	LimbRigidTransform WorldToModel,
	LimbRigidTransform Authored,
	LimbRigidTransform Contact,
	LimbRigidTransform Solver,
	LimbRigidTransform Offset,
	Vector3 ReferencePosition,
	Vector3 MotionDelta );
