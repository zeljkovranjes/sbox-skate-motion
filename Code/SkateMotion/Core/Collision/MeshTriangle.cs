namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Local-space collision vertices and their packed surface material.</summary>
internal readonly record struct MeshTriangle( Vector3 A, Vector3 B, Vector3 C, ushort Material );
