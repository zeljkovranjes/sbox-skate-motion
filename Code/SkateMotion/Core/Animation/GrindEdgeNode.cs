namespace SkateMotion.Core.Animation;

/// <summary>A loose-tree node whose octants contain chain heads or child node indices.</summary>
internal sealed class GrindEdgeNode
{
	private readonly int[] _children;
	internal int Head { get; }
	internal byte LeafMask { get; }

	internal GrindEdgeNode( int head, byte leafMask, int[] children )
	{
		ArgumentNullException.ThrowIfNull( children );
		if ( head < -1 || children.Length != 8 )
			throw new ArgumentException( "A node needs eight octants and a chain head or -1." );
		Head = head;
		LeafMask = leafMask;
		_children = [ .. children ];
	}

	internal bool IsLeaf( int octant ) => (LeafMask & (1 << octant)) != 0;
	internal int Child( int octant ) => _children[ octant ];
}
