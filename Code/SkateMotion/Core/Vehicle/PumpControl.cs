namespace SkateMotion.Core.Vehicle;

/// <summary>
/// Measured pumping. Holding pump crouches the rider; letting go stands them back up, and while the centre of mass rises
/// on ground that curves up under the board (a transition) the board gains speed: the faster the ground turns, the
/// faster the rise and the slower the board, the more. Ground curving away and flat ground give nothing, and neither
/// does crouching. A rise keeps its full effect for <see cref="FullEffect"/> and fades out by a second. The rider also
/// holds a lower crouch on steeper ground, so dropping into a transition rises a little by itself: that pumps too, at
/// a share set by the difficulty.
/// </summary>
internal sealed class PumpControl
{
	private float _rise;
	private float _time;
	private float _turn;
	private bool _held;

	/// <summary>Height (m) of the rider's centre of mass above the deck: standing, or crouched while pump is held.</summary>
	internal float Height { get; private set; } = Standing;

	/// <summary>How far down (0 standing, 1 fully crouched) the rider is.</summary>
	internal float Crouch => (Standing - Height) / (Standing - Crouched);

	/// <summary>Centre of mass height (m) above the deck standing and crouched; how quickly (1/s) the rider moves between them.</summary>
	internal const float Standing = 0.9649f;
	internal const float Crouched = 0.5726f;
	internal const float Ease = 8f;

	// Slowest rise (m/s) that pumps and the fastest that counts, how quickly (1/s) the measured rise follows the
	// body, speed gained per unit of pumping per metre risen, and the strongest acceleration (m/s^2) a pump gives.
	private const float MinimumRise = 0.24f;
	private const float MaximumRise = 0.36f;
	private const float RiseSmoothing = 6.3f;
	private const float TurnSmoothing = 13.4f;
	private const float EffectFactor = 18f;
	internal const float MaximumAcceleration = 10f;

	// How close (m) to standing height counts as stood back up.
	private const float StoodUp = 0.01f;

	/// <summary>Seconds a rise keeps its full effect.</summary>
	internal const float FullEffect = 0.4f;

	/// <summary>
	/// Acceleration (m/s^2, never negative) along the travel this step. <paramref name="groundTurn"/> is how fast
	/// (rad/s) the ground's normal turns under the board, positive where it curves up ahead.
	/// </summary>
	internal float Acceleration( bool pump, float speed, float groundTurn, float step, float groundTilt = 0f,
		float unheld = 0f )
	{
		var previous = Height;
		var crouch = pump ? 1f : HandlingCurves.CrouchOnSlope( groundTilt );
		Height += (Standing - crouch * (Standing - Crouched) - Height) * (1f - MathF.Exp( -Ease * step ));
		_turn += (groundTurn - _turn) * (1f - MathF.Exp( -TurnSmoothing * step ));
		var rise = MathF.Max( 0f, (Height - previous) / step );
		_rise += (rise - _rise) * (1f - MathF.Exp( -RiseSmoothing * step ));
		var effect = _rise < MinimumRise ? 0f : MathF.Min( _rise, MaximumRise );
		_time = effect > 0f ? _time + step : 0f;
		// Standing up from a held crouch is a pump on purpose until the rider is standing again.
		_held = pump || (_held && Height < Standing - StoodUp);
		if ( effect <= 0f || !float.IsFinite( _turn ) || !float.IsFinite( speed ) )
			return 0f;
		var pumping = _turn * HandlingCurves.PumpAtSpeed( speed ) * (pump || _held ? 1f : unheld);
		var acceleration = pumping * EffectFactor * effect * HandlingCurves.PumpOverTime( _time );
		return Math.Clamp( acceleration, 0f, MaximumAcceleration );
	}

	internal void Reset()
	{
		Height = Standing;
		_rise = 0f;
		_time = 0f;
		_turn = 0f;
		_held = false;
	}
}
