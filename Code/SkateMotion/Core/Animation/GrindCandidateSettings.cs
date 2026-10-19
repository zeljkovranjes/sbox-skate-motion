namespace SkateMotion.Core.Animation;

/// <summary>Caller-supplied approach gates, ledge separation scales and accepted-contact limits.</summary>
internal readonly record struct GrindCandidateSettings(
	float MinimumHeightDelta, float MaximumDirectionY, float MinimumDirectionLength,
	float MaximumVerticalApproach, float MaximumLedgeHorizontalSquared, float MaximumRailHorizontalSquared,
	float PositiveCorrectionNegativeSupportScale, float PositiveCorrectionPositiveSupportScale,
	float NegativeCorrectionNegativeSupportScale, float NegativeCorrectionPositiveSupportScale,
	float LongCorrectionScale, float MaximumCorrectionAngleDegrees, float MaximumCorrectionSpeed,
	float VelocityGain, float MaximumTiltDegrees, float SampleInterval, float RadiansPerDegree,
	float MinimumAngleLengthProduct );
