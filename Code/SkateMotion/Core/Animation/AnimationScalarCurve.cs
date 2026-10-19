namespace SkateMotion.Core.Animation;

using Vector2 = System.Numerics.Vector2;

/// <summary>An ordered scalar curve with exact-key lookup and fused segment interpolation.</summary>
internal sealed class AnimationScalarCurve
{
	private readonly Vector2[] _keys;

	internal AnimationScalarCurve( Vector2[] keys )
	{
		ArgumentNullException.ThrowIfNull( keys );
		if ( keys.Length == 0 )
			throw new ArgumentException( "A scalar curve needs at least one key.", nameof( keys ) );
		_keys = new Vector2[ keys.Length ];
		for ( var index = 0; index < keys.Length; index++ )
		{
			var key = keys[ index ];
			if ( !float.IsFinite( key.X ) || !float.IsFinite( key.Y )
				|| (index > 0 && key.X < keys[ index - 1 ].X) )
				throw new ArgumentException( "Curve keys must be finite and ordered by time.", nameof( keys ) );
			_keys[ index ] = key;
		}
	}

	internal float Sample( float position )
	{
		if ( position <= _keys[ 0 ].X )
			return _keys[ 0 ].Y;
		if ( position >= _keys[ _keys.Length - 1 ].X )
			return _keys[ _keys.Length - 1 ].Y;

		var first = 0;
		var last = _keys.Length - 1;
		while ( first <= last )
		{
			var middle = (first + last) / 2;
			var key = _keys[ middle ];
			if ( position > key.X )
				first = middle + 1;
			else if ( position < key.X )
				last = middle - 1;
			else
				return key.Y;
		}

		var lower = _keys[ last ];
		var upper = _keys[ first ];
		var slope = (upper.Y - lower.Y) / (upper.X - lower.X);
		return MathF.FusedMultiplyAdd( slope, position - lower.X, lower.Y );
	}
}
