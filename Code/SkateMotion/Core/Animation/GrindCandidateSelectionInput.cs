namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Inputs shared by scoring, selected-trajectory preparation and query regeneration.</summary>
internal readonly record struct GrindCandidateSelectionInput(
	int MinimumSample, GrindScoringContext Scoring, GrindCandidateInput Contact,
	GrindQueryRequestInput Request, Vector4 Anchor );
