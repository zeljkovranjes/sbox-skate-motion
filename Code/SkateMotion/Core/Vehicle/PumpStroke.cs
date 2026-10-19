namespace SkateMotion.Core.Vehicle;

// Estimated rider pump: extending the legs while the board follows a curved surface does work
// against the centripetal load, P = m v^2 k * extensionSpeed. On flat ground k = 0 and the rider
// gains nothing. Stroke length and timing are estimates.
internal sealed class PumpStroke
{
	/// <summary>Leg extension per stroke, in metres.</summary>
	internal const float Extension = 0.25f;

	/// <summary>Time to extend fully, in seconds.</summary>
	internal const float Duration = 0.3f;

	private float _extended;
	private bool _released = true;

	/// <summary>True while the rider is mid-stroke.</summary>
	internal bool Active => _extended > 0f && _extended < Duration;

	/// <summary>Tangential acceleration from pumping this step; curvature is in 1/m, speed in m/s.</summary>
	internal float Acceleration( bool pump, float speed, float curvature, float step )
	{
		if ( !pump )
		{
			_released = true;
			_extended = 0f;
			return 0f;
		}
		if ( _released )
		{
			_released = false;
			_extended = 0f;
		}
		if ( _extended >= Duration || !float.IsFinite( curvature ) || !float.IsFinite( speed ) )
			return 0f;
		_extended += step;
		// a = P / (m v) = v k * extensionSpeed, sign follows the travel direction.
		return speed * Math.Max( curvature, 0f ) * (Extension / Duration);
	}

	internal void Reset()
	{
		_extended = 0f;
		_released = true;
	}
}
