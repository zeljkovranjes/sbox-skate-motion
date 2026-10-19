namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Collects ordered static edge records from supplied loose trees.</summary>
internal static class GrindEdgeQuery
{
	internal const int Capacity = 40;
	private const int PendingCapacity = 211;

	internal static int Collect( GrindEdgeChunk[] chunks, GrindEdgeBounds query,
		bool excludeVertical, GrindEdgeRecord[] output )
	{
		ArgumentNullException.ThrowIfNull( chunks );
		ArgumentNullException.ThrowIfNull( output );
		if ( output.Length < Capacity )
			throw new ArgumentException( "Output must have space for 40 edge records." );
		var diagonal = query.Minimum - query.Maximum;
		if ( !(LimbSolverMath.Dot( diagonal, diagonal ) > 0.0000152587890625f) )
			return 0;
		var count = 0;
		Span<int> pendingNodes = stackalloc int[ PendingCapacity ];
		Span<GrindEdgeBounds> pendingBounds = stackalloc GrindEdgeBounds[ PendingCapacity ];
		Span<int> heads = stackalloc int[ 9 ];
		foreach ( var chunk in chunks )
		{
			if ( !query.Overlaps( chunk.Bounds ) )
				continue;
			pendingNodes[ 0 ] = 0;
			pendingBounds[ 0 ] = chunk.TreeBounds;
			var pending = 1;
			var visited = 0;
			while ( pending > 0 )
			{
				if ( ++visited > chunk.NodeCount )
					throw new ArgumentException( "A chunk must be an acyclic tree with unique child nodes." );
				pending--;
				var node = chunk.Node( pendingNodes[ pending ] );
				var bounds = pendingBounds[ pending ];
				var inset = LimbSolverMath.Scale( bounds.Maximum - bounds.Minimum, -.4f );
				var lowerHigh = bounds.Maximum + inset;
				var upperLow = bounds.Minimum - inset;
				var headCount = 0;
				if ( node.Head != -1 )
					heads[ headCount++ ] = node.Head;
				for ( var octant = 0; octant < 8; octant++ )
				{
					if ( !TouchesOctant( query, lowerHigh, upperLow, octant ) )
						continue;
					var child = node.Child( octant );
					if ( node.IsLeaf( octant ) )
					{
						if ( child != -1 )
							heads[ headCount++ ] = child;
						continue;
					}
					if ( pending == PendingCapacity )
						throw new ArgumentException( "The tree exceeds the pending node capacity." );
					pendingNodes[ pending ] = child;
					pendingBounds[ pending++ ] = ChildBounds( bounds, lowerHigh, upperLow, octant );
				}
				// Leaf chains are consumed in reverse octant order before pending child nodes.
				for ( var head = headCount - 1; head >= 0; head-- )
				{
					var traversed = 0;
					for ( var edge = heads[ head ]; edge != -1; edge = chunk.Next( edge ) )
					{
						if ( ++traversed > chunk.EdgeCount )
							throw new ArgumentException( "Edge chains must be acyclic." );
						if ( !query.Overlaps( chunk.EdgeBounds( edge ) ) )
							continue;
						var record = chunk.Record( edge );
						if ( excludeVertical && record.IsVertical )
							continue;
						// A reused slot keeps its other flags; only the vertical bit is replaced.
						output[ count ] = record with { Flags = (output[ count ].Flags & 0x7fffffffu) | (record.Flags & 0x80000000u) };
						if ( ++count == Capacity )
							return count;
					}
				}
			}
		}
		return count;
	}

	private static bool TouchesOctant( GrindEdgeBounds query, Vector3 lowerHigh, Vector3 upperLow, int octant ) =>
		!(((octant & 1) == 0 ? query.Minimum.X > lowerHigh.X : upperLow.X > query.Maximum.X)
		|| ((octant & 2) == 0 ? query.Minimum.Y > lowerHigh.Y : upperLow.Y > query.Maximum.Y)
		|| ((octant & 4) == 0 ? query.Minimum.Z > lowerHigh.Z : upperLow.Z > query.Maximum.Z));

	private static GrindEdgeBounds ChildBounds( GrindEdgeBounds parent, Vector3 lowerHigh, Vector3 upperLow, int octant ) => new(
		new Vector3( (octant & 1) == 0 ? parent.Minimum.X : upperLow.X,
			(octant & 2) == 0 ? parent.Minimum.Y : upperLow.Y, (octant & 4) == 0 ? parent.Minimum.Z : upperLow.Z ),
		new Vector3( (octant & 1) == 0 ? lowerHigh.X : parent.Maximum.X,
			(octant & 2) == 0 ? lowerHigh.Y : parent.Maximum.Y, (octant & 4) == 0 ? lowerHigh.Z : parent.Maximum.Z ) );
}
