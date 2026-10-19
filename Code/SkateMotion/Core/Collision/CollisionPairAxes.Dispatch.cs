namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	internal static void Pair( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		Validate( first, second, output );
		if ( first[ 36 ] == 5 )
			CylinderPair( first, second, output );
		else if ( second[ 36 ] == 5 )
			ReversedCylinderPair( first, second, output );
		else if ( first[ 36 ] == 1 && second[ 36 ] == 1 )
			SpherePair( first, second, output );
		else if ( first[ 36 ] == 4 && second[ 36 ] == 4 )
			BoxPair( first, second, output );
		else if ( first[ 36 ] == 3 && second[ 36 ] == 4 )
			TriangleBoxPair( first, second, output );
		else if ( first[ 36 ] == 4 && second[ 36 ] == 3 )
			BoxTrianglePair( first, second, output );
		else
			GeneralPair( first, second, output );
	}
}
