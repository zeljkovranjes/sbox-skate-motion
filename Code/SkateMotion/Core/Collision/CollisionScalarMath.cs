namespace SkateMotion.Core.Collision;

/// <summary>Preserves rounded scalar intermediates when the engine flushes float subnormals.</summary>
internal static class CollisionScalarMath
{
	private const double MinimumNormal = 1.1754943508222875e-38;
	private const double MinimumSubnormal = 1.401298464324817e-45;

	internal static double Widen( float value )
	{
		var bits = BitConverter.SingleToUInt32Bits( value );
		if ( (bits & 0x7f800000u) != 0 || (bits & 0x7fffffffu) == 0 )
			return value;
		var magnitude = (bits & 0x7fffffu) * MinimumSubnormal;
		return (bits & 0x80000000u) == 0 ? magnitude : -magnitude;
	}

	internal static double Round( double value )
	{
		if ( Math.Abs( value ) >= MinimumNormal || !double.IsFinite( value ) || value == 0d )
			return (float)value;
		return Math.Round( value / MinimumSubnormal ) * MinimumSubnormal;
	}

	internal static float Narrow( double value )
	{
		value = Round( value );
		if ( Math.Abs( value ) >= MinimumNormal || !double.IsFinite( value ) || value == 0d )
			return (float)value;
		var bits = (uint)(Math.Abs( value ) / MinimumSubnormal);
		if ( value < 0d )
			bits |= 0x80000000u;
		return BitConverter.UInt32BitsToSingle( bits );
	}

	internal static double Product( double a, double b ) => Round( a * b );
	internal static double Sum( double a, double b ) => Round( a + b );
	internal static double Difference( double a, double b ) => Round( a - b );
	internal static double MultiplyAdd( double a, double b, double c ) => Round( a * b + c );
	internal static double Divide( double a, double b ) => Round( a / b );
	internal static double SquareRoot( double value ) => Round( Math.Sqrt( value ) );
}
