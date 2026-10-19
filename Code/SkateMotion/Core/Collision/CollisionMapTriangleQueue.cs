namespace SkateMotion.Core.Collision;

/// <summary>Queues decoded triangles in their supplied order before generating contact batches.</summary>
internal sealed class CollisionMapTriangleQueue
{
	private readonly uint[] _triangles = new uint[ 20 * 44 ];
	private readonly ReadOnlyMemory<CollisionMapBodyInput> _bodies;
	private readonly CollisionMapContactContext _context;
	private readonly ICollisionContactCollector _collector;
	internal int Count { get; private set; }
	internal uint TotalTriangles { get; private set; }
	internal ReadOnlySpan<uint> Pending => _triangles.AsSpan( 0, Count * 44 );

	internal CollisionMapTriangleQueue( ReadOnlyMemory<CollisionMapBodyInput> bodies,
		CollisionMapContactContext context, ICollisionContactCollector collector )
	{
		ArgumentNullException.ThrowIfNull( collector );
		_bodies = bodies;
		_context = context;
		_collector = collector;
	}

	internal void Enqueue( ReadOnlySpan<uint> triangle )
	{
		if ( triangle.Length < 44 || triangle[ 36 ] != 3 )
			throw new ArgumentException( "The queue requires a complete decoded triangle descriptor.", nameof( triangle ) );
		if ( Count == 20 )
		{
			GeneratePending();
			Count = 0;
		}
		var offset = Count++ * 44;
		var destination = _triangles.AsSpan( offset, 44 );
		triangle[ ..35 ].CopyTo( destination );
		// Descriptor padding belongs to the destination slot, including its two trailing flag bytes.
		destination[ 35 ] = (triangle[ 35 ] & 0xffff0000u) | (destination[ 35 ] & 0xffffu);
		triangle.Slice( 36, 5 ).CopyTo( destination[ 36.. ] );
		TotalTriangles = unchecked( TotalTriangles + 1 );
	}

	// The caller owns the batch lifecycle. Explicit generation retains pending records.
	internal void GeneratePending() => CollisionMapContactBatch.Generate( _bodies.Span, Pending, _context, _collector );
}
