namespace SkateMotion.Core;

internal sealed class SkateMovement
{
	private double _phase;

	internal float PushAcceleration( ControlInput input, float forwardSpeed, SkateSettings settings, float step )
	{
		if ( input.Push <= 0f || input.Brake > 0f )
		{
			_phase = 0d;
			return 0f;
		}

		var end = _phase + step / settings.PushPeriod;
		// Integrate the stroke over the tick so the delivered push does not depend on sampling rate.
		var stroke = (Primitive( end ) - Primitive( _phase )) * settings.PushPeriod / step;
		_phase = end - Math.Floor( end );
		var remaining = Math.Clamp( 1f - Math.Max( forwardSpeed, 0f ) / settings.PushSpeed, 0f, 1f );
		return (float)stroke * input.Push * settings.PushAcceleration * remaining;
	}

	/// <summary>
	/// Push acceleration for the measured handling: each push adds a set amount of speed (more from a standstill,
	/// less near the push limit) while the pushing foot is on the ground, at a limited rate per second. Foot contact
	/// comes from the animation when it reports one; otherwise a stroke of <see cref="FallbackContact"/> seconds in
	/// every <see cref="SkateSettings.PushPeriod"/> stands in for it.
	/// </summary>
	internal float RecoveredAcceleration( ControlInput input, float forwardSpeed, SkateSettings settings, float step )
	{
		var pushing = input.Push > 0f && input.Brake <= 0f;
		bool contact;
		if ( input.PushContact is { } reported )
		{
			contact = pushing && reported;
			_phase = 0d;
		}
		else
		{
			contact = pushing && _phase < FallbackContact / settings.PushPeriod;
			_phase = pushing ? (_phase + step / settings.PushPeriod) % 1d : 0d;
		}
		if ( !contact )
		{
			_inContact = false;
			return 0f;
		}
		var speed = Math.Max( forwardSpeed, 0f );
		if ( !_inContact )
			_target = Vehicle.HandlingCurves.PushTarget( speed );
		_inContact = true;
		var change = Math.Clamp( _target - speed, 0f, Vehicle.HandlingCurves.PushRate( speed, settings.Difficulty ) * step );
		return change / step * input.Push;
	}

	/// <summary>Seconds of foot contact per push when no animation reports it.</summary>
	internal const float FallbackContact = 0.2f;

	private float _target;
	private bool _inContact;

	internal void Reset()
	{
		_phase = 0d;
		_inContact = false;
	}

	private static double Primitive( double phase )
	{
		var cycles = Math.Floor( phase );
		return cycles + (1d - Math.Cos( Math.PI * (phase - cycles) )) * 0.5d;
	}
}
