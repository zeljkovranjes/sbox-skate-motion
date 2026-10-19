using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	internal static void BoxPair( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		Validate( first, second, output );
		if ( first[ 36 ] != 4 || second[ 36 ] != 4 )
			throw new ArgumentException( "Box separation requires two box descriptors." );
		Span<uint> axes = stackalloc uint[ 64 ];
		var count = 0;
		for ( var axis = 3; axis > 0; --axis )
			Append( Read( first, axis * 4 ), axes, ref count );
		for ( var axis = 3; axis > 0; --axis )
			Append( Read( second, axis * 4 ), axes, ref count );
		for ( var a = 0; a < 3; ++a )
		{
			for ( var b = 0; b < 3; ++b )
			{
				var axis = Read( first, 16 + a * 4 ).Cross( Read( second, 16 + b * 4 ) );
				var squared = axis.LengthSquared();
				var estimate = Interpolate( squared, RootEstimate( squared ) );
				var inverse = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
					NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
				Append( axis.Scale( inverse ), axes, ref count );
			}
		}
		Append( Read( axes, 56 ), axes, ref count );
		Span<uint> firstIntervals = stackalloc uint[ 192 ];
		Span<uint> secondIntervals = stackalloc uint[ 192 ];
		CollisionShapeProjection.ProjectBatch( first, axes, firstIntervals );
		CollisionShapeProjection.ProjectBatch( second, axes, secondIntervals );
		var selected = 0;
		var separation = 0u;
		var sign = 0x3f800000u;
		for ( var i = 0; i < 16; ++i )
		{
			var offset = i * 12;
			var reverse = Subtract( firstIntervals[ offset ], secondIntervals[ offset + 4 ] );
			var forward = Subtract( secondIntervals[ offset ], firstIntervals[ offset + 4 ] );
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
}
