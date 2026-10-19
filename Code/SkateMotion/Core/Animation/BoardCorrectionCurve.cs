namespace SkateMotion.Core.Animation;

/// <summary>An eight-point angular response with ordered inputs and scalar interpolation.</summary>
internal sealed class BoardCorrectionCurve
{
	private readonly float[] _inputs;
	private readonly float[] _outputs;

	internal BoardCorrectionCurve( float[] inputs, float[] outputs )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( outputs );
		if ( inputs.Length != 8 || outputs.Length != 8 )
			throw new ArgumentException( "Board correction curves require eight inputs and outputs." );
		_inputs = new float[ 8 ];
		_outputs = new float[ 8 ];
		for ( var i = 0; i < 8; i++ )
		{
			if ( !float.IsFinite( inputs[ i ] ) || !float.IsFinite( outputs[ i ] )
				|| (i > 0 && inputs[ i ] < inputs[ i - 1 ]) )
				throw new ArgumentException( "Curve inputs must be ordered and all points finite." );
			_inputs[ i ] = inputs[ i ];
			_outputs[ i ] = outputs[ i ];
		}
	}

	internal float Evaluate( float angle )
	{
		if ( _inputs[ 0 ] > angle )
			return _outputs[ 0 ];
		if ( _inputs[ 7 ] <= angle )
			return _outputs[ 7 ];
		for ( var i = 1; i < 8; i++ )
		{
			if ( !(angle < _inputs[ i ]) )
				continue;
			var width = _inputs[ i ] - _inputs[ i - 1 ];
			if ( width <= 0f )
				return _outputs[ i ];
			var slope = (_outputs[ i ] - _outputs[ i - 1 ]) / width;
			return LimbSolverMath.MultiplyAdd( slope, angle - _inputs[ i - 1 ], _outputs[ i - 1 ] );
		}
		return _outputs[ 0 ];
	}
}
