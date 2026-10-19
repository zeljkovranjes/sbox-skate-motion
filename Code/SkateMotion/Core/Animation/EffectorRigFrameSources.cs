namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Supplied character frames and reference vectors before rig binding.</summary>
internal readonly record struct EffectorRigFrameSources(
	LimbRigidTransform ModelToWorld,
	LimbRigidTransform WorldToModel,
	LimbRigidTransform Authored,
	LimbRigidTransform Contact,
	LimbRigidTransform Solver,
	LimbRigidTransform Offset,
	LimbRigidTransform AnimatedRoot,
	Vector3 ReferencePosition,
	Vector3 MotionDelta );
