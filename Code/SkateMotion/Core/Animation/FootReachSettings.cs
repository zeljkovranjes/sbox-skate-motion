namespace SkateMotion.Core.Animation;

/// <summary>Supplied limits for height smoothing, clearance correction and reach status.</summary>
internal readonly record struct FootReachSettings(
	float MinimumRequest, float MaximumRequest, float MinimumStep, float MaximumStep,
	float MinimumCorrection, float ContactBias, float MaximumContactHeight, float Clearance,
	float ReferenceHeightThreshold, float FlagDistanceThreshold, float FlagOffsetThreshold );
