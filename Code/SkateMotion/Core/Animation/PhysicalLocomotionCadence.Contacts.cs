namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using A = PhysicalCadenceArithmetic;

internal static partial class PhysicalLocomotionCadence
{
	/// <summary>Updates physical gait with an actual contact observation when correction is requested.</summary>
	internal static bool TryUpdateWithContacts( ref PhysicalLocomotionCadenceState state,
		in PhysicalLocomotionCadenceInput input, Vector4 thresholds, in PhysicalLocomotionCadenceContactInput contacts )
		=> TryUpdateCore( ref state, input, thresholds, contacts );

	private static PhysicalLocomotionCadenceState CorrectContact( PhysicalLocomotionCadenceState state,
		uint flags, float magnitude, in PhysicalLocomotionCadenceContactInput contacts )
	{
		var distance = A.Length( A.Subtract( contacts.ContactPoint, contacts.FramePosition ) );
		var time = A.Divide( A.Subtract( distance, A.Value( 0x3e4ccccc ) ), magnitude );
		if ( !A.Greater( time, A.Value( 0x3c088888 ) ) )
			return state;
		var upper = A.Multiply( state.Rate, 2.5f );
		if ( (flags & 0x100u) != 0 )
		{
			var target = A.Greater( state.Phase, A.Value( 0x3ebd70a3 ) ) && !A.Greater( state.Phase, A.Value( 0x3f5eb851 ) )
				? A.Value( 0x3f666666 ) : A.Value( 0x3ecccccc );
			var delta = A.Wrap( A.Subtract( target, state.Phase ), 0f, 1f );
			return state with { Rate = ClampRate( A.Divide( delta, time ), 0f, upper ) };
		}
		var lower = A.Multiply( state.Rate, A.Value( 0x3ecccccc ) );
		var first = A.Divide( A.Wrap( A.Subtract( A.Value( 0x3ee66666 ), state.Phase ), 0f, 1f ), time );
		var second = A.Divide( A.Wrap( A.Subtract( A.Value( 0x3f733333 ), state.Phase ), 0f, 1f ), time );
		var preferFirst = A.Greater( A.Abs( A.Subtract( state.Rate, second ) ), A.Abs( A.Subtract( state.Rate, first ) ) );
		return state with { Rate = SelectContactRate( first, second, lower, upper, preferFirst ? first : second ) };
	}

	private static float SelectContactRate( float first, float second, float lower, float upper, float preferred )
	{
		var firstBelow = A.Greater( lower, first );
		var secondBelow = A.Greater( lower, second );
		var firstValid = !firstBelow && !A.Greater( first, upper );
		var secondValid = !secondBelow && !A.Greater( second, upper );
		if ( firstValid )
			return secondValid ? preferred : first;
		if ( secondValid )
			return second;
		var firstDistance = firstBelow ? A.Subtract( lower, first ) : A.Subtract( first, upper );
		var secondDistance = secondBelow ? A.Subtract( lower, second ) : A.Subtract( second, upper );
		return ClampRate( A.Greater( secondDistance, firstDistance ) ? first : second, lower, upper );
	}

	private static float ClampRate( float value, float lower, float upper )
	{
		var aboveLower = A.Negative( A.Subtract( lower, value ) ) ? value : lower;
		return A.Negative( A.Subtract( upper, aboveLower ) ) ? upper : aboveLower;
	}
}
