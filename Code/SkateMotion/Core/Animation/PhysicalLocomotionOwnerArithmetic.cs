namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using A = PhysicalCadenceArithmetic;
using C = SkateMotion.Core.Collision.CollisionArithmetic;

internal static class PhysicalLocomotionOwnerArithmetic
{
	internal static Vector3 Scale( Vector3 value, float factor ) => new(
		A.Multiply( value.X, factor ), A.Multiply( value.Y, factor ), A.Multiply( value.Z, factor ) );

	internal static Vector3 Add( Vector3 left, Vector3 right ) => new(
		A.Add( left.X, right.X ), A.Add( left.Y, right.Y ), A.Add( left.Z, right.Z ) );

	internal static Vector3 AddScaled( Vector3 value, float factor, Vector3 add ) => new(
		A.MultiplyAdd( value.X, factor, add.X ), A.MultiplyAdd( value.Y, factor, add.Y ),
		A.MultiplyAdd( value.Z, factor, add.Z ) );

	internal static Vector3 Cross( Vector3 left, Vector3 right ) => new(
		A.NegativeMultiplySubtract( right.Y, left.Z, A.Multiply( left.Y, right.Z ) ),
		A.NegativeMultiplySubtract( right.Z, left.X, A.Multiply( left.Z, right.X ) ),
		A.NegativeMultiplySubtract( right.X, left.Y, A.Multiply( left.X, right.Y ) ) );

	internal static Vector3 Normalize( Vector3 value )
	{
		var squared = A.Dot( value, value );
		var inverse = C.Value( C.Interpolate( C.Word( squared ), C.RootEstimate( C.Word( squared ) ) ) );
		for ( var index = 0; index < 2; index++ )
			inverse = A.MultiplyAdd( A.Multiply( inverse, .5f ),
				A.NegativeMultiplySubtract( squared, A.Multiply( inverse, inverse ), 1f ), inverse );
		return Scale( value, inverse );
	}

	internal static float Clamp( float value, float lower, float upper )
	{
		var aboveLower = A.Negative( A.Subtract( lower, value ) ) ? value : lower;
		return A.Negative( A.Subtract( upper, aboveLower ) ) ? upper : aboveLower;
	}

	internal static float ArcSine( float value )
	{
		var magnitude = A.Abs( value );
		var high = A.Greater( magnitude, .5f );
		var reduced = high ? A.MultiplyAdd( magnitude, -.5f, .5f ) : A.Multiply( magnitude, magnitude );
		var numerator = A.MultiplyAdd( reduced, A.Value( 0xbf012065 ), A.Value( 0x3f6f163f ) );
		var denominator = A.MultiplyAdd( A.Add( reduced, A.Value( 0xc0b18d0b ) ), reduced, A.Value( 0x40b350b7 ) );
		var inverseRoot = C.Value( C.Interpolate( C.Word( reduced ), C.RootEstimate( C.Word( reduced ) ) ) );
		var root = A.Multiply( inverseRoot, reduced );
		root = A.MultiplyAdd( A.NegativeMultiplySubtract( inverseRoot, root, A.Value( 0x3f800001 ) ),
			A.Multiply( root, .5f ), root );
		var leading = high ? A.Multiply( root, -2f ) : magnitude;
		var reciprocal = C.Value( C.Interpolate( C.Word( denominator ), C.ReciprocalEstimate( C.Word( denominator ) ) ) );
		var term = A.Multiply( A.Multiply( A.Multiply( leading, reduced ), numerator ), reciprocal );
		term = A.MultiplyAdd( A.NegativeMultiplySubtract( denominator, reciprocal, A.Value( 0x3f800001 ) ), term, term );
		var result = A.Add( leading, term );
		if ( high )
			result = A.Add( result, A.Value( 0x3fc90fda ) );
		return A.Greater( value, 0f ) ? result : C.Value( C.Word( result ) ^ 0x80000000u );
	}

	internal static (float Sine, float Cosine) SinCos( float angle )
	{
		var scaled = A.Multiply( angle, A.Value( 0x3f22f983 ) );
		var rounded = A.Add( scaled, A.Negative( scaled ) ? -.5f : .5f );
		var quadrant = rounded >= 2147483648f ? int.MaxValue : rounded < -2147483648f ? int.MinValue : (int)rounded;
		var converted = (float)quadrant;
		if ( Math.Abs( (double)converted ) > Math.Abs( (double)quadrant ) )
			converted = C.Value( C.Word( converted ) - 1 );
		var reduced = A.NegativeMultiplySubtract( converted, A.Value( 0x3fc90fd9 ), angle );
		reduced = A.NegativeMultiplySubtract( converted, A.Value( 0x33a22168 ), reduced );
		var squared = A.Multiply( reduced, reduced );
		var cosine = A.MultiplyAdd( squared, A.Value( 0xbab24992 ), A.Value( 0x3d2aa035 ) );
		cosine = A.MultiplyAdd( cosine, squared, A.Value( 0xbeffffdf ) );
		cosine = A.MultiplyAdd( cosine, squared, 1f );
		var sine = A.MultiplyAdd( squared, A.Value( 0xb94c8c6d ), A.Value( 0x3c088342 ) );
		sine = A.MultiplyAdd( sine, squared, A.Value( 0xbe2aaaa1 ) );
		sine = A.MultiplyAdd( sine, A.Multiply( squared, reduced ), reduced );
		float Negate( float number ) => C.Value( C.Word( number ) ^ 0x80000000u );
		return (quadrant & 3) switch
		{
			0 => (sine, cosine), 1 => (cosine, Negate( sine )),
			2 => (Negate( sine ), Negate( cosine )), _ => (Negate( cosine ), sine)
		};
	}
}
