namespace SkateMotion.Core.Animation;

/// <summary>Resolved orientation, probe, score and correction limits for fallback rejection.</summary>
internal readonly record struct GrindCandidateRejectionSettings(
	float MaximumAbsDirectionY, float MaximumNormalDot, float ProbeOffset, float ProbeDrop,
	float DistanceScale, float ScorePenalty, float MinimumHorizontalLength,
	float MaximumVerticalRatio, float MinimumDistance, float MaximumCorrectionSpeed );
