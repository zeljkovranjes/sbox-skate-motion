namespace SkateMotion.Core.Animation;

/// <summary>Caller-supplied hit thresholds, curb bounds and angular clearance.</summary>
internal readonly record struct GrindSurfaceClassificationSettings(
	float LedgeFraction,
	float MinimumNormalLength,
	float OptionalNormalUpLimit,
	float CurbNormalAlignment,
	float CurbHighMinimum,
	float CurbHighMaximum,
	float CurbLowMinimum,
	float CurbLowMaximum,
	float QuarterTurn,
	float FullTurn,
	float AngleClearance );
