using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionCollectorReduction
{
	private static int SelectGeometry( ReadOnlySpan<uint> records, ReadOnlySpan<int> group, Span<int> selection )
	{
		var normal = Point( records, group, 0, 8 );
		var first = 0;
		var origin = Point( records, group, 0 );
		var depth = Dot( normal, origin.Subtract( Point( records, group, 0, 4 ) ) );
		for ( var i = 1; i < group.Length; ++i )
		{
			var point = Point( records, group, i );
			var candidate = Dot( normal, point.Subtract( Point( records, group, i, 4 ) ) );
			if ( !Greater( depth, candidate ) )
				continue;
			first = i;
			origin = point;
			depth = candidate;
		}
		selection[ 0 ] = first;
		var second = 0;
		var baseline = Point( records, group, 0 ).Subtract( origin );
		var distance = baseline.LengthSquared();
		for ( var i = 1; i < group.Length; ++i )
		{
			var delta = Point( records, group, i ).Subtract( origin );
			var candidate = delta.LengthSquared();
			if ( !Greater( candidate, distance ) )
				continue;
			second = i;
			baseline = delta;
			distance = candidate;
		}
		if ( first == second )
			return 1;
		selection[ 1 ] = second;
		var third = 0;
		var diagonal = Point( records, group, 0 ).Subtract( origin );
		var area = diagonal.Cross( baseline ).LengthSquared();
		for ( var i = 1; i < group.Length; ++i )
		{
			var delta = Point( records, group, i ).Subtract( origin );
			var candidate = delta.Cross( baseline ).LengthSquared();
			if ( !Greater( candidate, area ) )
				continue;
			third = i;
			diagonal = delta;
			area = candidate;
		}
		selection[ 2 ] = third;
		if ( third == first || third == second )
			return 2;
		var fourth = SelectFourth( records, group, origin, baseline, diagonal, normal );
		selection[ 3 ] = fourth;
		return fourth != first && fourth != second && fourth != third && fourth != 9999 ? 4 : 3;
	}

	private static int SelectFourth( ReadOnlySpan<uint> records, ReadOnlySpan<int> group,
		CollisionWordVector origin, CollisionWordVector baseline, CollisionWordVector diagonal,
		CollisionWordVector normal )
	{
		var lastEdge = diagonal.Subtract( baseline );
		var baselineNormal = normal.Cross( baseline );
		var diagonalNormal = normal.Cross( diagonal );
		var sign = Greater( 0, Dot( baselineNormal, diagonal ) ) ? 0xbf800000u : 0x3f800000u;
		baselineNormal = baselineNormal.Scale( sign );
		diagonalNormal = diagonalNormal.Scale( sign );
		var selected = 9999;
		var maximumArea = 0xbf800000u;
		for ( var i = 0; i < group.Length; ++i )
		{
			var delta = Point( records, group, i ).Subtract( origin );
			var cross = delta.Subtract( baseline ).Cross( lastEdge );
			if ( Greater( Dot( delta, diagonalNormal ), 0 ) )
				cross = delta.Cross( diagonal );
			if ( !Greater( Dot( delta, baselineNormal ), 0 ) )
				cross = delta.Cross( baseline );
			var area = cross.LengthSquared();
			if ( !Greater( area, maximumArea ) )
				continue;
			selected = i;
			maximumArea = area;
		}
		return selected;
	}
}
