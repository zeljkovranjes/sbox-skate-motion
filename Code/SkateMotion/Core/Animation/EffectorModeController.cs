namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Advances foot and hand modes before the rig's weight update.</summary>
internal static class EffectorModeController
{
	internal static void Update( EffectorWeightState[] states, bool[] pinning, bool[] releasing,
		float[] pinnedWeights, Vector3[] offsets, bool feetEnabled, bool leftHandEnabled, bool rightHandEnabled )
	{
		ArgumentNullException.ThrowIfNull( states );
		ArgumentNullException.ThrowIfNull( pinning );
		ArgumentNullException.ThrowIfNull( releasing );
		ArgumentNullException.ThrowIfNull( pinnedWeights );
		ArgumentNullException.ThrowIfNull( offsets );
		if ( states.Length != 4 || pinning.Length != 4 || releasing.Length != 4
			|| pinnedWeights.Length != 4 || offsets.Length != 4 )
			throw new ArgumentException( "The rig needs four effector slots." );

		for ( var i = 0; i < states.Length; i++ )
		{
			var enabled = i < 2 ? feetEnabled : i == 2 ? leftHandEnabled : rightHandEnabled;
			states[ i ] = Advance( states[ i ], pinning[ i ], releasing[ i ], pinnedWeights[ i ],
				enabled ? 1 : 0, ref offsets[ i ] );
		}
	}

	private static EffectorWeightState Advance( EffectorWeightState state, bool pinning, bool releasing,
		float pinnedWeight, int defaultMode, ref Vector3 offset )
	{
		switch ( state.Mode )
		{
			case 0:
			case 1:
				if ( state.Mode == 0 )
					state = state with { Target = 0f };
				if ( pinning )
					return state with { Mode = 2, Override = pinnedWeight };
				if ( releasing )
				{
					offset = Vector3.Zero;
					return state with { Mode = 3, Override = pinnedWeight };
				}
				if ( state.Mode == 0 || state.Target == 0f )
					return state with { Mode = defaultMode, Override = 0f };
				return state;
			case 2:
				return pinning ? state : state with { Mode = 3 };
			case 3:
				if ( pinning )
					state = state with { Mode = 2, Override = pinnedWeight };
				// Completion is checked after re-pinning, including a zero-weight pin.
				if ( !releasing && state.Override == 0f )
				{
					offset = Vector3.Zero;
					state = state with { Mode = defaultMode };
				}
				return state;
			default:
				return state;
		}
	}
}
