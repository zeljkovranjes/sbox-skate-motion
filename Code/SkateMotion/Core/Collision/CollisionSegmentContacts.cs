using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static class CollisionSegmentContacts
{
	internal static void Assemble( Span<uint> first, Span<uint> second,
		ReadOnlySpan<uint> direction, Span<uint> contacts )
	{
		if ( first.Length < 144 || second.Length < 144 || direction.Length < 4 || contacts.Length < 136 )
			throw new ArgumentException( "Segment contacts require complete feature, direction and contact buffers." );
		if ( first[ 140 ] != 1 || second[ 140 ] != 1 )
			throw new ArgumentException( "Both features must be segments." );
		var a = Read( first, 4 );
		var b = Read( second, 4 );
		var u = Read( first, 8 );
		var v = Read( second, 8 );
		var cross = u.Cross( v );
		var squared = cross.LengthSquared();
		if ( Greater( squared, 0x34000000u ) )
		{
			var perpendicular = cross.Scale( InverseRoot( squared ) ).Cross( v );
			var denominator = Dot( u, perpendicular );
			var numerator = Dot( b.Subtract( a ), perpendicular );
			var reciprocal = Interpolate( denominator, ReciprocalEstimate( denominator ) );
			var initial = Multiply( numerator, reciprocal );
			var along = MultiplyAdd( NegativeMultiplySubtract( initial, denominator, numerator ), reciprocal, initial );
			if ( Greater( 0, along ) )
				along = 0;
			if ( Greater( along, first[ 16 ] ) )
				along = first[ 16 ];
			var secondPoint = CollisionPointContacts.ProjectSegment( second, u.MultiplyAdd( along, a ) );
			var firstPoint = CollisionPointContacts.ProjectSegment( first, secondPoint );
			firstPoint.Write( contacts, 0 );
			secondPoint.Write( contacts, 64 );
			contacts[ 132 ] = 1;
			return;
		}
		Parallel( first, second, Read( direction, 0 ), contacts );
	}

	private static void Parallel( Span<uint> first, Span<uint> second, CollisionWordVector normal, Span<uint> contacts )
	{
		var a = Read( first, 4 );
		var b = Read( second, 4 );
		var u = Read( first, 8 );
		var v = Read( second, 8 );
		var aEnd = u.MultiplyAdd( first[ 16 ], a );
		var bEnd = v.MultiplyAdd( second[ 16 ], b );
		var axis = Greater( Abs( Dot( normal, v ) ), Abs( Dot( normal, u ) ) ) ? u : v;
		var aStartProjection = Dot( axis, a );
		var aEndProjection = Dot( axis, aEnd );
		var bStartProjection = Dot( axis, b );
		var bEndProjection = Dot( axis, bEnd );
		var aForward = Greater( aEndProjection, aStartProjection );
		var bForward = Greater( bEndProjection, bStartProjection );
		var aMaximum = aForward ? aEndProjection : aStartProjection;
		var aMinimum = aForward ? aStartProjection : aEndProjection;
		var bMaximum = bForward ? bEndProjection : bStartProjection;
		var bMinimum = bForward ? bStartProjection : bEndProjection;
		var upper = Greater( bMaximum, aMaximum ) ? aMaximum : bMaximum;
		var lower = Greater( aMinimum, bMinimum ) ? aMinimum : bMinimum;
		if ( !Greater( upper, lower ) )
		{
			var secondAfter = Greater( bMinimum, aMinimum );
			var useFirstStart = secondAfter
				? Greater( aStartProjection, aEndProjection ) : Greater( aEndProjection, aStartProjection );
			var useSecondStart = secondAfter
				? Greater( bEndProjection, bStartProjection ) : Greater( bStartProjection, bEndProjection );
			(useFirstStart ? a : aEnd).Write( contacts, 0 );
			(useSecondStart ? b : bEnd).Write( contacts, 64 );
			first[ 0 ] = unchecked( first[ 0 ] + (useFirstStart ? uint.MaxValue : 1u) );
			second[ 0 ] = unchecked( second[ 0 ] + (useSecondStart ? uint.MaxValue : 1u) );
			contacts[ 132 ] = 1;
			return;
		}
		var firstPlane = normal.Cross( u ).Cross( u );
		var secondPlane = normal.Cross( v ).Cross( v );
		firstPlane = firstPlane.Scale( InverseRoot( firstPlane.LengthSquared() ) );
		secondPlane = secondPlane.Scale( InverseRoot( secondPlane.LengthSquared() ) );
		var start = axis.Scale( lower );
		var end = axis.Scale( upper );
		ProjectLine( IntersectPlane( start, normal, a, firstPlane ), a, u ).Write( contacts, 0 );
		ProjectLine( IntersectPlane( end, normal, a, firstPlane ), a, u ).Write( contacts, 4 );
		ProjectLine( IntersectPlane( start, normal, b, secondPlane ), b, v ).Write( contacts, 64 );
		ProjectLine( IntersectPlane( end, normal, b, secondPlane ), b, v ).Write( contacts, 68 );
		contacts[ 132 ] = 2;
	}

	private static CollisionWordVector IntersectPlane( CollisionWordVector point, CollisionWordVector direction,
		CollisionWordVector planePoint, CollisionWordVector planeNormal )
	{
		var denominator = Dot( direction, planeNormal );
		var numerator = Dot( planePoint.Subtract( point ), planeNormal );
		var estimate = Interpolate( denominator, ReciprocalEstimate( denominator ) );
		var reciprocal = MultiplyAdd( estimate, NegativeMultiplySubtract( estimate, denominator, 0x3f800000u ), estimate );
		return direction.MultiplyAdd( Multiply( reciprocal, numerator ), point );
	}

	private static CollisionWordVector ProjectLine( CollisionWordVector point, CollisionWordVector start, CollisionWordVector direction ) =>
		direction.MultiplyAdd( Dot( point.Subtract( start ), direction ), start );

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < 2; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}

	private static uint Abs( uint value ) => value & 0x7fffffffu;
	private static CollisionWordVector Read( ReadOnlySpan<uint> buffer, int offset ) => CollisionWordVector.Read( buffer, offset );
	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
}
