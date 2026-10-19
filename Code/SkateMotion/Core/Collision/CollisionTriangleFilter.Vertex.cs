using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionTriangleFilter
{
	private static uint FilterVertex( ReadOnlySpan<uint> triangle, int first, int second,
		CollisionWordVector direction, CollisionWordVector face, uint flags, bool adjustBoundary,
		Span<uint> normal, bool reverseNormal, Span<uint> contacts )
	{
		var firstConvex = (flags & (0x20u << first)) != 0;
		var secondConvex = (flags & (0x20u << second)) != 0;
		if ( adjustBoundary )
		{
			if ( !Greater( Dot( direction, face ), 0 ) )
				return 0;
			if ( firstConvex && !secondConvex && InAdjustmentRange( triangle[ 38 + second ] ) )
				return CorrectEdgePlane( triangle, first, direction, normal, reverseNormal, contacts );
			if ( !firstConvex && secondConvex && InAdjustmentRange( triangle[ 38 + first ] ) )
				return CorrectEdgePlane( triangle, second, direction, normal, reverseNormal, contacts );
			return 0;
		}
		return TestOneSidedEdge( triangle, first, direction, face, firstConvex )
			&& TestOneSidedEdge( triangle, second, direction, face, secondConvex ) ? 1u : 0u;
	}

	private static bool InAdjustmentRange( uint cosine ) =>
		Greater( cosine, 0x3a83126eu ) && !Greater( cosine, 0x3f7851ebu );

	private static bool TestOneSidedEdge( ReadOnlySpan<uint> triangle, int index,
		CollisionWordVector direction, CollisionWordVector face, bool convex )
	{
		var edge = Negate( CollisionWordVector.Read( triangle, 16 + (2 - index) * 4 ) );
		if ( Greater( 0, Dot( direction, edge.Cross( face ) ) ) )
			return true;
		if ( !convex )
			return false;
		var projected = Normalize( Project( direction, edge ), 2 );
		return !Greater( triangle[ 38 + index ], Dot( projected, face ) );
	}

	private static uint CorrectEdgePlane( ReadOnlySpan<uint> triangle, int edgeIndex,
		CollisionWordVector direction, Span<uint> normal, bool reverseNormal, Span<uint> contacts )
	{
		var edge = CollisionWordVector.Read( triangle, 16 + (2 - edgeIndex) * 4 );
		var corrected = Normalize( edge.Cross( direction ).Cross( edge ), 1 );
		Correct( corrected, normal, reverseNormal, contacts );
		return 1;
	}

	private static void Correct( CollisionWordVector replacement, Span<uint> normal, bool reverseNormal,
		Span<uint> contacts )
	{
		(reverseNormal ? Negate( replacement ) : replacement).Write( normal, 0 );
		var count = (int)contacts[ 132 ];
		for ( var i = 0; i < count; ++i )
		{
			var first = CollisionWordVector.Read( contacts, i * 4 );
			var second = CollisionWordVector.Read( contacts, 64 + i * 4 );
			var difference = reverseNormal ? second.Subtract( first ) : first.Subtract( second );
			var anchor = reverseNormal ? first : second;
			var projected = replacement.MultiplyAdd( Dot( difference, replacement ), anchor );
			projected.Write( contacts, (reverseNormal ? 64 : 0) + i * 4 );
		}
	}
}
