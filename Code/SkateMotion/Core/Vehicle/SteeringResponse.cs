namespace SkateMotion.Core.Vehicle;

/// <summary>
/// Truck angle for the measured handling: the steering input is smoothed, shaped by the stick and speed response
/// curves, eased down while pushing (a pushing rider steers less) and reduced in a manual. The wheels' grip still
/// caps the angle as a safety limit (<see cref="SkateSettings.CarveAcceleration"/>).
/// </summary>
internal sealed class SteeringResponse
{
	private float _steer;
	private float _pushScale = 1f;

	// Per sixtieth of a second: share of the new input taken, and how fast pushing eases steering down and back.
	private const float InputShare = 0.7f;
	private const float PushDrop = 0.015f;
	private const float PushRecover = 0.05f;
	private const float PushFloor = 0.5f;

	/// <summary>Share of steering kept while balancing a manual.</summary>
	internal const float ManualShare = 0.6f;

	internal float Angle( SkateSettings settings, float steer, float speed, bool pushing, bool manual, float step )
	{
		var ticks = step * 60f;
		_steer += (steer - _steer) * (1f - MathF.Pow( 1f - InputShare, ticks ));
		_pushScale = pushing ? MathF.Max( PushFloor, _pushScale - PushDrop * ticks ) : MathF.Min( 1f, _pushScale + PushRecover * ticks );
		var angle = settings.TruckAngle * HandlingCurves.SteerResponse( _steer ) * HandlingCurves.SteerAtSpeed( speed )
			* _pushScale * (manual ? ManualShare : 1f);
		var squared = speed * speed;
		var grip = squared > 0.0001f ? MathF.Atan( settings.Wheelbase * settings.CarveAcceleration / (2f * squared) ) : MathF.Abs( angle );
		return Math.Clamp( angle, -grip, grip );
	}

	internal void Reset()
	{
		_steer = 0f;
		_pushScale = 1f;
	}
}
