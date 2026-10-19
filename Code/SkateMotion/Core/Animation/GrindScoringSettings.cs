namespace SkateMotion.Core.Animation;

/// <summary>Resolved timing, directional, kind and special-state score parameters.</summary>
internal readonly record struct GrindScoringSettings(
	float SampleInterval, float MinimumLength, float SidewaysGain, float AlongGain,
	float FirstCandidateBonus, float SpecialGain, float TimeWindow, float OrientationThreshold,
	float NearApexPenalty, float FirstKindPenalty, float SecondKindPenalty );
