namespace SkateMotion.Core.Animation;

/// <summary>Supplied hierarchy, bind poses, animated poses and mutable output bones.</summary>
internal readonly record struct EffectorRigSkeleton(
	EffectorBoneBinding[] Bindings,
	int[] Parents,
	LimbRigidTransform[] BindTransforms,
	LimbRigidTransform[] AnimatedBones,
	LimbRigidTransform[] MutableBones );
