namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using C = SkateMotion.Core.Collision.CollisionArithmetic;

internal static class PhysicalCadenceArithmetic
{
	internal static float Value( uint value ) => C.Value( value );
	internal static float Add( float a, float b ) => C.Value( C.Add( C.Word( a ), C.Word( b ) ) );
	internal static float Subtract( float a, float b ) => C.Value( C.Subtract( C.Word( a ), C.Word( b ) ) );
	internal static float Multiply( float a, float b ) => C.Value( C.Multiply( C.Word( a ), C.Word( b ) ) );
	internal static float MultiplyAdd( float a, float b, float c ) => C.Value( C.MultiplyAdd( C.Word( a ), C.Word( b ), C.Word( c ) ) );
	internal static float NegativeMultiplySubtract( float a, float b, float c ) => C.Value( C.NegativeMultiplySubtract( C.Word( a ), C.Word( b ), C.Word( c ) ) );
	internal static bool Greater( float a, float b ) => C.Greater( C.Word( a ), C.Word( b ) );
	internal static bool Negative( float value ) => (C.Word( value ) & 0x80000000u) != 0;
	internal static float Abs( float value ) => C.Value( C.Word( value ) & 0x7fffffffu );

	internal static float Divide( float numerator, float denominator )
	{
		var seed = C.Value( C.Interpolate( C.Word( denominator ), C.ReciprocalEstimate( C.Word( denominator ) ) ) );
		var quotient = Multiply( numerator, seed );
		return MultiplyAdd( NegativeMultiplySubtract( quotient, denominator, numerator ), seed, quotient );
	}

	internal static float Root( float value )
	{
		var inverse = C.Value( C.Interpolate( C.Word( value ), C.RootEstimate( C.Word( value ) ) ) );
		for ( var index = 0; index < 2; index++ )
		{
			var squared = Multiply( inverse, inverse );
			var half = Multiply( inverse, .5f );
			inverse = MultiplyAdd( half, NegativeMultiplySubtract( value, squared, 1f ), inverse );
		}
		return Multiply( value, inverse );
	}

	internal static float Dot( Vector3 a, Vector3 b ) => Add( Add( Multiply( a.X, b.X ), Multiply( a.Y, b.Y ) ), Multiply( a.Z, b.Z ) );
	internal static float Length( Vector3 value ) => Root( Dot( value, value ) );
	internal static float TransformRow( Vector3 row, Vector3 value ) => MultiplyAdd( row.Z, value.Z, MultiplyAdd( row.Y, value.Y, Multiply( row.X, value.X ) ) );
	internal static Vector3 Subtract( Vector3 a, Vector3 b ) => new( Subtract( a.X, b.X ), Subtract( a.Y, b.Y ), Subtract( a.Z, b.Z ) );

	internal static Vector3 Reject( Vector3 value, Vector3 axis )
	{
		var projection = Dot( axis, value );
		return new( NegativeMultiplySubtract( axis.X, projection, value.X ),
			NegativeMultiplySubtract( axis.Y, projection, value.Y ), NegativeMultiplySubtract( axis.Z, projection, value.Z ) );
	}

	internal static float Floor( float value )
	{
		var basis = Greater( 0f, value ) ? 8388608f : -8388608f;
		var shifted = Subtract( value, basis );
		var whole = shifted == value ? value : Add( shifted, basis );
		return Subtract( whole, Greater( whole, value ) ? 1f : 0f );
	}

	internal static float Wrap( float value, float lower, float upper )
	{
		var count = Greater( lower, value ) ? Floor( Subtract( upper, value ) )
			: Greater( value, upper ) ? -Floor( Subtract( value, lower ) ) : 0f;
		var adjusted = Add( value, count );
		var clipped = Negative( adjusted ) && lower == 0f ? 0f
			: Negative( Subtract( lower, adjusted ) ) ? adjusted : lower;
		return Negative( Subtract( upper, clipped ) ) ? upper : clipped;
	}
}
