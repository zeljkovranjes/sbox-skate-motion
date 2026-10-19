namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Caller-supplied trajectory, angular-unit and correction parameters.</summary>
internal readonly record struct GrindApproachSettings(
	int SampleCount, float NormalAcceleration, Vector3 Gravity,
	float NormalizationThreshold, float AngularSpeedThreshold, float AngleScale,
	float AngleFoldThreshold, float AngleFoldPeriod, float MaximumCorrectionVelocity,
	float MaximumCorrectionPosition );
