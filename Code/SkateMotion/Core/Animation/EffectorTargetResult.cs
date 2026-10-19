namespace SkateMotion.Core.Animation;

/// <summary>Updated target state and transforms; an absent reference has no transform update.</summary>
internal readonly record struct EffectorTargetResult(
	EffectorTargetState State, LimbRigidTransform Target, LimbRigidTransform? Reference );
