namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>World-space swept segment and its instance and surface filters.</summary>
internal readonly record struct MeshQueryRequest(
	Vector3 Start, Vector3 End, float Radius, int Filter, uint Exclusion, MeshQueryFilter Surfaces );
