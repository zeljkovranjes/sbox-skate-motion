namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Producer-owned candidate geometry, independent of the surface classification gate.</summary>
internal readonly record struct PhysicalHandCandidateInput(
	bool Enabled, Vector3 Point, GrindEdgeRecord Edge, Vector3 Origin, Vector3 Approach,
	Vector3 Normal, Vector3 Side, Vector3 Tangent, Vector3 PreviousPoint );
