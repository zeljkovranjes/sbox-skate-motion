namespace SkateMotion.Core.Collision;

// Word operations keep rounding and exceptional encodings independent of native float modes.
internal static class CollisionArithmetic
{
	private const uint Sign = 0x80000000u;
	private const uint Exponent = 0x7f800000u;
	private const uint Fraction = 0x007fffffu;

	internal static uint Add( uint a, uint b ) => AddOrSubtract( a, b, false );
	internal static uint Subtract( uint a, uint b ) => AddOrSubtract( a, b, true );

	internal static uint Multiply( uint a, uint b )
	{
		a = Flush( a );
		b = Flush( b );
		if ( Magnitude( a ) == 0 || Magnitude( b ) == 0 )
			return 0;
		if ( IsExtended( a ) || IsExtended( b ) )
			return ((a ^ b) & Sign) | 0x7fffffffu;
		var result = Product( a, b );
		return Magnitude( result ) == 0 ? 0 : result;
	}

	internal static uint MultiplyAdd( uint a, uint b, uint c ) => Add( ProductForAdd( a, b ), c );
	internal static uint NegativeMultiplySubtract( uint a, uint b, uint c ) => Subtract( c, ProductForAdd( a, b ) );

	internal static bool Greater( uint a, uint b )
	{
		if ( IsNanEncoding( a ) )
			a &= ~0x00800000u;
		if ( IsNanEncoding( b ) )
			b &= ~0x00800000u;
		a = Flush( a );
		b = Flush( b );
		if ( Magnitude( a ) == 0 && Magnitude( b ) == 0 )
			return false;
		if ( ((a ^ b) & Sign) != 0 )
			return (a & Sign) == 0;
		return (a & Sign) == 0 ? a > b : a < b;
	}

	internal static uint RootEstimate( uint value )
	{
		var exponent = (value >> 23) & 255;
		var resultExponent = exponent == 0 ? 255 : 190 - (exponent + 1) / 2;
		return (resultExponent << 23) | CollisionEstimateTables.RootFraction[ (value >> 18) & 63 ];
	}

	internal static uint ReciprocalEstimate( uint value ) => (value & Sign)
		| CollisionEstimateTables.ReciprocalFraction[ (value >> 18) & 31 ]
		| CollisionEstimateTables.ReciprocalExponent[ (value >> 23) & 255 ];

	internal static uint Interpolate( uint input, uint estimate )
	{
		var basis = (estimate & 0x007ffc00u) << 9;
		var product = (estimate & 0x3ffu) * (input & 0x7ffffu);
		var adjust = product > basis;
		var fraction = unchecked(basis - product) >> (adjust ? 8 : 9);
		return unchecked(((estimate & 0xff800000u) | (fraction & Fraction)) - (adjust ? 0x00800000u : 0u));
	}

	internal static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
	internal static float Value( uint word ) => BitConverter.UInt32BitsToSingle( word );

	private static uint ProductForAdd( uint a, uint b )
	{
		a = IsExtended( a ) ? 0 : Flush( a );
		b = IsExtended( b ) ? 0 : Flush( b );
		return Product( a, b );
	}

	private static uint Product( uint a, uint b )
	{
		var sign = (a ^ b) & Sign;
		if ( Magnitude( a ) == 0 || Magnitude( b ) == 0 )
			return sign;
		var significand = (ulong)((a & Fraction) | 0x00800000u) * ((b & Fraction) | 0x00800000u);
		var exponent = (int)((a >> 23) & 255) + (int)((b >> 23) & 255) - 300;
		return Encode( significand, exponent, sign );
	}

	private static uint AddOrSubtract( uint a, uint b, bool subtract )
	{
		a = Flush( a );
		b = Flush( b );
		if ( IsNanEncoding( a ) )
			return a | 0x00400000u;
		if ( IsNanEncoding( b ) )
			return b | 0x00400000u;
		if ( subtract )
			b ^= Sign;
		if ( IsExtended( a ) || IsExtended( b ) )
		{
			if ( IsExtended( a ) && IsExtended( b ) && ((a ^ b) & Sign) != 0 )
				return 0xffc00000u;
			return IsExtended( a ) ? a : b;
		}
		var magnitudeA = Magnitude( a );
		var magnitudeB = Magnitude( b );
		if ( magnitudeA == 0 && magnitudeB == 0 )
			return (a & b) & Sign;
		if ( magnitudeA == 0 )
			return b;
		if ( magnitudeB == 0 )
			return a;
		if ( magnitudeA < magnitudeB )
			(a, b) = (b, a);
		var exponentA = (int)((a >> 23) & 255);
		var exponentB = (int)((b >> 23) & 255);
		var large = (ulong)((a & Fraction) | 0x00800000u) << 32;
		var small = ShiftWithStickyBit( (ulong)((b & Fraction) | 0x00800000u) << 32, exponentA - exponentB );
		var sum = ((a ^ b) & Sign) == 0 ? large + small : large - small;
		return sum == 0 ? 0 : Encode( sum, exponentA - 182, a & Sign );
	}

	private static ulong ShiftWithStickyBit( ulong value, int count )
	{
		if ( count == 0 )
			return value;
		if ( count >= 64 )
			return value == 0 ? 0ul : 1ul;
		return (value >> count) | ((value << (64 - count)) != 0 ? 1ul : 0ul);
	}

	private static uint Encode( ulong significand, int exponent, uint sign )
	{
		var highestBit = System.Numerics.BitOperations.Log2( significand );
		var unbiased = exponent + highestBit;
		if ( unbiased < -126 )
			return sign;
		if ( unbiased > 127 )
			return sign | 0x7f7fffffu;
		var fraction = highestBit >= 23 ? significand >> (highestBit - 23) : significand << (23 - highestBit);
		return sign | ((uint)(unbiased + 127) << 23) | ((uint)fraction & Fraction);
	}

	private static uint Flush( uint value ) => (value & Exponent) == 0 ? value & Sign : value;
	private static uint Magnitude( uint value ) => value & ~Sign;
	private static bool IsExtended( uint value ) => (value & Exponent) == Exponent;
	private static bool IsNanEncoding( uint value ) => IsExtended( value ) && (value & Fraction) != 0;
}
