namespace SkateMotion.Core.Animation;

/// <summary>Animated and authored poses plus the already-updated effector weights.</summary>
internal readonly record struct EffectorPoseInput(
	LimbRigidTransform Animated, LimbRigidTransform? Reference, LimbRigidTransform Authored,
	float TargetWeight, float OverrideWeight );
