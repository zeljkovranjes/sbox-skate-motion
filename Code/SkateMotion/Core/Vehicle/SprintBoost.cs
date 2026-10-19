namespace SkateMotion.Core.Vehicle;

/// <summary>
/// Sprinting on foot: held while running, the top speed rises to <see cref="SprintSpeed"/>; let go, it stays there for
/// <see cref="Hold"/> and then eases back to the running speed over <see cref="Fade"/>, so the rider slows smoothly.
/// </summary>
internal sealed class SprintBoost
{
	private float _boost;
	private float _since = Hold;

	/// <summary>Sprinting top speed (m/s).</summary>
	internal const float SprintSpeed = 10f;

	/// <summary>Seconds the sprint lasts after it is let go, and seconds it then takes to ease back to a run.</summary>
	internal const float Hold = 0.3f;
	internal const float Fade = 3.4f;

	/// <summary>Top speed (m/s) this step, given the running speed and whether sprint is held while running.</summary>
	internal float TopSpeed( bool sprinting, float runSpeed, float step )
	{
		if ( sprinting )
		{
			_boost = 1f;
			_since = 0f;
		}
		else
		{
			_since += step;
			if ( _since > Hold )
				_boost = MathF.Max( 0f, _boost - step / Fade );
		}
		return runSpeed + (SprintSpeed - runSpeed) * _boost;
	}

	internal void Reset()
	{
		_boost = 0f;
		_since = Hold;
	}
}
