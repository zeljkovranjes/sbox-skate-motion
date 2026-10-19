using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionPairAxes
{
	internal static void SpherePair( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		Validate( first, second, output );
		if ( first[ 36 ] != 1 || second[ 36 ] != 1 )
			throw new ArgumentException( "Sphere separation requires two sphere descriptors." );
		var difference = Read( second, 0 ).Subtract( Read( first, 0 ) );
		var squared = difference.LengthSquared();
		var inverse = InverseRoot( squared );
		output[ ..4 ].Fill( Multiply( squared, inverse ) );
		difference.Scale( inverse ).Write( output, 4 );
	}

	internal static void GeneralPair( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		Validate( first, second, output );
		Span<uint> axes = stackalloc uint[ 60 ];
		var count = 0;
		AppendFaces( first, axes, ref count );
		AppendFaces( second, axes, ref count );
		var firstEdges = (int)((first[ 35 ] >> 16) & 0xffu);
		var secondEdges = (int)((second[ 35 ] >> 16) & 0xffu);
		for ( var a = 0; a < firstEdges; ++a )
		{
			for ( var b = 0; b < secondEdges; ++b )
			{
				var axis = Read( first, 16 + a * 4 ).Cross( Read( second, 16 + b * 4 ) );
				var squared = axis.LengthSquared();
				if ( Greater( squared, 0x3a83126eu ) )
					Append( axis.Scale( InverseRoot( squared ) ), axes, ref count );
			}
		}
		if ( firstEdges == 1 && secondEdges == 1 )
		{
			var a = Read( first, 16 );
			var b = Read( second, 16 );
			var cross = a.Cross( b );
			var squared = cross.LengthSquared();
			if ( Greater( Multiply( squared, InverseRoot( squared ) ), 0x34000000u ) )
			{
				AppendNormalized( a.Cross( cross ), axes, ref count );
				AppendNormalized( b.Cross( cross ), axes, ref count );
			}
		}
		if ( count == 0 )
		{
			var difference = Read( first, 0 ).Subtract( Read( second, 0 ) );
			if ( firstEdges == 1 && secondEdges == 1 )
			{
				var edge = Read( first, 16 );
				AppendNormalized( difference.Cross( edge ).Cross( edge ), axes, ref count );
			}
			if ( firstEdges + secondEdges == 1 )
			{
				var edge = Read( firstEdges == 1 ? first : second, 16 );
				AppendNormalized( difference.Cross( edge ).Cross( edge ), axes, ref count );
			}
			if ( count == 0 )
			{
				AppendNormalized( difference, axes, ref count );
				if ( count == 0 )
					Append( new( 0, 0, 0x3f800000u, 0 ), axes, ref count );
			}
		}
		Span<uint> firstIntervals = stackalloc uint[ 180 ];
		Span<uint> secondIntervals = stackalloc uint[ 180 ];
		CollisionShapeProjection.ProjectBatch( first, axes[ ..(count * 4) ], firstIntervals );
		CollisionShapeProjection.ProjectBatch( second, axes[ ..(count * 4) ], secondIntervals );
		var selected = 0;
		var separation = 0u;
		var flipped = false;
		for ( var i = 0; i < count; ++i )
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
				flipped = flip;
			}
		}
		output[ ..4 ].Fill( separation );
		var normal = Read( axes, selected * 4 );
		if ( flipped )
			normal = new( normal.X ^ 0x80000000u, normal.Y ^ 0x80000000u, normal.Z ^ 0x80000000u, normal.W ^ 0x80000000u );
		normal.Write( output, 4 );
	}

	private static void Validate( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<uint> output )
	{
		if ( first.Length < 44 || second.Length < 44 || output.Length < 8 )
			throw new ArgumentException( "Pair separation requires two shape descriptors and two output rows." );
		if ( first[ 36 ] is < 1 or > 5 || second[ 36 ] is < 1 or > 5 )
			throw new ArgumentException( "Unsupported primitive descriptor." );
		if ( first[ 35 ] >> 24 > 3 || second[ 35 ] >> 24 > 3
			|| ((first[ 35 ] >> 16) & 0xffu) > 3 || ((second[ 35 ] >> 16) & 0xffu) > 3 )
			throw new ArgumentException( "Primitive face and edge counts must fit the descriptor." );
	}

	private static void AppendFaces( ReadOnlySpan<uint> shape, Span<uint> axes, ref int count )
	{
		for ( var index = (int)(shape[ 35 ] >> 24); index > 0; --index )
			Append( Read( shape, index * 4 ), axes, ref count );
	}

	private static void AppendNormalized( CollisionWordVector axis, Span<uint> axes, ref int count )
	{
		var squared = axis.LengthSquared();
		var inverse = InverseRoot( squared );
		if ( Greater( Multiply( squared, inverse ), 0x34000000u ) )
			Append( axis.Scale( inverse ), axes, ref count );
	}

	private static void Append( CollisionWordVector axis, Span<uint> axes, ref int count )
	{
		axis.Write( axes, count * 4 );
		++count;
	}

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < 2; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}

	private static CollisionWordVector Read( ReadOnlySpan<uint> words, int offset ) => CollisionWordVector.Read( words, offset );
}
