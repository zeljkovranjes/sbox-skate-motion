namespace SkateMotion.Core.Animation;

/// <summary>Four-point height correction with caller-specific ordered interval selection.</summary>
internal sealed class GrindHeightCorrectionCurve
{
	private readonly float[] _inputs;
	private readonly float[] _outputs;

	internal GrindHeightCorrectionCurve( float[] inputs, float[] outputs )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( outputs );
		if ( inputs.Length != 4 || outputs.Length != 4 )
			throw new ArgumentException( "Height correction curves require four inputs and outputs." );
		_inputs = [ .. inputs ];
		_outputs = [ .. outputs ];
	}

	internal float Evaluate( float value )
	{
		if ( _inputs[ 0 ] > value )
			return _outputs[ 0 ];
		if ( !(_inputs[ 3 ] > value) )
			return _outputs[ 3 ];
		for ( var i = 1; i < 4; i++ )
		{
			if ( !(value < _inputs[ i ]) )
				continue;
			var width = _inputs[ i ] - _inputs[ i - 1 ];
			if ( width <= 0f )
				return _outputs[ i ];
			var slope = (_outputs[ i ] - _outputs[ i - 1 ]) / width;
			return MathF.FusedMultiplyAdd( slope, value - _inputs[ i - 1 ], _outputs[ i - 1 ] );
		}
		return _outputs[ 0 ];
	}
}
