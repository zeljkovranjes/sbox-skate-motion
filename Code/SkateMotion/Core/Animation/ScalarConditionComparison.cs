namespace SkateMotion.Core.Animation;

internal readonly record struct ScalarConditionComparison(
	ConditionComparisonMode Mode, float Threshold, bool Absolute = false )
{
	internal bool Evaluate( float value )
	{
		if ( Absolute )
			value = MathF.Abs( value );

		// Negated comparisons deliberately accept unordered values.
		return Mode switch
		{
			ConditionComparisonMode.Equal => value == Threshold,
			ConditionComparisonMode.NotEqual => value != Threshold,
			ConditionComparisonMode.Greater => value > Threshold,
			ConditionComparisonMode.Less => value < Threshold,
			ConditionComparisonMode.NotLess => !( value < Threshold ),
			ConditionComparisonMode.NotGreater => !( value > Threshold ),
			ConditionComparisonMode.AbsoluteGreater => MathF.Abs( value ) > Threshold,
			_ => false
		};
	}
}
