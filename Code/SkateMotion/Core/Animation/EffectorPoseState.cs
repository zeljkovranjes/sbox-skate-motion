namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Persistent pose caches and request state for one of the rig's four effectors.</summary>
internal readonly record struct EffectorPoseState(
	EffectorTargetState TargetState, LimbRigidTransform PinnedTarget, LimbRigidTransform PinnedReference,
	LimbRigidTransform AnimatedTarget, LimbRigidTransform AuthoredTarget,
	LimbRigidTransform AnimatedReference, LimbRigidTransform AuthoredReference,
	Vector3 LocalPosition, bool InsideBounds );
