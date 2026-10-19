namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Binds the existing ordered static edge registry and its two optional providers to ground queries.</summary>
internal sealed class PhysicalGroundRegisteredEdges
{
	private readonly GrindEdgeRecord[] _records = new GrindEdgeRecord[ GrindEdgeQuery.Capacity ];
	private readonly uint[] _output = new uint[ GrindEdgeQuery.Capacity * 12 ];
	private GrindEdgeChunk[] _chunks;
	private readonly PhysicalGroundEdgeProvider _first;
	private readonly PhysicalGroundEdgeProvider _second;

	internal PhysicalGroundRegisteredEdges( GrindEdgeChunk[] chunks,
		PhysicalGroundEdgeProvider first = null, PhysicalGroundEdgeProvider second = null )
	{
		ArgumentNullException.ThrowIfNull( chunks );
		_chunks = chunks;
		_first = first;
		_second = second;
	}

	internal void ReplaceChunks( GrindEdgeChunk[] chunks )
	{
		ArgumentNullException.ThrowIfNull( chunks );
		_chunks = chunks;
	}

	internal ReadOnlyMemory<uint> Collect( Vector4 minimum, Vector4 maximum, uint worldSelection, int instanceFilter )
	{
		var bounds = new GrindEdgeBounds( new( minimum.X, minimum.Y, minimum.Z ), new( maximum.X, maximum.Y, maximum.Z ) );
		var count = GrindEdgeQuery.Collect( _chunks, bounds, false, _records );
		for ( var index = 0; index < count; index++ )
		{
			var offset = index * 12;
			var record = _records[ index ];
			Write( offset, record.Segment.Start );
			Write( offset + 4, record.Segment.End );
			_output[ offset + 8 ] = record.Metadata;
			_output[ offset + 9 ] = (_output[ offset + 9 ] & 0x7fffffff) | (record.Flags & 0x80000000);
		}
		_first?.Invoke( bounds, worldSelection, instanceFilter, _output, ref count );
		ValidateCount( count );
		_second?.Invoke( bounds, worldSelection, instanceFilter, _output, ref count );
		ValidateCount( count );
		return _output.AsMemory( 0, count * 12 );
	}

	private static void ValidateCount( int count )
	{
		if ( (uint)count > GrindEdgeQuery.Capacity )
			throw new InvalidOperationException( "A registered provider exceeded the retained edge capacity." );
	}

	private void Write( int offset, Vector3 value )
	{
		_output[ offset ] = BitConverter.SingleToUInt32Bits( value.X );
		_output[ offset + 1 ] = BitConverter.SingleToUInt32Bits( value.Y );
		_output[ offset + 2 ] = BitConverter.SingleToUInt32Bits( value.Z );
		_output[ offset + 3 ] = 0;
	}
}
