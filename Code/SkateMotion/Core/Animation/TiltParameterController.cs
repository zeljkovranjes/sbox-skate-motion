namespace SkateMotion.Core.Animation;

/// <summary>Updates a signed tilt parameter with authored spin attenuation and per-step speed/acceleration limits.</summary>
internal sealed class TiltParameterController
{
	private readonly float[] _spin;
	private readonly float[] _gain;
	private readonly float _groundSpeed;
	private readonly float _groundAcceleration;
	private readonly float _airSpeed;
	private readonly float _airAcceleration;
	private float _value;
	private float _velocity;
	private bool _enabled;

	internal TiltParameterController( float[] spin, float[] gain,
		float groundSpeed, float groundAcceleration, float airSpeed, float airAcceleration )
	{
		ArgumentNullException.ThrowIfNull( spin );
		ArgumentNullException.ThrowIfNull( gain );
		if ( spin.Length != 4 || gain.Length != 4 )
			throw new ArgumentException( "Tilt attenuation requires four input and output points." );
		for ( var i = 0; i < spin.Length; i++ )
		{
			if ( !float.IsFinite( spin[ i ] ) || !float.IsFinite( gain[ i ] )
				|| (i > 0 && spin[ i ] < spin[ i - 1 ]) )
				throw new ArgumentException( "Tilt curve inputs must be ordered and all points finite." );
		}
		ValidateLimit( groundSpeed );
		ValidateLimit( groundAcceleration );
		ValidateLimit( airSpeed );
		ValidateLimit( airAcceleration );
		_spin = spin.AsSpan().ToArray();
		_gain = gain.AsSpan().ToArray();
		_groundSpeed = groundSpeed;
		_groundAcceleration = groundAcceleration;
		_airSpeed = airSpeed;
		_airAcceleration = airAcceleration;
	}

	/// <summary>Advances once at the caller's controller cadence. Disabled steps publish no parameter.</summary>
	internal float? Step( bool enabled, bool mirrored, float lateralTilt, float spinSpeed, int category )
	{
		if ( enabled && !_enabled )
		{
			_value = 0f;
			_velocity = 0f;
		}
		_enabled = enabled;
		if ( !enabled )
			return null;

		var gain = Attenuation( MathF.Abs( spinSpeed ) );
		var tilt = mirrored ? lateralTilt : -lateralTilt;
		var speed = category == 2 ? _airSpeed : _groundSpeed;
		var acceleration = category == 2 ? _airAcceleration : _groundAcceleration;
		var difference = MathF.FusedMultiplyAdd( gain, tilt, -_value );
		var desired = Bound( difference, -speed, speed );
		_velocity += Bound( desired - _velocity, -acceleration, acceleration );
		_value += _velocity;
		return _value;
	}

	private float Attenuation( float spin )
	{
		if ( spin < _spin[ 0 ] )
			return _gain[ 0 ];
		if ( spin >= _spin[ 3 ] )
			return _gain[ 3 ];
		for ( var i = 1; i < _spin.Length; i++ )
		{
			if ( spin >= _spin[ i ] )
				continue;
			var width = _spin[ i ] - _spin[ i - 1 ];
			if ( width <= 0f )
				return _gain[ i ];
			var slope = (_gain[ i ] - _gain[ i - 1 ]) / width;
			return MathF.FusedMultiplyAdd( slope, spin - _spin[ i - 1 ], _gain[ i - 1 ] );
		}
		return _gain[ 0 ];
	}

	private static float Bound( float value, float minimum, float maximum )
	{
		var lower = minimum - value >= 0f ? minimum : value;
		return maximum - lower >= 0f ? lower : maximum;
	}

	private static void ValidateLimit( float value )
	{
		if ( !float.IsFinite( value ) || value < 0f )
			throw new ArgumentOutOfRangeException( nameof( value ), "Tilt limits must be finite and nonnegative." );
	}
}
