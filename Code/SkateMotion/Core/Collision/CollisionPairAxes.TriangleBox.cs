using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	internal static void TriangleBoxPair( ReadOnlySpan<uint> triangle, ReadOnlySpan<uint> box, Span<uint> output )
	{
		Validate( triangle, box, output );
		if ( triangle[ 36 ] != 3 || box[ 36 ] != 4 )
			throw new ArgumentException( "Triangle/box separation requires a triangle followed by a box." );
		Span<uint> axes = stackalloc uint[ 52 ];
		var count = 0;
		Append( Read( triangle, 4 ), axes, ref count );
		for ( var axis = 3; axis > 0; --axis )
			Append( Read( box, axis * 4 ), axes, ref count );
		for ( var a = 0; a < 3; ++a )
		{
			for ( var b = 0; b < 3; ++b )
			{
				var axis = Read( triangle, 16 + a * 4 ).Cross( Read( box, 16 + b * 4 ) );
				var squared = axis.LengthSquared();
				var estimate = Interpolate( squared, RootEstimate( squared ) );
				var inverse = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
					NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
				Append( axis.Scale( inverse ), axes, ref count );
			}
		}
		Span<uint> boxIntervals = stackalloc uint[ 156 ];
		CollisionShapeProjection.ProjectBatch( box, axes, boxIntervals );
		var selected = 0;
		var separation = 0u;
		var sign = 0x3f800000u;
		for ( var i = 0; i < 13; ++i )
		{
			var axis = Read( axes, i * 4 );
			var second = TriangleDot( Read( triangle, 8 ), axis );
			var third = TriangleDot( Read( triangle, 12 ), axis );
			var first = TriangleDot( Read( triangle, 0 ), axis );
			var greater = Greater( second, third );
			var maximum = greater ? second : third;
			var minimum = greater ? third : second;
			maximum = Greater( first, maximum ) ? first : maximum;
			minimum = Greater( first, minimum ) ? minimum : first;
			var reverse = Subtract( minimum, boxIntervals[ i * 12 + 4 ] );
			var forward = Subtract( boxIntervals[ i * 12 ], maximum );
			var flip = Greater( reverse, forward );
			var gap = flip ? reverse : forward;
			if ( i == 0 || Greater( gap, separation ) )
			{
				selected = i;
				separation = gap;
				sign = flip ? 0xbf800000u : 0x3f800000u;
			}
		}
		output[ ..4 ].Fill( separation );
		Read( axes, selected * 4 ).Scale( sign ).Write( output, 4 );
	}

	internal static void BoxTrianglePair( ReadOnlySpan<uint> box, ReadOnlySpan<uint> triangle, Span<uint> output )
	{
		TriangleBoxPair( triangle, box, output );
		for ( var i = 4; i < 8; ++i )
			output[ i ] ^= 0x80000000u;
	}

	private static uint TriangleDot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
}
