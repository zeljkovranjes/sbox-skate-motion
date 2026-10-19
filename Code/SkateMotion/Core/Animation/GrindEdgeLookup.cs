using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Queries and suppresses nearby edges using a reference position in Y-up metres.</summary>
internal sealed class GrindEdgeLookup
{
	private readonly RailSegment[] _segments = new RailSegment[ GrindEdgeQuery.Capacity ];
	private readonly int[] _retained = new int[ GrindEdgeQuery.Capacity ];

	internal int Collect( GrindEdgeChunk[] chunks, GrindEdgeBounds bounds, Vector3 reference,
		GrindEdgeRecord[] output )
	{
		var count = GrindEdgeQuery.Collect( chunks, bounds, false, output );
		for ( var i = 0; i < count; i++ )
			_segments[ i ] = output[ i ].Segment;
		var retained = GrindEdgeFilter.FilterSpan( _segments.AsSpan( 0, count ), reference, _retained );
		for ( var i = 0; i < retained; i++ )
			output[ i ] = output[ _retained[ i ] ];
		return retained;
	}
}
