using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

// Decoded vertices retain all four lanes; the supplied affine rows define their frame.
internal static class CollisionWorldTriangleConstruction
{
	internal static void Construct( ReadOnlySpan<uint> vertices, ReadOnlySpan<uint> transform,
		Span<uint> destination, ushort group, ushort surface, uint shapeIdentity,
		ReadOnlySpan<byte> authoredEdges = default, uint callerFlags = 0 )
	{
		if ( vertices.Length < 12 || transform.Length < 16 || destination.Length < 44 )
			throw new ArgumentException( "Triangle construction requires 12 vertex, 16 transform and 44 destination words." );
		if ( !authoredEdges.IsEmpty && authoredEdges.Length != 3 )
			throw new ArgumentException( "Authored triangle edges require exactly three codes.", nameof( authoredEdges ) );

		var first = Transform( CollisionWordVector.Read( vertices, 0 ), transform );
		var second = Transform( CollisionWordVector.Read( vertices, 4 ), transform );
		var third = Transform( CollisionWordVector.Read( vertices, 8 ), transform );
		var normal = second.Subtract( first ).Cross( third.Subtract( first ) );
		var squared = normal.LengthSquared();
		var inverse = InverseRoot( squared );
		inverse = RefineRoot( squared, inverse );
		first.Write( destination, 0 );
		normal.Scale( inverse ).Write( destination, 4 );
		second.Write( destination, 8 );
		third.Write( destination, 12 );
		Edge( third.Subtract( first ), destination, 16, 28 );
		Edge( second.Subtract( third ), destination, 20, 29 );
		Edge( first.Subtract( second ), destination, 24, 30 );
		destination[ 31 ] = 0;
		destination[ 32 ] = 0;
		destination[ 33 ] = shapeIdentity;
		destination[ 34 ] = group | ((uint)surface << 16);
		destination[ 35 ] = 0x01030000u | (destination[ 35 ] & 0xffffu);
		destination[ 36 ] = 3;
		destination[ 37 ] = authoredEdges.IsEmpty ? 0x1e0u : EdgeFlags( authoredEdges, callerFlags );
		for ( var edge = 0; edge < 3; ++edge )
			destination[ 38 + edge ] = authoredEdges.IsEmpty ? 0u : EdgeCosine( authoredEdges[ edge ] );
	}

	private static CollisionWordVector Transform( CollisionWordVector point, ReadOnlySpan<uint> transform )
	{
		var result = CollisionWordVector.Read( transform, 0 ).MultiplyAdd( point.X, CollisionWordVector.Read( transform, 12 ) );
		result = CollisionWordVector.Read( transform, 4 ).MultiplyAdd( point.Y, result );
		return CollisionWordVector.Read( transform, 8 ).MultiplyAdd( point.Z, result );
	}

	private static uint InverseRoot( uint squared ) =>
		RefineRoot( squared, Interpolate( squared, RootEstimate( squared ) ) );

	private static uint RefineRoot( uint squared, uint estimate ) =>
		MultiplyAdd( Multiply( estimate, 0x3f000000u ),
			NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );

	private static void Edge( CollisionWordVector edge, Span<uint> destination, int vectorOffset, int lengthOffset )
	{
		var inverseLength = InverseRoot( edge.LengthSquared() );
		edge.Scale( inverseLength ).Write( destination, vectorOffset );
		var estimate = Interpolate( inverseLength, ReciprocalEstimate( inverseLength ) );
		destination[ lengthOffset ] = MultiplyAdd( estimate,
			NegativeMultiplySubtract( estimate, inverseLength, 0x3f800000u ), estimate );
	}

	private static uint EdgeCosine( byte code )
	{
		// The packed exponent is shifted as an integer before its signed conversion.
		var divisor = Word( (float)unchecked((int)(8u << (code & 31))) );
		var estimate = Interpolate( divisor, ReciprocalEstimate( divisor ) );
		var quotient = Multiply( 0x411de9e5u, estimate );
		quotient = MultiplyAdd( NegativeMultiplySubtract( quotient, divisor, 0x411de9e5u ), estimate, quotient );
		return Subtract( 0x3f800000u, quotient );
	}

	private static uint EdgeFlags( ReadOnlySpan<byte> edges, uint callerFlags ) =>
		((uint)(edges[ 2 ] & 0x20) << 2) | ((uint)(edges[ 1 ] & 0x40) << 4)
		| (callerFlags & 0x10) | (uint)(edges[ 0 ] & 0x20) | ((uint)(edges[ 1 ] & 0x20) << 1)
		| ((uint)(edges[ 0 ] & 0x40) << 3) | ((uint)(edges[ 2 ] & 0x40) << 5) | 0x101u;
}
