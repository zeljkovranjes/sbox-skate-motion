using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionShapeFeatures
{
	private static void Cylinder( ReadOnlySpan<uint> shape, CollisionWordVector direction, bool firstSide, Span<uint> feature )
	{
		var x = R( shape, 3 );
		var y = R( shape, 2 );
		var z = R( shape, 1 );
		var local = new CollisionWordVector( FrameDot( x, direction ), FrameDot( y, direction ), FrameDot( z, direction ), 0 );
		var half = shape[ 28 ];
		var radius = shape[ 29 ];
		if ( Greater( Abs( local.Z ), 0x3f7ff2e4u ) )
			CylinderCap( local.Z, half, radius, firstSide, feature );
		else if ( Greater( Abs( local.Z ), 0x3ca3d70au ) )
		{
			var radial = local with { Z = 0 };
			var point = radial.Scale( InverseRoot( radial.LengthSquared(), 2 ) ).Scale( radius );
			point = point with { Z = Greater( 0, local.Z ) ? half ^ 0x80000000u : half };
			Point( point, feature );
			local.Write( feature, 132 );
		}
		else
		{
			var point = local.Scale( InverseRoot( local.LengthSquared(), 2 ) ).Scale( radius ) with { Z = half };
			var end = point with { Z = half ^ 0x80000000u };
			var delta = end.Subtract( point );
			var reverseSquared = point.Subtract( end ).LengthSquared();
			var tangent = delta.Scale( InverseRoot( delta.LengthSquared(), 2 ) );
			WriteEdge( feature, 0, point, tangent, new( 0x3ca3d70au, 0x3ca3d70au, 0x3ca3d70au, 0x3ca3d70au ),
				Multiply( reverseSquared, InverseRoot( reverseSquared, 2 ) ) );
			feature[ 140 ] = 1;
			local.Write( feature, 132 );
		}
		TransformDirection( x, y, z, R( feature, 33 ) ).Write( feature, 132 );
		TransformPoint( shape, R( feature, 34 ) ).Write( feature, 136 );
		for ( var i = 0; i < feature[ 140 ]; ++i )
		{
			var row = 1 + i * 4;
			TransformPoint( shape, R( feature, row ) ).Write( feature, row * 4 );
			TransformDirection( x, y, z, R( feature, row + 1 ) ).Write( feature, (row + 1) * 4 );
			TransformDirection( x, y, z, R( feature, row + 2 ) ).Write( feature, (row + 2) * 4 );
		}
	}

	private static void CylinderCap( uint axial, uint half, uint radius, bool firstSide, Span<uint> feature )
	{
		var normal = new CollisionWordVector( 0, 0, 0x3f800000u, 0 );
		if ( Greater( 0, axial ) )
		{
			Negate( normal ).Write( feature, 132 );
			half ^= 0x80000000u;
			firstSide = !firstSide;
		}
		else
			normal.Write( feature, 132 );
		var diagonal = Multiply( radius, 0x3f3504f3u );
		var negativeRadius = radius ^ 0x80000000u;
		var negativeDiagonal = diagonal ^ 0x80000000u;
		Span<CollisionWordVector> ring = stackalloc CollisionWordVector[]
		{
			new( 0, radius, half, 0 ), new( diagonal, diagonal, half, 0 ),
			new( radius, 0, half, 0 ), new( diagonal, negativeDiagonal, half, 0 ),
			new( 0, negativeRadius, half, 0 ), new( negativeDiagonal, negativeDiagonal, half, 0 ),
			new( negativeRadius, 0, half, 0 ), new( negativeDiagonal, diagonal, half, 0 )
		};
		for ( var i = 0; i < 8; ++i )
		{
			var point = ring[ firstSide ? 7 - i : i ];
			var next = ring[ firstSide ? (14 - i) & 7 : (i + 1) & 7 ];
			var delta = next.Subtract( point );
			var cross = firstSide ? delta.Cross( normal ) : normal.Cross( delta );
			var squared = delta.LengthSquared();
			var length = Multiply( squared, InverseRoot( squared, 2 ) );
			var reciprocal = Reciprocal( length );
			WriteEdge( feature, i, point, delta.Scale( reciprocal ), cross.Scale( reciprocal ), length );
		}
		feature[ 140 ] = 8;
	}

	private static uint FrameDot( CollisionWordVector axis, CollisionWordVector direction ) =>
		MultiplyAdd( axis.Z, direction.Z, MultiplyAdd( axis.Y, direction.Y, Multiply( axis.X, direction.X ) ) );

	private static CollisionWordVector TransformDirection( CollisionWordVector x, CollisionWordVector y,
		CollisionWordVector z, CollisionWordVector value ) => z.MultiplyAdd( value.Z, y.MultiplyAdd( value.Y, x.Scale( value.X ) ) );

	private static CollisionWordVector TransformPoint( ReadOnlySpan<uint> shape, CollisionWordVector value ) =>
		R( shape, 1 ).MultiplyAdd( value.Z, R( shape, 2 ).MultiplyAdd( value.Y, R( shape, 3 ).MultiplyAdd( value.X, R( shape, 0 ) ) ) );
}
