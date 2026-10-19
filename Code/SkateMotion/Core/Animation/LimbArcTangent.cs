namespace SkateMotion.Core.Animation;

using static LimbSolverMath;

/// <summary>Evaluates a supplied eight-term arc-tangent profile with quadrant reduction.</summary>
internal sealed class LimbArcTangent
{
	private readonly float[] _coefficients;
	private readonly float _halfPi;
	private readonly float _pi;

	internal LimbArcTangent( float[] coefficients, float halfPi, float pi )
	{
		ArgumentNullException.ThrowIfNull( coefficients );
		if ( coefficients.Length != 8 )
			throw new ArgumentException( "Expected eight arc-tangent coefficients." );
		foreach ( var coefficient in coefficients )
		{
			if ( !float.IsFinite( coefficient ) )
				throw new ArgumentException( "Arc-tangent coefficients must be finite." );
		}
		if ( !float.IsFinite( halfPi ) || !float.IsFinite( pi ) || halfPi <= 0f || pi <= 0f )
			throw new ArgumentException( "Quadrant angles must be finite and positive." );
		_coefficients = coefficients.AsSpan().ToArray();
		_halfPi = halfPi;
		_pi = pi;
	}

	internal float Evaluate( float y, float x )
	{
		var estimate = 1f / x;
		estimate = MultiplyAdd( estimate, SubtractProduct( estimate, x, 1f ), estimate );
		var ratio = MultiplyAdd( y, estimate, 0f );
		var inverse = 1f / ratio;
		var improved = MultiplyAdd( SubtractProduct( ratio, inverse, 1f ), inverse, inverse );
		if ( !float.IsNaN( improved ) )
			inverse = improved;
		var large = MathF.Abs( ratio ) > 1f;
		var reduced = large ? -inverse : ratio;
		var leading = MathF.CopySign( large ? _halfPi : 0f, ratio ) + reduced;
		var squared = MultiplyAdd( reduced, reduced, 0f );
		var high = MultiplyAdd( squared, _coefficients[ 0 ], _coefficients[ 1 ] );
		var low = MultiplyAdd( squared, _coefficients[ 5 ], _coefficients[ 6 ] );
		var fourth = MultiplyAdd( squared, squared, 0f );
		var third = MultiplyAdd( squared, reduced, 0f );
		high = MultiplyAdd( high, squared, _coefficients[ 2 ] );
		low = MultiplyAdd( low, squared, _coefficients[ 7 ] );
		var eighth = MultiplyAdd( fourth, fourth, 0f );
		high = MultiplyAdd( high, squared, _coefficients[ 3 ] );
		low = MultiplyAdd( low, third, leading );
		var ninth = MultiplyAdd( eighth, reduced, -0f );
		high = MultiplyAdd( high, squared, _coefficients[ 4 ] );
		var result = MultiplyAdd( high, ninth, low );
		if ( x < 0f )
			result += MathF.CopySign( _pi, y );
		return x == 0f ? MathF.CopySign( _halfPi, y ) : result;
	}
}
