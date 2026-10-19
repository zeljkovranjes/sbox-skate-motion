namespace SkateMotion.Core.Vehicle;

/// <summary>A response curve through sample points, linear between them and held flat beyond the ends.</summary>
internal readonly struct LinearCurve
{
	private readonly float[] _x;
	private readonly float[] _y;

	internal LinearCurve( float[] x, float[] y )
	{
		if ( x is null || y is null || x.Length == 0 || x.Length != y.Length )
			throw new ArgumentException( "A curve needs matching, non-empty sample lists." );
		for ( var i = 1; i < x.Length; i++ )
		{
			if ( !(x[ i ] > x[ i - 1 ]) )
				throw new ArgumentException( "Curve samples must rise strictly." );
		}
		_x = x;
		_y = y;
	}

	internal float Evaluate( float value )
	{
		if ( !float.IsFinite( value ) || value <= _x[ 0 ] )
			return _y[ 0 ];
		var last = _x.Length - 1;
		if ( value >= _x[ last ] )
			return _y[ last ];
		var i = 1;
		while ( _x[ i ] < value )
			i++;
		var t = (value - _x[ i - 1 ]) / (_x[ i ] - _x[ i - 1 ]);
		return _y[ i - 1 ] + (_y[ i ] - _y[ i - 1 ]) * t;
	}
}
