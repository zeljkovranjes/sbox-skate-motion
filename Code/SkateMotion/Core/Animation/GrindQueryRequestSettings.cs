namespace SkateMotion.Core.Animation;

/// <summary>Origin clearance, duration controls and retained-trajectory acceptance limits.</summary>
internal readonly record struct GrindQueryRequestSettings(
	float Radius, float MaximumOffset, float PredictionHeight, float MaximumDuration,
	float FirstStep, float LaterStep, float VelocityScale, float GravityFactor, float InverseGravity,
	float ReuseDistanceSquared, float ReuseMinimumSpeed, float ReuseMinimumRatio, float ReuseMaximumRatio );
