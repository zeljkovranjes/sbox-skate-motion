namespace SkateMotion.Core.Animation;

/// <summary>Angular conversion, spread limits and retained height initialization for query directions.</summary>
internal readonly record struct GrindQueryFanSettings(
	float MinimumLength, float SpeedScale, float DegreesToRadians, float MinimumSpreadSpeed,
	float MaximumSpreadSpeed, float FullCircle, float InitialHeight );
