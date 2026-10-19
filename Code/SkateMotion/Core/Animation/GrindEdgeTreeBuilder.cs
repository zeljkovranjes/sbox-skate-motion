namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Builds a static loose tree by inserting bounded records in producer order.</summary>
internal sealed class GrindEdgeTreeBuilder
{
	private readonly GrindEdgeBounds _bounds;
	private readonly GrindEdgeBounds _treeBounds;
	private readonly int[] _heads;
	private readonly byte[] _leafMasks;
	private readonly int[][] _children;
	private readonly byte[][] _counts;
	private readonly int[] _next;
	private readonly bool[] _splittable;
	private readonly bool[] _inserted;
	private readonly GrindEdgeBounds[] _edgeBounds;
	private readonly GrindEdgeRecord[] _records;
	private int _nodeCount = 1;
	private int _freeNode = 1;
	private int _insertedCount;

	internal GrindEdgeTreeBuilder( GrindEdgeBounds bounds, int edgeCount )
	{
		if ( edgeCount < 0 || edgeCount > 65535 )
			throw new ArgumentOutOfRangeException( nameof( edgeCount ) );
		_bounds = bounds;
		var center = LimbSolverMath.Scale( bounds.Minimum + bounds.Maximum, .5f );
		var half = LimbSolverMath.Scale( bounds.Maximum - bounds.Minimum, .5f );
		var extent = new Vector3( MathF.Max( MathF.Max( half.X, half.Y ), half.Z ) );
		_treeBounds = new GrindEdgeBounds( center - extent, center + extent );
		var capacity = edgeCount / 2 + 1;
		_heads = new int[ capacity ];
		_leafMasks = new byte[ capacity ];
		_children = new int[ capacity ][];
		_counts = new byte[ capacity ][];
		for ( var i = 0; i < capacity; i++ )
		{
			_heads[ i ] = -1;
			_leafMasks[ i ] = 255;
			_children[ i ] = new int[ 8 ];
			Array.Fill( _children[ i ], -1 );
			_counts[ i ] = new byte[ 8 ];
		}
		_next = new int[ edgeCount ];
		_splittable = new bool[ edgeCount ];
		_inserted = new bool[ edgeCount ];
		_edgeBounds = new GrindEdgeBounds[ edgeCount ];
		_records = new GrindEdgeRecord[ edgeCount ];
	}

	internal void Insert( int index, GrindEdgeBounds bounds, GrindEdgeRecord record )
	{
		if ( index < 0 || index >= _records.Length )
			throw new ArgumentOutOfRangeException( nameof( index ) );
		if ( _inserted[ index ] )
			throw new ArgumentException( "Each record must be inserted once." );
		_inserted[ index ] = true;
		_insertedCount++;
		_edgeBounds[ index ] = bounds;
		_records[ index ] = record;
		if ( !Contains( _treeBounds, bounds ) )
		{
			Attach( index, 0, -1, default );
			return;
		}
		var node = 0;
		var current = _treeBounds;
		while ( true )
		{
			if ( !SelectChild( current, bounds, out var octant, out var childBounds ) )
			{
				Attach( index, node, -1, default );
				return;
			}
			if ( (_leafMasks[ node ] & (1 << octant)) == 0 )
			{
				node = _children[ node ][ octant ];
				current = childBounds;
				continue;
			}
			Attach( index, node, octant, childBounds );
			if ( _counts[ node ][ octant ] > 3 && _freeNode != -1 )
				Split( node, octant, childBounds );
			return;
		}
	}

	internal GrindEdgeChunk Build()
	{
		if ( _insertedCount != _records.Length )
			throw new InvalidOperationException( "Insert every declared edge before building the chunk." );
		var nodes = new GrindEdgeNode[ _nodeCount ];
		for ( var i = 0; i < nodes.Length; i++ )
			nodes[ i ] = new GrindEdgeNode( _heads[ i ], _leafMasks[ i ], _children[ i ] );
		return new GrindEdgeChunk( _bounds, _treeBounds, nodes, _next, _edgeBounds, _records );
	}

	private void Attach( int edge, int node, int octant, GrindEdgeBounds childBounds )
	{
		if ( octant == -1 )
		{
			_next[ edge ] = _heads[ node ];
			_heads[ node ] = edge;
			_splittable[ edge ] = false;
			return;
		}
		_next[ edge ] = _children[ node ][ octant ];
		_children[ node ][ octant ] = edge;
		var fits = FitsNext( childBounds, _edgeBounds[ edge ] );
		_splittable[ edge ] = fits;
		if ( fits && _counts[ node ][ octant ] < byte.MaxValue )
			_counts[ node ][ octant ]++;
	}

	private void Split( int parent, int octant, GrindEdgeBounds bounds )
	{
		var node = _freeNode;
		_freeNode = node + 1 < _heads.Length ? node + 1 : -1;
		_nodeCount++;
		var edge = _children[ parent ][ octant ];
		_children[ parent ][ octant ] = node;
		_leafMasks[ parent ] = (byte)(_leafMasks[ parent ] & ~(1 << octant));
		while ( edge != -1 )
		{
			var next = _next[ edge ];
			if ( _splittable[ edge ] )
			{
				SelectChild( bounds, _edgeBounds[ edge ], out var child, out var childBounds );
				Attach( edge, node, child, childBounds );
			}
			else
				Attach( edge, node, -1, default );
			edge = next;
		}
		// Redistribution does not recursively split an already crowded child.
	}

	private static bool Contains( GrindEdgeBounds outer, GrindEdgeBounds inner ) =>
		!(outer.Minimum.X > inner.Minimum.X || outer.Minimum.Y > inner.Minimum.Y || outer.Minimum.Z > inner.Minimum.Z
		|| inner.Maximum.X > outer.Maximum.X || inner.Maximum.Y > outer.Maximum.Y || inner.Maximum.Z > outer.Maximum.Z);

	private static void InnerBounds( GrindEdgeBounds bounds, out Vector3 low, out Vector3 high )
	{
		var inset = LimbSolverMath.Scale( bounds.Maximum - bounds.Minimum, -.4f );
		low = bounds.Minimum - inset;
		high = bounds.Maximum + inset;
	}

	private static bool SelectChild( GrindEdgeBounds bounds, GrindEdgeBounds edge, out int octant, out GrindEdgeBounds child )
	{
		InnerBounds( bounds, out var low, out var high );
		var lowSlack = edge.Minimum - low;
		var highSlack = high - edge.Maximum;
		octant = -1;
		child = default;
		if ( (lowSlack.X <= 0f && highSlack.X <= 0f) || (lowSlack.Y <= 0f && highSlack.Y <= 0f)
			|| (lowSlack.Z <= 0f && highSlack.Z <= 0f) )
			return false;
		octant = (lowSlack.X > highSlack.X ? 1 : 0) | (lowSlack.Y > highSlack.Y ? 2 : 0) | (lowSlack.Z > highSlack.Z ? 4 : 0);
		child = new GrindEdgeBounds(
			new Vector3( (octant & 1) == 0 ? bounds.Minimum.X : low.X,
				(octant & 2) == 0 ? bounds.Minimum.Y : low.Y, (octant & 4) == 0 ? bounds.Minimum.Z : low.Z ),
			new Vector3( (octant & 1) == 0 ? high.X : bounds.Maximum.X,
				(octant & 2) == 0 ? high.Y : bounds.Maximum.Y, (octant & 4) == 0 ? high.Z : bounds.Maximum.Z ) );
		return true;
	}

	private static bool FitsNext( GrindEdgeBounds bounds, GrindEdgeBounds edge )
	{
		InnerBounds( bounds, out var low, out var high );
		return (edge.Minimum.X > low.X || high.X > edge.Maximum.X)
			&& (edge.Minimum.Y > low.Y || high.Y > edge.Maximum.Y)
			&& (edge.Minimum.Z > low.Z || high.Z > edge.Maximum.Z);
	}
}
