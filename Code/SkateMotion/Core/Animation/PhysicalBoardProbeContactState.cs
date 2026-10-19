namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Four wheel observations and the nearest accepted wheel distance for the current batch.</summary>
internal sealed class PhysicalBoardProbeContactState
{
	internal Vector3[] Normals { get; } = new Vector3[ 4 ];
	internal float[] Distances { get; } = new float[ 4 ];
	internal uint[] SurfaceIndices { get; } = new uint[ 4 ];
	internal uint[] MaterialIndices { get; } = new uint[ 4 ];
	internal uint[] Flags { get; } = new uint[ 4 ];
	internal float ClosestDistance { get; set; }
}
