namespace SkateMotion.Core.Animation;

/// <summary>Owns independent request, candidate and one-use surface result lifetimes.</summary>
internal sealed class PhysicalHandCandidatePipelineState
{
	internal PhysicalHandRequestState Request { get; } = new();
	internal PhysicalHandCandidateState Candidate { get; } = new();
	internal GrindSurfaceQueryResult Surface { get; } = new();
}
