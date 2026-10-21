namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Ordered local triangles, their enclosing local bounds and, for a large shape, a grid to find nearby ones.</summary>
internal readonly record struct MeshQueryGeometry( MeshTriangle[] Triangles, Vector3 Minimum, Vector3 Maximum,
	TriangleGrid Grid = null );
