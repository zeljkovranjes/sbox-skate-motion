namespace SkateMotion.Core.Input;

/// <summary>An eight- or sixteen-point directional input response with fused interpolation.</summary>
internal sealed class InputResponseCurve
{
	private readonly float[] _inputs;
	private readonly float[] _outputs;

	internal InputResponseCurve( float[] inputs, float[] outputs )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( outputs );
		if ( inputs.Length is not (8 or 16) || outputs.Length != inputs.Length )
			throw new ArgumentException( "Input responses require eight or sixteen paired points." );
		for ( var i = 0; i < inputs.Length; i++ )
		{
			if ( !float.IsFinite( inputs[ i ] ) || !float.IsFinite( outputs[ i ] )
				|| (i > 0 && inputs[ i ] < inputs[ i - 1 ]) )
				throw new ArgumentException( "Input response points must be finite and ordered." );
		}
		_inputs = [ .. inputs ];
		_outputs = [ .. outputs ];
	}

	internal float Evaluate( float input )
	{
		if ( _inputs[ 0 ] > input )
			return _outputs[ 0 ];
		if ( _inputs[ ^1 ] <= input )
			return _outputs[ ^1 ];
		for ( var i = 1; i < _inputs.Length; i++ )
		{
			if ( !(input < _inputs[ i ]) )
				continue;
			var width = _inputs[ i ] - _inputs[ i - 1 ];
			if ( width <= 0f )
				return _outputs[ i ];
			var slope = (_outputs[ i ] - _outputs[ i - 1 ]) / width;
			return MathF.FusedMultiplyAdd( slope, input - _inputs[ i - 1 ], _outputs[ i - 1 ] );
		}
		return _outputs[ 0 ];
	}
}
