using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>Caches a tree for a host-authored world polyline, rebuilding when its segments change.</summary>
internal sealed class RailEdgeSource
{
	private RailSegment[] _segments = Array.Empty<RailSegment>();
	internal GrindEdgeChunk Chunk { get; private set; } = new GrindEdgeTreeBuilder( default, 0 ).Build();

	internal void Update( ReadOnlySpan<RailSegment> segments )
	{
		if ( Matches( segments ) )
			return;
		var minimum = new Vector3( float.PositiveInfinity );
		var maximum = new Vector3( float.NegativeInfinity );
		foreach ( var segment in segments )
		{
			var edge = RailEdgeSpace.ToLookup( segment );
			minimum = Vector3.Min( minimum, Vector3.Min( edge.Start, edge.End ) );
			maximum = Vector3.Max( maximum, Vector3.Max( edge.Start, edge.End ) );
		}
		var bounds = segments.IsEmpty ? default : new GrindEdgeBounds( minimum - Vector3.One, maximum + Vector3.One );
		var builder = new GrindEdgeTreeBuilder( bounds, segments.Length );
		for ( var i = 0; i < segments.Length; i++ )
		{
			var edge = RailEdgeSpace.ToLookup( segments[ i ] );
			var difference = edge.End - edge.Start;
			var vertical = !(MathF.Abs( difference.X ) > 0.0000152587890625f
				|| MathF.Abs( difference.Z ) > 0.0000152587890625f);
			builder.Insert( i, new GrindEdgeBounds( Vector3.Min( edge.Start, edge.End ), Vector3.Max( edge.Start, edge.End ) ),
				new GrindEdgeRecord( edge, (uint)i, vertical ? 0x80000000u : 0u ) );
		}
		Chunk = builder.Build();
		_segments = segments.ToArray();
	}

	private bool Matches( ReadOnlySpan<RailSegment> segments )
	{
		if ( segments.Length != _segments.Length )
			return false;
		for ( var i = 0; i < segments.Length; i++ )
		{
			if ( segments[ i ] != _segments[ i ] )
				return false;
		}
		return true;
	}
}
