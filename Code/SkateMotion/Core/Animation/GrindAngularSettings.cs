namespace SkateMotion.Core.Animation;

/// <summary>Supplied angular units, normalization gates, period and correction limits.</summary>
internal readonly record struct GrindAngularSettings(
	float DegreesPerRadian, float RadiansPerDegree, float MaximumRate,
	float RollFactor, float NormalizationThreshold, float MinimumSquaredLength,
	float Period, float InversePeriod );
