namespace SkateMotion.Core.Animation;

/// <summary>Eight-point height-to-angle comparison margins with scalar fused interpolation.</summary>
internal sealed class GrindEdgeSelectionCurve
{
	private readonly float[] _inputs;
	private readonly float[] _outputs;

	internal GrindEdgeSelectionCurve( float[] inputs, float[] outputs )
	{
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( outputs );
		if ( inputs.Length != 8 || outputs.Length != 8 )
			throw new ArgumentException( "Edge selection curves require eight inputs and outputs." );
		_inputs = [ .. inputs ];
		_outputs = [ .. outputs ];
	}

	internal float Evaluate( float value )
	{
		if ( _inputs[ 0 ] > value )
			return _outputs[ 0 ];
		if ( !(_inputs[ 7 ] > value) )
			return _outputs[ 7 ];
		return Interpolate( value );
	}

	/// <summary>Uses ordered endpoint comparisons for callers that continue searching on unordered inputs.</summary>
	internal float EvaluateOrdered( float value )
	{
		if ( _inputs[ 0 ] > value )
			return _outputs[ 0 ];
		if ( _inputs[ 7 ] <= value )
			return _outputs[ 7 ];
		return Interpolate( value );
	}

	private float Interpolate( float value )
	{
		for ( var i = 1; i < 8; i++ )
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
