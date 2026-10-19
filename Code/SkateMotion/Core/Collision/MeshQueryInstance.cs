namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Collision geometry with world transforms, local instance bounds and query filters.</summary>
internal readonly record struct MeshQueryInstance(
	MeshQueryGeometry Geometry,
	Matrix4x4 Transform,
	Matrix4x4 InverseTransform,
	Vector3 Minimum,
	Vector3 Maximum,
	int Filter,
	uint Exclusion,
	uint Identity );
