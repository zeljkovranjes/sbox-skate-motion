namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Collects nearby static edges before preparing paired contact geometry.</summary>
internal sealed class PhysicalHandSourceLookup
{
	private readonly GrindEdgeRecord[] _nearby = new GrindEdgeRecord[ GrindEdgeQuery.Capacity ];
	internal PhysicalHandSourceGeometry Geometry { get; } = new();
	internal int NearbyCount { get; private set; }
	internal int Count { get; private set; }

	internal bool TryPrepare( GrindEdgeChunk[] chunks, Vector3 origin, Vector3 normal )
	{
		var extent = new Vector3( 2f, 4f, 2f );
		NearbyCount = GrindEdgeQuery.Collect( chunks, new GrindEdgeBounds( origin - extent, origin + extent ),
			false, _nearby );
		Count = 0;
		if ( NearbyCount == 0 )
			return false;
		Geometry.Prepare( origin, normal, _nearby, NearbyCount );
		Count = Geometry.Count;
		return Count != 0;
	}
}
