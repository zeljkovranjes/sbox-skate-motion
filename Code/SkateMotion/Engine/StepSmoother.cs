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

	// Where the object is shown easing over from (its offset from where it is, in its own frame), since when and for how
	// long (seconds; none when not positive).
	private Transform _easeOffset;
	private float _easeFrom;
	private float _easeLength;

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
	internal void Reset()
	{
		_ready = false;
		_easeLength = 0f;
	}

	/// <summary>
	/// Shows the object easing over to where it is from <paramref name="shown"/>, where it was shown a moment ago, over
	/// <paramref name="seconds"/>, the way still to go travelling with it: a board put down from the hand goes on from
	/// where the hand showed it to where physics put it, rather than appearing there.
	/// </summary>
	internal void EaseFrom( Transform shown, Transform actual, float now, float seconds )
	{
		_easeOffset = Smoothed( actual, now ).ToLocal( shown );
		_easeFrom = now;
		_easeLength = seconds;
	}

	/// <summary>
	/// The object was moved by <paramref name="offset"/> on top of its own motion: the recorded steps move with it, so it
	/// is shown going on from where it was shown, offset, instead of easing across the move or jumping ahead.
	/// </summary>
	internal void Shift( Vector3 offset )
	{
		_previous.Position += offset;
		_current.Position += offset;
	}

	/// <summary>
	/// The object was just put at <paramref name="placed"/> where <paramref name="other"/> was going (a board put down under
	/// the rider carrying it): it takes over the other's recorded steps, moved onto it and turned as it is placed, so it is
	/// shown going on as the other was shown, rather than standing a step behind and then jumping on.
	/// </summary>
	internal void Follow( StepSmoother other, Transform placed )
	{
		var offset = placed.Position - other._current.Position;
		_previous = placed.WithPosition( other._previous.Position + offset );
		_current = placed;
		_stepTime = other._stepTime;
		_stepLength = other._stepLength;
		_ready = other._ready;
	}

	/// <summary>
	/// The pose to show at <paramref name="now"/>, or <paramref name="actual"/> when no recent step was recorded
	/// (the object is not moving in fixed steps just now) or it has since been placed somewhere else entirely.
	/// </summary>
	internal Transform At( Transform actual, float now )
	{
		var at = Smoothed( actual, now );
		if ( !(_easeLength > 0f) || now - _easeFrom >= _easeLength )
			return at;
		var amount = Math.Clamp( (now - _easeFrom) / _easeLength, 0f, 1f );
		var left = 1f - amount * amount * (3f - 2f * amount);
		return at.ToWorld( new Transform( _easeOffset.Position * left, Rotation.Slerp( Rotation.Identity, _easeOffset.Rotation, left ),
			Vector3.One ) );
	}

	private Transform Smoothed( Transform actual, float now )
	{
		if ( !_ready || now - _stepTime > _stepLength * 2f || (actual.Position - _current.Position).Length > Placed )
			return actual;
		var amount = Math.Clamp( (now - _stepTime) / _stepLength, 0f, 1f );
		return new Transform( Vector3.Lerp( _previous.Position, _current.Position, amount ),
			Rotation.Slerp( _previous.Rotation, _current.Rotation, amount ), _current.Scale );
	}
}
