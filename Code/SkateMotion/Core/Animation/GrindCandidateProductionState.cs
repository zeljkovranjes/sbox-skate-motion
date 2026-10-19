namespace SkateMotion.Core.Animation;

/// <summary>Keeps found-edge status distinct from retained acceptance and candidate outputs.</summary>
internal record struct GrindCandidateProductionState(
	GrindCandidateState Candidate, bool FoundEdges, bool Accepted );
