using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Candidate outputs whose partial updates remain observable after rejection.</summary>
internal record struct GrindCandidateState(
	GrindEdgeTrajectory Trajectory, float Time, int SampleIndex,
	Vector3 Normal, Vector3 EdgeUp, Vector3 Side, Vector3 HighSide, RailSegment Edge,
	bool IsLedge, bool IsActive, GrindSurfaceContact Contact );
