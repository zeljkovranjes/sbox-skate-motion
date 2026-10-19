using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static class CollisionContactManifold
{
	internal static int Assemble( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, uint margin,
		TriangleContactFilterContext context, Span<uint> output )
	{
		if ( first.Length < 44 || second.Length < 44 || output.Length < 140 )
			throw new ArgumentException( "A manifold requires two complete descriptors and storage for sixteen contact pairs." );
		Span<uint> separation = stackalloc uint[ 8 ];
		CollisionPairAxes.Pair( first, second, separation );
		if ( Greater( separation[ 0 ], Add( Add( first[ 32 ], second[ 32 ] ), margin ) ) )
			return 0;
		Span<uint> firstFeature = stackalloc uint[ 144 ];
		Span<uint> secondFeature = stackalloc uint[ 144 ];
		Span<uint> contacts = stackalloc uint[ 136 ];
		Span<uint> normal = stackalloc uint[ 4 ];
		Span<uint> opposite = stackalloc uint[ 4 ];
		firstFeature.Clear();
		secondFeature.Clear();
		contacts.Clear();
		separation[ 4..8 ].CopyTo( normal );
		Negate( Read( normal, 0 ) ).Write( opposite, 0 );
		// Descriptor identity tags stay internal to the temporary feature records.
		CollisionShapeFeatures.Select( first, normal, true, 0, firstFeature );
		CollisionShapeFeatures.Select( second, opposite, false, 0, secondFeature );
		CollisionFeatureContacts.Assemble( firstFeature, secondFeature, normal, contacts );
		RefineNormal( first, second, separation[ 0 ], contacts, normal );
		var accepted = second[ 36 ] != 3 || CollisionTriangleFilter.Apply( second, normal, true, contacts, context ) != 0;
		var count = checked( (int)contacts[ 132 ] );
		if ( count is < 0 or > 16 )
			throw new ArgumentException( "Contact count exceeds manifold storage." );
		Write( first, second, normal, contacts, count, output );
		return accepted ? count : 0;
	}

	private static void RefineNormal( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, uint originalGap,
		Span<uint> contacts, Span<uint> normal )
	{
		if ( contacts[ 132 ] == 1 )
		{
			var difference = Read( contacts, 64 ).Subtract( Read( contacts, 0 ) );
			var squared = difference.LengthSquared();
			var inverse = InverseRoot( squared );
			if ( Greater( Multiply( squared, inverse ), 0x34000000u ) )
			{
				difference.Scale( inverse ).Write( contacts, 128 );
				contacts[ 133 ] = 1;
			}
		}
		if ( contacts[ 133 ] == 0 )
			return;
		Span<uint> firstInterval = stackalloc uint[ 8 ];
		Span<uint> secondInterval = stackalloc uint[ 8 ];
		CollisionShapeProjection.Project( first, contacts[ 128..132 ], firstInterval );
		CollisionShapeProjection.Project( second, contacts[ 128..132 ], secondInterval );
		var reverse = Subtract( firstInterval[ 0 ], secondInterval[ 4 ] );
		var forward = Subtract( secondInterval[ 0 ], firstInterval[ 4 ] );
		var flipped = Greater( reverse, forward );
		var candidateGap = flipped ? reverse : forward;
		if ( Greater( originalGap, candidateGap ) )
			return;
		var candidate = Read( contacts, 128 );
		(flipped ? Negate( candidate ) : candidate).Write( normal, 0 );
	}

	private static void Write( ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, ReadOnlySpan<uint> normal,
		ReadOnlySpan<uint> contacts, int count, Span<uint> output )
	{
		output[ 0 ] = first[ 33 ];
		output[ 1 ] = second[ 33 ];
		output[ 2 ] = first[ 34 ];
		output[ 3 ] = second[ 34 ];
		output[ 4 ] = (uint)count;
		var direction = Read( normal, 0 );
		Negate( direction ).Write( output, 8 );
		var firstOffset = direction.Scale( first[ 32 ] );
		var secondOffset = direction.Scale( second[ 32 ] );
		for ( var i = 0; i < count; ++i )
		{
			Sum( Read( contacts, i * 4 ), firstOffset ).Write( output, 12 + i * 8 );
			Read( contacts, 64 + i * 4 ).Subtract( secondOffset ).Write( output, 16 + i * 8 );
		}
	}

	private static CollisionWordVector Read( ReadOnlySpan<uint> words, int offset ) => CollisionWordVector.Read( words, offset );
	private static CollisionWordVector Negate( CollisionWordVector value ) =>
		new( value.X ^ 0x80000000u, value.Y ^ 0x80000000u, value.Z ^ 0x80000000u, value.W ^ 0x80000000u );
	private static CollisionWordVector Sum( CollisionWordVector a, CollisionWordVector b ) =>
		new( Add( a.X, b.X ), Add( a.Y, b.Y ), Add( a.Z, b.Z ), Add( a.W, b.W ) );

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < 2; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}
}
