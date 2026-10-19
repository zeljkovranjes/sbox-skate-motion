namespace SkateMotion.Core.Animation;

/// <summary>Authored launch-speed endpoints and the caller's correction influence.</summary>
internal readonly record struct PhysicalJumpVelocityTuning(
	float MinimumSpeed, float MaximumSpeed, float CorrectionInfluence );
