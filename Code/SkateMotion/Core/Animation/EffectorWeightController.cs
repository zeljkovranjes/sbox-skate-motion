namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Updates two foot and two hand effector weights once per rig invocation.</summary>
internal sealed class EffectorWeightController
{
	private readonly float _normalRate;
	private readonly float _releaseRate;
	private readonly Vector3 _boxOffset;
	private readonly Vector3 _boxFade;

	internal EffectorWeightController( float normalRate, float releaseRate, Vector3 boxOffset, Vector3 boxFade )
	{
		if ( !float.IsFinite( normalRate ) || normalRate < 0f
			|| !float.IsFinite( releaseRate ) || releaseRate < 0f )
			throw new ArgumentException( "Weight rates must be finite and nonnegative." );
		_normalRate = normalRate;
		_releaseRate = releaseRate;
		_boxOffset = boxOffset;
		_boxFade = boxFade;
	}

	internal void Update( EffectorWeightState[] states, float[] pinnedWeights, bool[] releasing,
		Vector3[] handPositions, Vector3 anchor, bool disableFeet )
	{
		ArgumentNullException.ThrowIfNull( states );
		ArgumentNullException.ThrowIfNull( pinnedWeights );
		ArgumentNullException.ThrowIfNull( releasing );
		ArgumentNullException.ThrowIfNull( handPositions );
		if ( states.Length != 4 || pinnedWeights.Length != 4 || releasing.Length != 4 || handPositions.Length != 2 )
			throw new ArgumentException( "The rig needs four weight slots and two hand positions." );

		var inner = new Vector3( anchor.X, 0f, anchor.Z ) + _boxOffset;
		var outer = inner + _boxFade;
		for ( var i = 0; i < states.Length; i++ )
		{
			var state = states[ i ];
			switch ( state.Mode )
			{
				case 2:
					states[ i ] = state with { Target = FloorZero( state.Target - _normalRate ), Override = pinnedWeights[ i ] };
					break;
				case 3:
					states[ i ] = state with
					{
						Target = FloorZero( state.Target - _normalRate ),
						Override = releasing[ i ] ? 1f : FloorZero( state.Override - _releaseRate )
					};
					break;
				case 1:
					var target = i < 2
						? Approach( state.Target, disableFeet ? 0f : 1f )
						: HandWeight( state.Target, handPositions[ i - 2 ], inner, outer );
					states[ i ] = state with { Target = target };
					break;
			}
		}
	}

	private float HandWeight( float current, Vector3 position, Vector3 inner, Vector3 outer )
	{
		position = Vector3.Abs( position );
		if ( !(current > 0f) && !(outer.X > position.X && outer.Y > position.Y && outer.Z > position.Z) )
			return current;

		var target = 1f;
		if ( inner.X < position.X )
			target = AxisFade( position.X, inner.X, _boxFade.X );
		if ( inner.Z < position.Z )
		{
			var candidate = AxisFade( position.Z, inner.Z, _boxFade.Z );
			target = Select( target - candidate, candidate, target );
		}
		if ( inner.Y < position.Y )
		{
			var candidate = AxisFade( position.Y, inner.Y, _boxFade.Y );
			target = Select( candidate - target, target, candidate );
		}
		target = FloorZero( target );
		target = Select( 1f - target, target, 1f );
		return Approach( current, target );
	}

	private float Approach( float current, float target )
	{
		var difference = target - current;
		var step = Select( -_normalRate - difference, -_normalRate, difference );
		step = Select( _normalRate - step, step, _normalRate );
		return current + step;
	}

	private static float AxisFade( float position, float inner, float width ) => (inner - position) / width + 1f;

	private static float FloorZero( float value ) => Select( -value, 0f, value );

	// Explicit selection preserves equality, signed-zero and unordered comparison behavior.
	private static float Select( float condition, float positive, float negative ) => condition >= 0f ? positive : negative;
}
