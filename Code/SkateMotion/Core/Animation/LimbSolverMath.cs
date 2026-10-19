namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Evaluates caller-supplied limb polynomials with explicit single-rounding operations.</summary>
internal sealed class LimbSolverMath
{
	private readonly float[] _cosine;
	private readonly float[] _sine;
	private readonly float[] _arcCosine;
	private readonly float _quadrantScale;
	private readonly float _quadrantHigh;
	private readonly float _quadrantLow;
	internal float Pi { get; }

	/// <summary>Polynomial coefficients are highest degree first: three cosine, three sine and eight arc-cosine terms.</summary>
	internal LimbSolverMath( float[] cosine, float[] sine, float[] arcCosine,
		float quadrantScale, float quadrantHigh, float quadrantLow, float pi )
	{
		_cosine = CopyCoefficients( cosine, 3 );
		_sine = CopyCoefficients( sine, 3 );
		_arcCosine = CopyCoefficients( arcCosine, 8 );
		if ( !float.IsFinite( quadrantScale ) || !float.IsFinite( quadrantHigh )
			|| !float.IsFinite( quadrantLow ) || !float.IsFinite( pi )
			|| quadrantScale <= 0f || quadrantHigh <= 0f || pi <= 0f )
			throw new ArgumentException( "Angular reduction parameters must be finite and use positive scales." );
		_quadrantScale = quadrantScale;
		_quadrantHigh = quadrantHigh;
		_quadrantLow = quadrantLow;
		Pi = pi;
	}

	internal float ArcCosine( float value )
	{
		var magnitude = MathF.Abs( value );
		var high = MultiplyAdd( magnitude, _arcCosine[ 0 ], _arcCosine[ 1 ] );
		high = MultiplyAdd( high, magnitude, _arcCosine[ 2 ] );
		high = MultiplyAdd( high, magnitude, _arcCosine[ 3 ] );
		var low = MultiplyAdd( magnitude, _arcCosine[ 4 ], _arcCosine[ 5 ] );
		low = MultiplyAdd( low, magnitude, _arcCosine[ 6 ] );
		low = MultiplyAdd( low, magnitude, _arcCosine[ 7 ] );
		var fourth = MultiplyAdd( magnitude, magnitude, 0f );
		fourth = MultiplyAdd( fourth, fourth, 0f );
		var polynomial = MultiplyAdd( high, fourth, low );
		var remainder = 1f - magnitude;
		var estimate = 1f / MathF.Sqrt( remainder );
		var root = MultiplyAdd( estimate, remainder, 0f );
		var correction = SubtractProduct( estimate, root, 1f );
		root = MultiplyAdd( correction, MultiplyAdd( root, .5f, 0f ), root );
		if ( remainder == 0f || estimate == 0f )
			root = remainder;
		return value < 0f ? SubtractProduct( root, polynomial, Pi ) : MultiplyAdd( root, polynomial, 0f );
	}

	internal (float Sine, float Cosine) SinCos( float angle )
	{
		var scaled = MultiplyAdd( angle, _quadrantScale, 0f );
		var rounded = scaled + MathF.CopySign( .5f, scaled );
		if ( !float.IsFinite( rounded ) )
			return (float.NaN, float.NaN);
		var quadrant = (int)Math.Clamp( (double)MathF.Truncate( rounded ), int.MinValue, int.MaxValue );
		var reduced = SubtractProduct( quadrant, _quadrantHigh, angle );
		reduced = SubtractProduct( quadrant, _quadrantLow, reduced );
		var squared = MultiplyAdd( reduced, reduced, 0f );
		var cosine = MultiplyAdd( squared, _cosine[ 0 ], _cosine[ 1 ] );
		cosine = MultiplyAdd( cosine, squared, _cosine[ 2 ] );
		cosine = MultiplyAdd( cosine, squared, 1f );
		var sine = MultiplyAdd( squared, _sine[ 0 ], _sine[ 1 ] );
		sine = MultiplyAdd( sine, squared, _sine[ 2 ] );
		sine = MultiplyAdd( sine, MultiplyAdd( squared, reduced, 0f ), reduced );
		return (quadrant & 3) switch
		{
			0 => (sine, cosine),
			1 => (cosine, -sine),
			2 => (-sine, -cosine),
			_ => (-cosine, sine)
		};
	}

	internal LimbFrame AxisRotation( Vector3 axis, float angle )
	{
		var (sine, cosine) = SinCos( angle );
		var sx = MultiplyAdd( sine, axis.X, 0f );
		var sy = MultiplyAdd( sine, axis.Y, 0f );
		var sz = MultiplyAdd( sine, axis.Z, 0f );
		var tx = MultiplyAdd( 1f - cosine, axis.X, 0f );
		var ty = MultiplyAdd( 1f - cosine, axis.Y, 0f );
		var tz = MultiplyAdd( 1f - cosine, axis.Z, 0f );
		return new LimbFrame(
			new Vector3( MultiplyAdd( tx, axis.X, cosine ), MultiplyAdd( tx, axis.Y, sz ), MultiplyAdd( tx, axis.Z, 0f ) - sy ),
			new Vector3( MultiplyAdd( ty, axis.X, 0f ) - sz, MultiplyAdd( ty, axis.Y, cosine ), MultiplyAdd( ty, axis.Z, sx ) ),
			new Vector3( MultiplyAdd( tz, axis.X, sy ), MultiplyAdd( tz, axis.Y, 0f ) - sx, MultiplyAdd( tz, axis.Z, cosine ) ) );
	}

	internal static float MultiplyAdd( float a, float b, float c ) => MathF.FusedMultiplyAdd( a, b, c );

	internal static float SubtractProduct( float a, float b, float c ) => -MultiplyAdd( a, b, -c );

	internal static float Dot( Vector3 a, Vector3 b )
	{
		var x = MultiplyAdd( a.X, b.X, 0f );
		var y = MultiplyAdd( a.Y, b.Y, 0f );
		var z = MultiplyAdd( a.Z, b.Z, 0f );
		return (x + y) + z;
	}

	internal static Vector3 Cross( Vector3 a, Vector3 b ) => new(
		SubtractProduct( b.Y, a.Z, MultiplyAdd( a.Y, b.Z, 0f ) ),
		SubtractProduct( b.Z, a.X, MultiplyAdd( a.Z, b.X, 0f ) ),
		SubtractProduct( b.X, a.Y, MultiplyAdd( a.X, b.Y, 0f ) ) );

	internal static Vector3 Scale( Vector3 value, float scale ) => AddScaled( value, scale, Vector3.Zero );

	internal static Vector3 AddScaled( Vector3 value, float scale, Vector3 offset ) => new(
		MultiplyAdd( value.X, scale, offset.X ),
		MultiplyAdd( value.Y, scale, offset.Y ),
		MultiplyAdd( value.Z, scale, offset.Z ) );

	internal static float Reciprocal( float value )
	{
		var estimate = 1f / value;
		for ( var i = 0; i < 2; i++ )
			estimate = MultiplyAdd( estimate, SubtractProduct( estimate, value, 1f ), estimate );
		return estimate;
	}

	internal static float InverseSquareRoot( float value )
	{
		var estimate = 1f / MathF.Sqrt( value );
		for ( var i = 0; i < 2; i++ )
		{
			var squared = MultiplyAdd( estimate, estimate, 0f );
			var half = MultiplyAdd( estimate, .5f, 0f );
			estimate = MultiplyAdd( half, SubtractProduct( value, squared, 1f ), estimate );
		}
		return estimate;
	}

	internal static float SquareRoot( float value )
	{
		var result = MultiplyAdd( value, InverseSquareRoot( value ), 0f );
		return value == 0f ? 0f : result;
	}

	internal static Vector3 Normalize( Vector3 value ) => Scale( value, InverseSquareRoot( Dot( value, value ) ) );

	internal static float ClampCosine( float value ) => Math.Clamp( value, 0f, 1f );

	private static float[] CopyCoefficients( float[] values, int count )
	{
		ArgumentNullException.ThrowIfNull( values );
		if ( values.Length != count )
			throw new ArgumentException( $"Expected {count} polynomial coefficients." );
		foreach ( var value in values )
		{
			if ( !float.IsFinite( value ) )
				throw new ArgumentException( "Polynomial coefficients must be finite." );
		}
		return values.AsSpan().ToArray();
	}
}
