namespace SkateMotion.Core.Camera;

/// <summary>
/// A second-order follower for one camera value (a distance, a height, or an angle when wrapping). Each frame is
/// integrated in sub-steps of at most <see cref="MaxStep"/> with the target moved evenly from last frame's to this
/// frame's, so the path is the same at any frame rate, and the value's velocity and acceleration stay continuous
/// whenever its target is.
/// </summary>
internal sealed class CameraSpring
{
	/// <summary>Longest integration sub-step in seconds.</summary>
	internal const float MaxStep = 1f / 240f;

	private readonly bool _angle;
	private float _target;
	private bool _started;

	internal CameraSpring( bool angle = false ) => _angle = angle;

	internal float Value { get; private set; }
	internal float Velocity { get; private set; }

	/// <summary>Puts the value at rest (or moving at a velocity) on its target.</summary>
	internal void Reset( float value, float velocity = 0f )
	{
		Value = _angle ? CameraMath.Wrap( value ) : value;
		Velocity = velocity;
		_target = Value;
		_started = true;
	}

	/// <summary>Stops the value's motion where it is.</summary>
	internal void Stop() => Velocity = 0f;

	/// <summary>Advances toward the target; targetVelocity feeds the target's own motion forward, so a target moving
	/// steadily is followed without lag.</summary>
	internal float Step( float target, float delta, SpringTuning tuning, float targetVelocity = 0f )
	{
		if ( !_started )
			Reset( target, targetVelocity );
		if ( !(delta > 0f) )
			return Value;
		var change = _angle ? CameraMath.Wrap( target - _target ) : target - _target;
		var steps = Math.Max( 1, (int)MathF.Ceiling( delta / MaxStep ) );
		var step = delta / steps;
		for ( var index = 0; index < steps; index++ )
		{
			// The pull is worked out at the start of each sub-step (where the target is then), so a target followed
			// exactly with its velocity fed forward stays followed exactly.
			var aim = _target + change * index / steps;
			var error = _angle ? CameraMath.Wrap( aim - Value ) : aim - Value;
			var acceleration = tuning.Stiffness * error + tuning.Damping * (targetVelocity - Velocity);
			acceleration = Math.Clamp( acceleration, -tuning.MaxAcceleration, tuning.MaxAcceleration );
			Velocity = Math.Clamp( Velocity + acceleration * step, -tuning.MaxSpeed, tuning.MaxSpeed );
			Value += Velocity * step;
			if ( _angle )
				Value = CameraMath.Wrap( Value );
		}
		_target = _angle ? CameraMath.Wrap( target ) : target;
		return Value;
	}
}
