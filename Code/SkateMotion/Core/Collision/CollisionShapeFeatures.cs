using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

// Packed feature records preserve unused words and descriptor identities for subsequent dispatch.
internal static partial class CollisionShapeFeatures
{
	internal static void Select( ReadOnlySpan<uint> shape, ReadOnlySpan<uint> direction,
		bool firstSide, uint descriptorAddress, Span<uint> feature )
	{
		if ( shape.Length < 44 || direction.Length < 4 || feature.Length < 144 )
			throw new ArgumentException( "Feature selection requires a complete shape, direction and feature buffer." );
		var query = R( direction, 0 );
		switch ( shape[ 36 ] )
		{
			case 1:
				Point( R( shape, 0 ), feature );
				feature[ 0 ] = 0;
				break;
			case 2: Capsule( shape, query, feature ); break;
			case 3: Triangle( shape, query, firstSide, feature ); break;
			case 4: Box( shape, query, firstSide, descriptorAddress, feature ); break;
			case 5: Cylinder( shape, query, firstSide, feature ); break;
			default: throw new ArgumentOutOfRangeException( nameof( shape ), "Only primitive descriptors have feature selectors." );
		}
	}

	private static void Capsule( ReadOnlySpan<uint> shape, CollisionWordVector direction, Span<uint> feature )
	{
		var center = R( shape, 0 );
		var axis = R( shape, 4 );
		var projection = Dot( axis, direction );
		if ( Greater( 0x3d4cccccu, Abs( projection ) ) )
		{
			var offset = axis.Scale( shape[ 28 ] );
			var start = Sum( center, offset );
			var end = center.Subtract( offset );
			Segment( start, end, center, feature );
		}
		else
		{
			var point = Greater( projection, 0 )
				? axis.MultiplyAdd( shape[ 28 ], center )
				: new CollisionWordVector( NegativeMultiplySubtract( axis.X, shape[ 28 ], center.X ),
					NegativeMultiplySubtract( axis.Y, shape[ 28 ], center.Y ),
					NegativeMultiplySubtract( axis.Z, shape[ 28 ], center.Z ),
					NegativeMultiplySubtract( axis.W, shape[ 28 ], center.W ) );
			Point( point, feature );
		}
		feature[ 0 ] = 0;
	}

	private static void Triangle( ReadOnlySpan<uint> shape, CollisionWordVector direction, bool firstSide, Span<uint> feature )
	{
		var normal = R( shape, 1 );
		var normalDot = Dot( normal, direction );
		if ( Greater( Abs( normalDot ), 0x3f733333u ) )
		{
			var forward = Greater( 0, normalDot ) == firstSide;
			for ( var i = 0; i < 3; ++i )
			{
				var edge = forward ? i : 2 - i;
				var point = i == 0 ? 0 : forward ? (i == 1 ? 3 : 2) : i + 1;
				var tangent = R( shape, 4 + edge );
				WriteEdge( feature, i, R( shape, point ), forward ? tangent : Negate( tangent ),
					default, shape[ 28 + edge ] );
			}
			feature[ 0 ] = forward ? 8u : 0u;
			feature[ 140 ] = 3;
			normal.Write( feature, 132 );
		}
		else
		{
			var a = Dot( R( shape, 0 ), direction );
			var b = Dot( R( shape, 2 ), direction );
			var c = Dot( R( shape, 3 ), direction );
			var first = Abs( Dot( R( shape, 4 ), direction ) );
			var second = Abs( Dot( R( shape, 5 ), direction ) );
			var third = Abs( Dot( R( shape, 6 ), direction ) );
			int edge, point;
			if ( Greater( a, b ) && Greater( a, c ) )
			{
				edge = Greater( 0x3d4cccccu, first ) && Greater( third, first ) ? 0
					: Greater( 0x3d4cccccu, third ) ? 2 : -1;
				point = 0;
			}
			else if ( !Greater( a, b ) && Greater( b, c ) )
			{
				edge = Greater( 0x3d4cccccu, second ) && Greater( third, second ) ? 1
					: Greater( 0x3d4cccccu, third ) ? 2 : -1;
				point = 2;
			}
			else
			{
				edge = Greater( 0x3d4cccccu, first ) && Greater( second, first ) ? 0
					: Greater( 0x3d4cccccu, second ) ? 1 : -1;
				point = 3;
			}
			if ( edge < 0 )
				Point( R( shape, point ), feature );
			else
			{
				WriteEdge( feature, 0, R( shape, edge == 0 ? 0 : edge == 1 ? 3 : 2 ),
					R( shape, 4 + edge ), default, shape[ 28 + edge ] );
				feature[ 140 ] = 1;
			}
		}
		var outward = firstSide ? direction : Negate( direction );
		for ( var i = 0; i < feature[ 140 ]; ++i )
		{
			var cross = R( feature, 2 + i * 4 ).Cross( outward );
			var squared = cross.LengthSquared();
			cross.Scale( Greater( squared, 0 ) ? InverseRoot( squared, 1 ) : 0 ).Write( feature, 12 + i * 16 );
		}
	}

	private static void Point( CollisionWordVector point, Span<uint> feature )
	{
		point.Write( feature, 136 );
		feature[ 140 ] = 0;
	}

	private static void Segment( CollisionWordVector start, CollisionWordVector end,
		CollisionWordVector auxiliary, Span<uint> feature )
	{
		var delta = end.Subtract( start );
		var squared = delta.LengthSquared();
		var length = Greater( squared, 0 ) ? Multiply( squared, InverseRoot( squared, 1 ) ) : 0;
		var tangent = Greater( squared, 0 ) ? delta.Scale( Reciprocal( length ) ) : default;
		WriteEdge( feature, 0, start, tangent, auxiliary, length );
		feature[ 140 ] = 1;
	}

	private static void WriteEdge( Span<uint> feature, int index, CollisionWordVector point,
		CollisionWordVector tangent, CollisionWordVector auxiliary, uint length )
	{
		var offset = 4 + index * 16;
		point.Write( feature, offset );
		tangent.Write( feature, offset + 4 );
		auxiliary.Write( feature, offset + 8 );
		feature.Slice( offset + 12, 4 ).Fill( length );
	}

	private static CollisionWordVector R( ReadOnlySpan<uint> values, int row ) => CollisionWordVector.Read( values, row * 4 );
	private static uint Abs( uint value ) => value & 0x7fffffffu;
	private static CollisionWordVector Negate( CollisionWordVector value ) =>
		new( value.X ^ 0x80000000u, value.Y ^ 0x80000000u, value.Z ^ 0x80000000u, value.W ^ 0x80000000u );
	private static CollisionWordVector Sum( CollisionWordVector a, CollisionWordVector b ) =>
		new( Add( a.X, b.X ), Add( a.Y, b.Y ), Add( a.Z, b.Z ), Add( a.W, b.W ) );
	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
	private static uint InverseRoot( uint squared, int refinements )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var i = 0; i < refinements; ++i )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}
	private static uint Reciprocal( uint value )
	{
		var estimate = Interpolate( value, ReciprocalEstimate( value ) );
		return MultiplyAdd( estimate, NegativeMultiplySubtract( estimate, value, 0x3f800000u ), estimate );
	}
}
