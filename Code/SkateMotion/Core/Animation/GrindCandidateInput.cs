namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Approach motion, distinct reference/support positions and caller-owned query dimensions.</summary>
internal readonly record struct GrindCandidateInput(
	Vector3 Approach, Vector3 Reference, Vector3 Support,
	float SeparationThreshold, float Reach, float FirstDeckExtent, float SecondDeckExtent, int Filter );
