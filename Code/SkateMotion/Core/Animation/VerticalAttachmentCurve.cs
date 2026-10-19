namespace SkateMotion.Core.Animation;

/// <summary>A four-point response curve with ordered inputs and scalar fused interpolation.</summary>
internal sealed class VerticalAttachmentCurve
{
	private readonly float[] _inputs;
	private readonly float[] _outputs;

	internal VerticalAttachmentCurve( float[] inputs, float[] outputs )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( outputs );
		if ( inputs.Length != 4 || outputs.Length != 4 )
			throw new ArgumentException( "Vertical response curves require four input and output points." );
		_inputs = new float[ 4 ];
		_outputs = new float[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			if ( !float.IsFinite( inputs[ i ] ) || !float.IsFinite( outputs[ i ] )
				|| (i > 0 && inputs[ i ] < inputs[ i - 1 ]) )
				throw new ArgumentException( "Vertical response curve inputs must be ordered and all points finite." );
			_inputs[ i ] = inputs[ i ];
			_outputs[ i ] = outputs[ i ];
		}
	}

	internal float Evaluate( float input )
	{
		if ( input < _inputs[ 0 ] )
			return _outputs[ 0 ];
		if ( input >= _inputs[ 3 ] )
			return _outputs[ 3 ];
		for ( var i = 1; i < 4; i++ )
		{
			if ( input >= _inputs[ i ] )
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
