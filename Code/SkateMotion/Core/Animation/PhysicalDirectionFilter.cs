namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Advances the retained vector response in the caller's controller cadence.</summary>
internal static class PhysicalDirectionFilter
{
	internal static Vector3 Update( PhysicalDirectionFilterState state, Vector3 input )
	{
		var gains = state.Coefficients;
		var error = input - state.Value;
		state.SmoothedError = Scale( state.SmoothedError, 1f - gains.W ) + Scale( error, gains.W );
		state.SmoothedDelta = Scale( state.SmoothedDelta, 1f - gains.W )
			+ Scale( input - state.PreviousInput, gains.W );
		var value = state.Value + Scale( state.SmoothedError, gains.Y );
		value += Scale( error, gains.X );
		value += Scale( state.SmoothedDelta - state.PreviousDelta, gains.Z );
		state.PreviousDelta = value - state.Value;
		state.Value = value;
		state.PreviousInput = input;
		return value;
	}
}
