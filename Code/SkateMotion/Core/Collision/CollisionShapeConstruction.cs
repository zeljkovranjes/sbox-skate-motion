using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

// Packed geometry words preserve cached normals, feature metadata and unwritten fields.
// Source: four geometry rows, parameter row, then radius/material/flag row.
// Destination: four geometry rows, three edge rows, dimensions, and three metadata rows.
internal static class CollisionShapeConstruction
{
	internal static bool TryConstruct( int kind, Span<uint> source, Span<uint> destination,
		ReadOnlySpan<uint> transform, uint sourceIdentity )
	{
		if ( source.Length < 24 || destination.Length < 44 )
			throw new ArgumentException( "Shape construction requires 24 source words and 44 destination words." );
		if ( !transform.IsEmpty && transform.Length < 16 )
			throw new ArgumentException( "A shape transform requires four complete rows.", nameof( transform ) );
		if ( kind == 6 )
			return false;
		if ( kind < 1 || kind > 5 )
			throw new ArgumentOutOfRangeException( nameof( kind ) );

		if ( kind == 3 )
		{
			Triangle( source, destination, transform );
			Metadata( source, destination, kind, sourceIdentity, 1, 3 );
			return true;
		}

		Point( CollisionWordVector.Read( source, 12 ), transform ).Write( destination, 0 );
		switch ( kind )
		{
			case 1:
				Metadata( source, destination, kind, sourceIdentity, 0, 0 );
				break;
			case 2:
				Direction( CollisionWordVector.Read( source, 8 ), transform ).Write( destination, 16 );
				destination[ 28 ] = source[ 17 ];
				Metadata( source, destination, kind, sourceIdentity, 0, 1 );
				break;
			case 4:
				for ( var axis = 0; axis < 3; ++axis )
				{
					var direction = Direction( CollisionWordVector.Read( source, axis * 4 ), transform );
					direction.Write( destination, 4 + axis * 4 );
					direction.Write( destination, 16 + axis * 4 );
					destination[ 28 + axis ] = source[ 17 + axis ];
				}
				destination[ 31 ] = 0;
				Metadata( source, destination, kind, sourceIdentity, 3, 3 );
				break;
			case 5:
				for ( var axis = 0; axis < 3; ++axis )
					Direction( CollisionWordVector.Read( source, (2 - axis) * 4 ), transform ).Write( destination, 4 + axis * 4 );
				CollisionWordVector.Read( destination, 4 ).Write( destination, 16 );
				destination[ 28 ] = source[ 17 ];
				destination[ 29 ] = source[ 18 ];
				Metadata( source, destination, kind, sourceIdentity, 1, 1 );
				break;
		}
		return true;
	}

	private static void Metadata( ReadOnlySpan<uint> source, Span<uint> destination,
		int kind, uint identity, uint faces, uint edges )
	{
		destination[ 32 ] = source[ 20 ];
		destination[ 33 ] = identity;
		destination[ 34 ] = 0;
		destination[ 35 ] = (faces << 24) | (edges << 16) | (destination[ 35 ] & 0xffffu);
		destination[ 36 ] = (uint)kind;
		destination[ 37 ] = source[ 23 ];
	}

	private static void Triangle( Span<uint> source, Span<uint> destination, ReadOnlySpan<uint> transform )
	{
		var a = CollisionWordVector.Read( source, 0 );
		var b = CollisionWordVector.Read( source, 4 );
		var c = CollisionWordVector.Read( source, 8 );
		if ( (source[ 23 ] & 2) != 0 )
		{
			source[ 23 ] &= ~2u;
			var normal = b.Subtract( a ).Cross( c.Subtract( a ) );
			var squared = normal.LengthSquared();
			var normalized = normal.Scale( InverseRoot( squared ) );
			if ( Greater( squared, 0u ) )
				normal = normalized;
			normal.Write( source, 12 );
		}
		var first = Point( a, transform );
		var second = Point( b, transform );
		var third = Point( c, transform );
		var transformedNormal = Direction( CollisionWordVector.Read( source, 12 ), transform );
		if ( !transform.IsEmpty )
		{
			first = first with { W = 0x3f800000u };
			second = second with { W = 0x3f800000u };
			third = third with { W = 0x3f800000u };
			transformedNormal = transformedNormal with { W = 0 };
		}
		first.Write( destination, 0 );
		second.Write( destination, 8 );
		third.Write( destination, 12 );
		transformedNormal.Write( destination, 4 );
		Edge( third.Subtract( first ), destination, 16, 28 );
		Edge( second.Subtract( third ), destination, 20, 29 );
		Edge( first.Subtract( second ), destination, 24, 30 );
		destination[ 31 ] = 0;
		destination[ 38 ] = source[ 17 ];
		destination[ 39 ] = source[ 18 ];
		destination[ 40 ] = source[ 19 ];
	}

	private static void Edge( CollisionWordVector edge, Span<uint> destination, int vectorOffset, int lengthOffset )
	{
		var inverseLength = InverseRoot( edge.LengthSquared() );
		edge.Scale( inverseLength ).Write( destination, vectorOffset );
		var estimate = Interpolate( inverseLength, ReciprocalEstimate( inverseLength ) );
		destination[ lengthOffset ] = MultiplyAdd( estimate,
			NegativeMultiplySubtract( estimate, inverseLength, 0x3f800000u ), estimate );
	}

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		return MultiplyAdd( Multiply( estimate, 0x3f000000u ),
			NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
	}

	private static CollisionWordVector Point( CollisionWordVector point, ReadOnlySpan<uint> transform )
	{
		if ( transform.IsEmpty )
			return point;
		var result = CollisionWordVector.Read( transform, 0 ).MultiplyAdd( point.X, CollisionWordVector.Read( transform, 12 ) );
		result = CollisionWordVector.Read( transform, 4 ).MultiplyAdd( point.Y, result );
		return CollisionWordVector.Read( transform, 8 ).MultiplyAdd( point.Z, result );
	}

	private static CollisionWordVector Direction( CollisionWordVector direction, ReadOnlySpan<uint> transform )
	{
		if ( transform.IsEmpty )
			return direction;
		var result = CollisionWordVector.Read( transform, 0 ).Scale( direction.X );
		result = CollisionWordVector.Read( transform, 4 ).MultiplyAdd( direction.Y, result );
		return CollisionWordVector.Read( transform, 8 ).MultiplyAdd( direction.Z, result );
	}
}
