namespace SkateMotion.Core.Camera;

/// <summary>
/// The height the camera follows. On the ground it is the subject's own. In the air it stays at the take-off
/// height, so an ollie does not bob the view, follows the board down at once (drops), and rises only a little for
/// big airs, so a ramp air stays in frame. Landing at another height blends from the air rule over
/// <see cref="LandingBlend"/> instead of jumping.
/// </summary>
internal sealed class RideHeight
{
	/// <summary>Seconds over which landing blends the held air height into the ground height.</summary>
	internal const float LandingBlend = 0.5f;

	// Rising above take-off is followed by a share that grows from nothing toward RiseShare, half of it at RiseKnee (m).
	private const float RiseKnee = 0.6f;
	private const float RiseShare = 0.5f;

	private float _takeoff;
	private float _held;
	private float _sinceLanding = float.PositiveInfinity;
	private bool _airborne;

	internal float Value { get; private set; }

	internal void Reset( float height )
	{
		Value = _takeoff = _held = height;
		_sinceLanding = float.PositiveInfinity;
		_airborne = false;
	}

	/// <param name="height">The subject's height this frame (m).</param>
	/// <param name="airborne">True while the subject is in the air.</param>
	/// <param name="hold">False when the air rule does not apply (a tumbling ragdoll): the height is followed.</param>
	internal float Update( float height, bool airborne, bool hold, float delta )
	{
		if ( airborne && hold )
		{
			if ( !_airborne )
				_takeoff = height;
			_airborne = true;
			var above = height - _takeoff;
			_held = above <= 0f ? height : _takeoff + RiseShare * above * above / (above + RiseKnee);
			Value = _held;
			return Value;
		}
		if ( _airborne )
			_sinceLanding = 0f;
		_airborne = false;
		_sinceLanding += delta;
		Value = _sinceLanding < LandingBlend
			? _held + (height - _held) * CameraMath.Ease( _sinceLanding / LandingBlend )
			: height;
		return Value;
	}
}
