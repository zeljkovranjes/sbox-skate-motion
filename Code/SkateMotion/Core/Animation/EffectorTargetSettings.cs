namespace SkateMotion.Core.Animation;

/// <summary>Caller-supplied displacement limits, orientation limits and per-invocation decay.</summary>
internal readonly record struct EffectorTargetSettings(
	float PositionEpsilon, float MaximumDisplacement, float MaximumReleaseStep,
	float NormalThreshold, float OrientationDecay, float MinimumAngle, float MaximumAngle );
