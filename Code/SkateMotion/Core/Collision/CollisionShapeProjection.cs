using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

// Projection intervals describe the core feature geometry, before round-radius expansion.
internal static class CollisionShapeProjection
{
	internal static void Project( ReadOnlySpan<uint> shape, ReadOnlySpan<uint> direction, Span<uint> interval )
	{
		var kind = Kind( shape );
		if ( direction.Length < 4 || interval.Length < 8 )
			throw new ArgumentException( "A projection requires one direction row and two interval rows." );
		var bounds = Bounds( kind, shape, CollisionWordVector.Read( direction, 0 ), false );
		Write( interval, bounds.Minimum, bounds.Maximum );
	}

	internal static void ProjectBatch( ReadOnlySpan<uint> shape, ReadOnlySpan<uint> directions, Span<uint> intervals )
	{
		var kind = Kind( shape );
		if ( directions.Length % 4 != 0 || intervals.Length / 12 < directions.Length / 4 )
			throw new ArgumentException( "Batched projections require complete direction rows and three output rows per direction." );
		for ( var index = 0; index < directions.Length / 4; ++index )
		{
			var bounds = Bounds( kind, shape, CollisionWordVector.Read( directions, index * 4 ), true );
			Write( intervals[ (index * 12).. ], bounds.Minimum, bounds.Maximum );
		}
	}

	private static int Kind( ReadOnlySpan<uint> shape )
	{
		if ( shape.Length < 44 )
			throw new ArgumentException( "A projection requires a complete shape descriptor.", nameof( shape ) );
		var kind = shape[ 36 ];
		if ( kind < 1 || kind > 5 )
			throw new ArgumentOutOfRangeException( nameof( shape ), "Only primitive descriptors have projection intervals." );
		return (int)kind;
	}

	private static (uint Minimum, uint Maximum) Bounds( int kind, ReadOnlySpan<uint> shape,
		CollisionWordVector direction, bool batch )
	{
		var center = Dot( CollisionWordVector.Read( shape, 0 ), direction );
		if ( kind == 1 )
			return (center, center);
		if ( kind == 3 )
		{
			var second = Dot( CollisionWordVector.Read( shape, 8 ), direction );
			var third = Dot( CollisionWordVector.Read( shape, 12 ), direction );
			var greater = Greater( center, second );
			var maximum = greater ? center : second;
			var minimum = greater ? second : center;
			return (Greater( minimum, third ) ? third : minimum, Greater( maximum, third ) ? maximum : third);
		}
		uint extent;
		if ( kind == 2 )
		{
			var axial = Dot( CollisionWordVector.Read( shape, 16 ), direction );
			extent = Multiply( shape[ 28 ], Absolute( axial ) );
		}
		else if ( kind == 4 )
		{
			var x = BoxAxis( shape, direction, 4, 28, batch );
			var y = BoxAxis( shape, direction, 8, 29, batch );
			var z = BoxAxis( shape, direction, 12, 30, batch );
			extent = Add( Add( x, y ), z );
		}
		else
		{
			var axial = Dot( CollisionWordVector.Read( shape, 16 ), direction );
			var radialSquared = Absolute( NegativeMultiplySubtract( axial, axial, 0x3f800000u ) );
			var root = Multiply( radialSquared, RefinedInverseRoot( radialSquared ) );
			extent = Absolute( MultiplyAdd( Absolute( axial ), shape[ 28 ], Multiply( root, shape[ 29 ] ) ) );
		}
		return (Subtract( center, extent ), Add( center, extent ));
	}

	private static uint BoxAxis( ReadOnlySpan<uint> shape, CollisionWordVector direction,
		int axisOffset, int dimensionOffset, bool batch )
	{
		var axis = CollisionWordVector.Read( shape, axisOffset );
		// Batch queries scale each basis component first; scalar queries scale the dot product.
		return Absolute( batch
			? Dot( axis.Scale( shape[ dimensionOffset ] ), direction )
			: Multiply( Dot( axis, direction ), shape[ dimensionOffset ] ) );
	}

	private static uint Dot( CollisionWordVector a, CollisionWordVector b ) =>
		Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );

	private static uint RefinedInverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		for ( var iteration = 0; iteration < 2; ++iteration )
			estimate = MultiplyAdd( Multiply( estimate, 0x3f000000u ),
				NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
		return estimate;
	}

	private static uint Absolute( uint value ) => value & 0x7fffffffu;

	private static void Write( Span<uint> interval, uint minimum, uint maximum )
	{
		interval[ ..4 ].Fill( minimum );
		interval[ 4..8 ].Fill( maximum );
	}
}
