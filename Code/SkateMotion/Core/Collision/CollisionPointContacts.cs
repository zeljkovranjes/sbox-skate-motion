using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

// The point branches of feature-pair dispatch also update the selected feature's packed classification.
internal static class CollisionPointContacts
{
	internal static void Assemble( Span<uint> first, Span<uint> second,
		ReadOnlySpan<uint> direction, Span<uint> contacts )
	{
		if ( first.Length < 144 || second.Length < 144 || direction.Length < 4 || contacts.Length < 136 )
			throw new ArgumentException( "Contact assembly requires complete feature buffers, a direction and contact storage." );
		var firstKind = first[ 140 ];
		var secondKind = second[ 140 ];
		if ( !ValidKind( firstKind ) || !ValidKind( secondKind ) || (firstKind != 0 && secondKind != 0) )
			throw new ArgumentException( "Point contact assembly requires at least one point feature." );
		contacts[ 132 ] = 1;
		if ( firstKind == 0 && secondKind == 0 )
		{
			Read( first, 136 ).Write( contacts, 0 );
			Read( second, 136 ).Write( contacts, 64 );
			return;
		}
		var featureFirst = firstKind != 0;
		var feature = featureFirst ? first : second;
		var point = Read( featureFirst ? second : first, 136 );
		var selected = feature[ 140 ] == 1
			? ProjectSegment( feature, point )
			: Polygon( feature, point, Read( direction, 0 ) );
		selected.Write( contacts, featureFirst ? 0 : 64 );
		point.Write( contacts, featureFirst ? 64 : 0 );
	}

	internal static CollisionWordVector ProjectSegment( Span<uint> feature, CollisionWordVector point )
	{
		var start = Read( feature, 4 );
		var tangent = Read( feature, 8 );
		var distance = Dot( point.Subtract( start ), tangent );
		uint classification;
		CollisionWordVector selected;
		if ( Greater( 0, distance ) )
		{
			classification = 1;
			selected = start;
		}
		else if ( Greater( distance, feature[ 16 ] ) )
		{
			classification = 3;
			selected = tangent.MultiplyAdd( feature[ 16 ], start );
		}
		else
		{
			classification = 2;
			selected = tangent.MultiplyAdd( distance, start );
		}
		feature[ 0 ] = unchecked( feature[ 0 ] + 2 - classification );
		return selected;
	}

	private static CollisionWordVector Polygon( Span<uint> feature, CollisionWordVector point, CollisionWordVector direction )
	{
		var offset = Read( feature, 4 ).Subtract( point );
		var projected = direction.MultiplyAdd( Dot( direction, offset ), point );
		var edge = -1;
		uint maximum = 0;
		for ( var index = 0; index < feature[ 140 ]; ++index )
		{
			var record = 4 + index * 16;
			var distance = Dot( Read( feature, record + 8 ), projected.Subtract( Read( feature, record ) ) );
			if ( edge < 0 || Greater( distance, maximum ) )
			{
				edge = index;
				maximum = distance;
			}
		}
		if ( !Greater( maximum, 0 ) )
			return projected;
		var selectedRecord = 4 + edge * 16;
		var start = Read( feature, selectedRecord );
		var tangent = Read( feature, selectedRecord + 4 );
		var auxiliary = Read( feature, selectedRecord + 8 );
		var length = feature[ selectedRecord + 12 ];
		var corrected = SubtractScaled( projected, auxiliary, maximum );
		var along = Dot( corrected.Subtract( start ), tangent );
		uint classification;
		CollisionWordVector selected;
		if ( Greater( 0, along ) )
		{
			classification = 1;
			selected = start;
		}
		else if ( Greater( along, length ) )
		{
			classification = 3;
			selected = tangent.MultiplyAdd( length, start );
		}
		else
		{
			classification = 2;
			selected = tangent.MultiplyAdd( along, start );
		}
		feature[ 0 ] = unchecked( feature[ 0 ] + (uint)(edge * 2) + classification );
		return selected;
	}

	private static bool ValidKind( uint kind ) => kind is 0 or 1 or 3 or 4 or 8;
	private static CollisionWordVector Read( ReadOnlySpan<uint> buffer, int offset ) => CollisionWordVector.Read( buffer, offset );
	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
	private static CollisionWordVector SubtractScaled( CollisionWordVector value, CollisionWordVector axis, uint scale ) => new(
		NegativeMultiplySubtract( axis.X, scale, value.X ), NegativeMultiplySubtract( axis.Y, scale, value.Y ),
		NegativeMultiplySubtract( axis.Z, scale, value.Z ), NegativeMultiplySubtract( axis.W, scale, value.W ) );
}
