namespace SkateMotion.Core.Constraints;

internal static class ConstraintReciprocal
{
	internal static float Inverse( float value )
	{
		var estimate = 1f / value;
		return MathF.FusedMultiplyAdd( estimate, MathF.FusedMultiplyAdd( -estimate, value, 1f ), estimate );
	}

	internal static float InverseSquareRoot( float value )
	{
		var estimate = (float)(1d / Math.Sqrt( value ));
		for ( var i = 0; i < 2; ++i )
		{
			var error = MathF.FusedMultiplyAdd( -value, estimate * estimate, 1f );
			estimate = MathF.FusedMultiplyAdd( estimate * .5f, error, estimate );
		}
		return estimate;
	}
}
