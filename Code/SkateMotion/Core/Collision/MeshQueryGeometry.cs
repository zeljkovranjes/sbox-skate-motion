namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Ordered local triangles and their enclosing local bounds.</summary>
internal readonly record struct MeshQueryGeometry( MeshTriangle[] Triangles, Vector3 Minimum, Vector3 Maximum );
