namespace SkateMotion.Core.Animation;

/// <summary>Score floor, valid-time threshold and repeat-query tuning.</summary>
internal readonly record struct GrindCandidateSelectionSettings(
	float InitialScore, float MinimumTime, float MaximumCorrectionSpeed,
	float RepeatSpread, float RepeatSpreadScale );
