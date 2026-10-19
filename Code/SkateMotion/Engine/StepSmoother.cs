using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// Shows an object moved in fixed steps smoothly between them: each step's transform is recorded and frames are
/// given the pose interpolated between the last two (one step behind). Without it a body that moves at the fixed
/// rate while its animation runs every frame steps visibly, and a planted foot slides back and jumps forward.
/// </summary>
internal sealed class StepSmoother
{
	private Transform _previous;
	private Transform _current;
	private float _stepTime;
	private float _stepLength;
	private bool _ready;

	// Inches moved in one step beyond which the object counts as placed (teleported) rather than moving.
	private const float Placed = 48f;

	/// <summary>Records the transform an object has after a fixed step.</summary>
	internal void Record( Transform transform, float now, float step )
	{
		if ( !_ready || (transform.Position - _current.Position).Length > Placed )
			_current = transform;
		_previous = _current;
		_current = transform;
		_stepTime = now;
		_stepLength = step > 0f ? step : _stepLength;
		_ready = _stepLength > 0f;
	}

	/// <summary>Forgets the recorded steps, so the next frame shows the object where it is.</summary>
	internal void Reset() => _ready = false;

	/// <summary>
	/// The pose to show at <paramref name="now"/>, or <paramref name="actual"/> when no recent step was recorded
	/// (the object is not moving in fixed steps just now) or it has since been placed somewhere else entirely.
	/// </summary>
	internal Transform At( Transform actual, float now )
	{
		if ( !_ready || now - _stepTime > _stepLength * 2f || (actual.Position - _current.Position).Length > Placed )
			return actual;
		var amount = Math.Clamp( (now - _stepTime) / _stepLength, 0f, 1f );
		return new Transform( Vector3.Lerp( _previous.Position, _current.Position, amount ),
			Rotation.Slerp( _previous.Rotation, _current.Rotation, amount ), _current.Scale );
	}
}
