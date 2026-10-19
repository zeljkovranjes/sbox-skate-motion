namespace SkateMotion.Core.Animation;

/// <summary>Owns a static tree and ordered edge chains; rebuild when its geometry changes.</summary>
internal sealed class GrindEdgeChunk
{
	private readonly GrindEdgeNode[] _nodes;
	private readonly int[] _next;
	private readonly GrindEdgeBounds[] _edgeBounds;
	private readonly GrindEdgeRecord[] _records;
	internal GrindEdgeBounds Bounds { get; }
	internal GrindEdgeBounds TreeBounds { get; }
	internal int NodeCount => _nodes.Length;
	internal int EdgeCount => _records.Length;

	internal GrindEdgeChunk( GrindEdgeBounds bounds, GrindEdgeNode[] nodes, int[] next,
		GrindEdgeBounds[] edgeBounds, GrindEdgeRecord[] records )
		: this( bounds, bounds, nodes, next, edgeBounds, records )
	{
	}

	internal GrindEdgeChunk( GrindEdgeBounds bounds, GrindEdgeBounds treeBounds, GrindEdgeNode[] nodes,
		int[] next, GrindEdgeBounds[] edgeBounds, GrindEdgeRecord[] records )
	{
		ArgumentNullException.ThrowIfNull( nodes );
		ArgumentNullException.ThrowIfNull( next );
		ArgumentNullException.ThrowIfNull( edgeBounds );
		ArgumentNullException.ThrowIfNull( records );
		if ( nodes.Length == 0 || next.Length != records.Length || edgeBounds.Length != records.Length )
			throw new ArgumentException( "A chunk needs a root and matching record, bounds and link arrays." );
		Bounds = bounds;
		TreeBounds = treeBounds;
		_nodes = [ .. nodes ];
		_next = [ .. next ];
		_edgeBounds = [ .. edgeBounds ];
		_records = [ .. records ];
		ValidateIndices();
	}

	internal GrindEdgeNode Node( int index ) => _nodes[ index ];
	internal int Next( int index ) => _next[ index ];
	internal GrindEdgeBounds EdgeBounds( int index ) => _edgeBounds[ index ];
	internal GrindEdgeRecord Record( int index ) => _records[ index ];

	private void ValidateIndices()
	{
		foreach ( var next in _next )
			CheckEdgeIndex( next );
		foreach ( var node in _nodes )
		{
			ArgumentNullException.ThrowIfNull( node );
			CheckEdgeIndex( node.Head );
			for ( var octant = 0; octant < 8; octant++ )
			{
				var child = node.Child( octant );
				if ( node.IsLeaf( octant ) )
					CheckEdgeIndex( child );
				else if ( child < 0 || child >= _nodes.Length )
					throw new ArgumentException( "An internal octant must reference a node in this chunk." );
			}
		}
	}

	private void CheckEdgeIndex( int index )
	{
		if ( index < -1 || index >= _records.Length )
			throw new ArgumentException( "An edge chain index must reference this chunk or be -1." );
	}
}
