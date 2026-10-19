namespace SkateMotion.Core.Animation;

using static LimbSolverMath;

/// <summary>Evaluates a caller-supplied rational arc-sine approximation.</summary>
internal sealed class LimbArcSine
{
	private readonly float _numeratorSlope;
	private readonly float _numeratorConstant;
	private readonly float _denominatorLinear;
	private readonly float _denominatorConstant;
	private readonly float _halfPi;

	internal LimbArcSine( float numeratorSlope, float numeratorConstant,
		float denominatorLinear, float denominatorConstant, float halfPi )
	{
		if ( !float.IsFinite( numeratorSlope ) || !float.IsFinite( numeratorConstant )
			|| !float.IsFinite( denominatorLinear ) || !float.IsFinite( denominatorConstant )
			|| !float.IsFinite( halfPi ) || halfPi <= 0f )
			throw new ArgumentException( "Arc-sine coefficients must be finite and half-pi must be positive." );
		_numeratorSlope = numeratorSlope;
		_numeratorConstant = numeratorConstant;
		_denominatorLinear = denominatorLinear;
		_denominatorConstant = denominatorConstant;
		_halfPi = halfPi;
	}

	internal float Evaluate( float value )
	{
		var magnitude = MathF.Abs( value );
		var high = magnitude > .5f;
		var reduced = high ? MultiplyAdd( magnitude, -.5f, .5f ) : MultiplyAdd( magnitude, magnitude, 0f );
		var numerator = MultiplyAdd( reduced, _numeratorSlope, _numeratorConstant );
		var denominator = MultiplyAdd( reduced + _denominatorLinear, reduced, _denominatorConstant );
		var estimate = 1f / MathF.Sqrt( reduced );
		var root = MultiplyAdd( estimate, reduced, 0f );
		var correction = SubtractProduct( estimate, root, 1f );
		root = MultiplyAdd( correction, MultiplyAdd( root, .5f, 0f ), root );
		if ( reduced == 0f || estimate == 0f )
			root = reduced;
		var leading = high ? MultiplyAdd( root, -2f, 0f ) : magnitude;
		estimate = 1f / denominator;
		correction = SubtractProduct( denominator, estimate, 1f );
		var term = MultiplyAdd( leading, reduced, 0f );
		term = MultiplyAdd( term, numerator, 0f );
		term = MultiplyAdd( term, estimate, -0f );
		var improved = MultiplyAdd( correction, term, term );
		if ( !float.IsNaN( improved ) )
			term = improved;
		var result = leading + term;
		if ( high )
			result += _halfPi;
		return value > 0f ? result : -result;
	}
}
