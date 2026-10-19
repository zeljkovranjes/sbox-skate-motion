namespace SkateMotion.Core.Animation;

/// <summary>Caller-supplied normalization gates and cross-section probe dimensions.</summary>
internal readonly record struct GrindSurfaceProbeSettings(
	float MinimumNormalizeLength,
	float MinimumUpLength,
	float NearWidth,
	float NearHeight,
	float FarHeightScale,
	float OverheadRadius,
	float DownwardDepth );
