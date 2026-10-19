using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionShapeFeatures
{
	private static void Box( ReadOnlySpan<uint> shape, CollisionWordVector direction, bool firstSide,
		uint descriptorAddress, Span<uint> feature )
	{
		var near = 0;
		var nearAxis = 0;
		var farAxis = 0;
		for ( var i = 0; i < 3; ++i )
		{
			if ( Greater( 0x3e4cccccu, Abs( Dot( direction, R( shape, 1 + i ) ) ) ) )
			{
				near++;
				nearAxis = i;
			}
			else
				farAxis = i;
		}
		if ( near == 2 )
			BoxFace( shape, direction, farAxis, firstSide, feature );
		else if ( near == 1 )
		{
			var u = nearAxis == 0 ? 1 : 0;
			var v = nearAxis == 2 ? 1 : 2;
			var center = BoxExtreme( shape, direction, u, v, false );
			var offset = R( shape, nearAxis + 1 ).Scale( shape[ 28 + nearAxis ] );
			var addressOffset = (uint)nearAxis * 16;
			Segment( center.Subtract( offset ), Sum( center, offset ),
				new( descriptorAddress + addressOffset, addressOffset, addressOffset, addressOffset ), feature );
		}
		else
			Point( BoxExtreme( shape, direction, 1, 2, true ), feature );
		feature[ 0 ] = 0;
	}

	private static CollisionWordVector BoxExtreme( ReadOnlySpan<uint> shape, CollisionWordVector direction,
		int u, int v, bool corners )
	{
		var selected = default( CollisionWordVector );
		uint maximum = 0;
		for ( var i = 0; i < (corners ? 8 : 4); ++i )
		{
			var point = R( shape, 0 );
			if ( corners )
				point = R( shape, 1 ).MultiplyAdd( Multiply( i > 3 ? 0x3f800000u : 0xbf800000u, shape[ 28 ] ), point );
			var corner = i & 3;
			point = R( shape, u + 1 ).MultiplyAdd( Multiply( corner < 2 ? 0x3f800000u : 0xbf800000u, shape[ 28 + u ] ), point );
			point = R( shape, v + 1 ).MultiplyAdd( Multiply( corner == 0 || corner == 3 ? 0x3f800000u : 0xbf800000u, shape[ 28 + v ] ), point );
			var projection = Dot( point, direction );
			if ( i == 0 || Greater( projection, maximum ) )
			{
				maximum = projection;
				selected = point;
			}
		}
		return selected;
	}

	private static void BoxFace( ReadOnlySpan<uint> shape, CollisionWordVector direction,
		int axis, bool firstSide, Span<uint> feature )
	{
		var u = axis == 0 ? 1 : 0;
		var v = axis == 2 ? 1 : 2;
		var axisVector = R( shape, axis + 1 );
		var uVector = R( shape, u + 1 );
		var vVector = R( shape, v + 1 );
		var sign = Greater( Dot( axisVector, direction ), 0 ) ? 0x3f800000u : 0xbf800000u;
		var center = axisVector.MultiplyAdd( Multiply( sign, shape[ 28 + axis ] ), R( shape, 0 ) );
		var orientation = axis == 1 ? sign ^ 0x80000000u : sign;
		var crossV = firstSide ? vVector.Cross( direction ) : direction.Cross( vVector );
		var crossU = firstSide ? uVector.Cross( direction ) : direction.Cross( uVector );
		crossV = NormalizeFace( crossV );
		crossU = NormalizeFace( crossU );
		Span<CollisionWordVector> offsetsU = stackalloc CollisionWordVector[ 2 ];
		Span<CollisionWordVector> offsetsV = stackalloc CollisionWordVector[ 2 ];
		offsetsU[ 0 ] = uVector.Scale( shape[ 28 + u ] );
		offsetsU[ 1 ] = uVector.Scale( shape[ 28 + u ] ^ 0x80000000u );
		offsetsV[ 0 ] = vVector.Scale( shape[ 28 + v ] );
		offsetsV[ 1 ] = vVector.Scale( shape[ 28 + v ] ^ 0x80000000u );
		Span<CollisionWordVector> tangents = stackalloc CollisionWordVector[] { Negate( vVector ), Negate( uVector ), vVector, uVector };
		Span<CollisionWordVector> auxiliaries = stackalloc CollisionWordVector[] { Negate( crossV ), Negate( crossU ), crossV, crossU };
		var forward = Greater( 0, orientation ) == firstSide;
		for ( var i = 0; i < 4; ++i )
		{
			var corner = forward ? i : (i + 1) & 3;
			var tangent = forward ? i : (i + 2) & 3;
			var destination = forward ? i : 3 - i;
			var point = Sum( Sum( center, offsetsU[ corner < 2 ? 0 : 1 ] ), offsetsV[ corner == 0 || corner == 3 ? 0 : 1 ] );
			var length = Multiply( shape[ 28 + ((i & 1) == 0 ? v : u) ], 0x40000000u );
			WriteEdge( feature, destination, point, tangents[ tangent ], auxiliaries[ tangent ], length );
		}
		axisVector.Scale( orientation ^ 0x80000000u ).Write( feature, 132 );
		feature[ 140 ] = 4;
	}

	private static CollisionWordVector NormalizeFace( CollisionWordVector value )
	{
		var squared = value.LengthSquared();
		return Greater( squared, 0x34000000u ) ? value.Scale( InverseRoot( squared, 2 ) ) : value;
	}
}
