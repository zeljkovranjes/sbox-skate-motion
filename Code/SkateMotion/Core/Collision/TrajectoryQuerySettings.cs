namespace SkateMotion.Core.Collision;

/// <summary>Sampling and normal-selection parameters supplied by the trajectory caller.</summary>
internal readonly record struct TrajectoryQuerySettings(
	float SampleInterval, float MinimumMagnitude, float GroundNormalY, float MinimumNormalLength );
