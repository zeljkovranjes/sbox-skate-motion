namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Coordinate frames and reference positions supplied for one rig invocation.</summary>
internal readonly record struct EffectorRigFrame(
	LimbRigidTransform Root,
	LimbRigidTransform ModelToWorld,
	LimbRigidTransform WorldToModel,
	LimbFrame AuthoredOrientation,
	LimbRigidTransform ContactFrame,
	LimbFrame OffsetOrientation,
	float ReferenceHeight,
	Vector3 WeightAnchor );
