using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static class CollisionPolygonSegmentContacts
{
	internal static void Assemble( Span<uint> first, Span<uint> second,
		ReadOnlySpan<uint> direction, Span<uint> contacts )
	{
		if ( first.Length < 144 || second.Length < 144 || direction.Length < 4 || contacts.Length < 136 )
			throw new ArgumentException( "Polygon contacts require complete feature, direction and contact buffers." );
		var polygonFirst = first[ 140 ] is 3 or 4 or 8;
		if ( !(polygonFirst && second[ 140 ] == 1) && !(first[ 140 ] == 1 && second[ 140 ] is 3 or 4 or 8) )
			throw new ArgumentException( "Contact assembly requires one polygon and one segment." );
		var polygon = polygonFirst ? first : second;
		var segment = polygonFirst ? second : first;
		var polygonOffset = polygonFirst ? 0 : 64;
		var segmentOffset = polygonFirst ? 64 : 0;
		var normal = Read( direction, 0 );
		var start = Read( segment, 4 );
		var tangent = Read( segment, 8 );
		uint lower = 0;
		var upper = segment[ 16 ];
		var outside = false;
		var outsidePoint = default(CollisionWordVector);
		var outsideNormal = default(CollisionWordVector);
		for ( var edge = 0; edge < polygon[ 140 ]; ++edge )
		{
			var record = 4 + edge * 16;
			var across = normal.Cross( Read( polygon, record + 4 ) );
			var squared = across.LengthSquared();
			var inverse = InverseRoot( squared );
			across = across.Scale( inverse );
			if ( Greater( 0x3a83126eu, Multiply( squared, inverse ) ) )
				continue;
			var edgePoint = Read( polygon, record );
			var denominator = Dot( tangent, across );
			var numerator = Dot( edgePoint.Subtract( start ), across );
			if ( Greater( 0x3d4cccccu, denominator & 0x7fffffffu ) )
			{
				if ( Greater( numerator, 0 ) )
				{
					outside = true;
					outsidePoint = edgePoint;
					outsideNormal = across;
				}
				continue;
			}
			var distance = Divide( numerator, denominator );
			if ( Greater( denominator, 0 ) )
			{
				if ( Greater( distance, lower ) )
					lower = distance;
				if ( Greater( lower, upper ) )
				{
					lower = upper;
					outside = true;
					break;
				}
			}
			else
			{
				if ( Greater( upper, distance ) )
					upper = distance;
				if ( Greater( lower, upper ) )
				{
					upper = lower;
					outside = true;
					break;
				}
			}
		}
		if ( !outside )
		{
			contacts[ 132 ] = 2;
			var firstPoint = tangent.MultiplyAdd( lower, start );
			var secondPoint = tangent.MultiplyAdd( upper, start );
			firstPoint.Write( contacts, segmentOffset );
			secondPoint.Write( contacts, segmentOffset + 4 );
			IntersectPlane( polygon, firstPoint, normal ).Write( contacts, polygonOffset );
			IntersectPlane( polygon, secondPoint, normal ).Write( contacts, polygonOffset + 4 );
			return;
		}
		var width = Subtract( upper, lower );
		if ( !Greater( width, 0x3a83126eu ) )
		{
			contacts[ 132 ] = 1;
			var point = tangent.MultiplyAdd( lower, start );
			point = normal.MultiplyAdd( Dot( normal, Read( polygon, 4 ).Subtract( point ) ), point );
			var classification = ClampPolygon( polygon, normal, ref point );
			polygon[ 0 ] = unchecked( polygon[ 0 ] + classification );
			point.Write( contacts, polygonOffset );
			CollisionPointContacts.ProjectSegment( segment, point ).Write( contacts, segmentOffset );
			return;
		}
		OutsideEdge( polygon, segment, normal, contacts, polygonOffset, segmentOffset,
			lower, width, outsidePoint, outsideNormal );
	}

	private static void OutsideEdge( Span<uint> polygon, Span<uint> segment, CollisionWordVector normal,
		Span<uint> contacts, int polygonOffset, int segmentOffset, uint lower, uint width,
		CollisionWordVector outsidePoint, CollisionWordVector outsideNormal )
	{
		contacts[ 132 ] = 2;
		var start = Read( segment, 4 );
		var tangent = Read( segment, 8 );
		var projected = normal.MultiplyAdd( Dot( normal, Read( polygon, 4 ).Subtract( start ) ), start );
		projected = outsideNormal.MultiplyAdd( Dot( outsideNormal, outsidePoint.Subtract( projected ) ), projected );
		var firstPoint = tangent.MultiplyAdd( lower, projected );
		var secondPoint = tangent.MultiplyAdd( width, firstPoint );
		var firstClass = ClampPolygon( polygon, normal, ref firstPoint );
		var secondClass = ClampPolygon( polygon, normal, ref secondPoint );
		polygon[ 0 ] = unchecked( polygon[ 0 ] + (Math.Max( firstClass, secondClass ) & 0xfffffffeu) );
		firstPoint.Write( contacts, polygonOffset );
		secondPoint.Write( contacts, polygonOffset + 4 );
		var firstOnSegment = ClampSegment( segment, firstPoint );
		firstOnSegment.Write( contacts, segmentOffset );
		ClampSegment( segment, secondPoint ).Write( contacts, segmentOffset + 4 );
		var delta = firstPoint.Subtract( firstOnSegment );
		var squared = delta.LengthSquared();
		if ( !Greater( Multiply( squared, InverseRoot( squared ) ), 0x007fffffu ) )
			return;
		contacts[ 133 ] = 1;
		var adjustedNormal = tangent.Cross( delta.Cross( tangent ) );
		adjustedNormal.Scale( InverseRoot( adjustedNormal.LengthSquared() ) ).Write( contacts, 128 );
	}

	private static uint ClampPolygon( ReadOnlySpan<uint> polygon, CollisionWordVector direction, ref CollisionWordVector point )
	{
		var selected = -1;
		uint minimum = 0;
		var selectedNormal = default(CollisionWordVector);
		for ( var edge = 0; edge < polygon[ 140 ]; ++edge )
		{
			var record = 4 + edge * 16;
			var across = direction.Cross( Read( polygon, record + 4 ) );
			var squared = across.LengthSquared();
			if ( Greater( squared, 0x34000000u ) )
				across = across.Scale( InverseRoot( squared ) );
			var distance = Dot( across, point.Subtract( Read( polygon, record ) ) );
			if ( selected < 0 || Greater( minimum, distance ) )
			{
				selected = edge;
				minimum = distance;
				selectedNormal = across;
			}
		}
		if ( !Greater( 0, minimum ) )
			return 0;
		point = new( NegativeMultiplySubtract( selectedNormal.X, minimum, point.X ),
			NegativeMultiplySubtract( selectedNormal.Y, minimum, point.Y ),
			NegativeMultiplySubtract( selectedNormal.Z, minimum, point.Z ),
			NegativeMultiplySubtract( selectedNormal.W, minimum, point.W ) );
		var edgeRecord = 4 + selected * 16;
		var start = Read( polygon, edgeRecord );
		var tangent = Read( polygon, edgeRecord + 4 );
		var distanceAlong = Dot( point.Subtract( start ), tangent );
		uint classification;
		if ( Greater( 0, distanceAlong ) )
		{
			point = start;
			classification = 1;
		}
		else if ( Greater( distanceAlong, polygon[ edgeRecord + 12 ] ) )
		{
			point = tangent.MultiplyAdd( polygon[ edgeRecord + 12 ], start );
			classification = 3;
		}
		else
		{
			point = tangent.MultiplyAdd( distanceAlong, start );
			classification = 2;
		}
		return (uint)(selected * 2) + classification;
	}

	private static CollisionWordVector ClampSegment( ReadOnlySpan<uint> segment, CollisionWordVector point )
	{
		var start = Read( segment, 4 );
		var tangent = Read( segment, 8 );
		var distance = Dot( point.Subtract( start ), tangent );
		if ( Greater( 0, distance ) )
			return start;
		return tangent.MultiplyAdd( Greater( distance, segment[ 16 ] ) ? segment[ 16 ] : distance, start );
	}

	private static CollisionWordVector IntersectPlane( ReadOnlySpan<uint> polygon, CollisionWordVector point, CollisionWordVector direction )
	{
		var normal = Read( polygon, 132 );
		var distance = Divide( Dot( Read( polygon, 4 ).Subtract( point ), normal ), Dot( direction, normal ) );
		return direction.MultiplyAdd( distance, point );
	}

	private static uint Divide( uint numerator, uint denominator )
	{
		var estimate = Interpolate( denominator, ReciprocalEstimate( denominator ) );
		var initial = Multiply( numerator, estimate );
		return MultiplyAdd( NegativeMultiplySubtract( initial, denominator, numerator ), estimate, initial );
	}

	private static uint InverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < 2; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}

	private static CollisionWordVector Read( ReadOnlySpan<uint> buffer, int offset ) => CollisionWordVector.Read( buffer, offset );
	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
}
