namespace SkateMotion.Core.Camera;

/// <summary>
/// The player's own turn of the camera, as yaw and pitch offsets from where it would sit. Look input applies
/// directly (no smoothing, so the mouse feels one to one). While recentring is allowed (riding) the offsets ease
/// back to zero once the input has rested for <see cref="RecentreDelay"/>: never while the mouse moves, starting
/// gently and stopping the moment it moves again.
/// </summary>
internal sealed class CameraOrbit
{
	/// <summary>Seconds the look input must rest before the camera recentres.</summary>
	internal float RecentreDelay { get; set; } = 1.2f;

	// Natural frequency (rad/s) of the recentre, and seconds over which its pull builds up from nothing.
	private const float RecentreFrequency = 3.5f;
	private const float RecentreBuild = 0.4f;

	private readonly CameraSpring _yaw = new( angle: true );
	private readonly CameraSpring _pitch = new();
	private float _idle;
	private float _recentring;

	internal float Yaw => _yaw.Value;
	internal float Pitch => _pitch.Value;

	/// <summary>Seconds since the last look input.</summary>
	internal float Idle => _idle;

	internal void Reset()
	{
		_yaw.Reset( 0f );
		_pitch.Reset( 0f );
		_idle = 0f;
		_recentring = 0f;
	}

	/// <summary>Adds to the yaw offset without counting as input, keeping the view where it is while what it is
	/// measured from moves (the follow heading taking over a new direction).</summary>
	internal void Shift( float yaw ) => _yaw.Reset( _yaw.Value + yaw, _yaw.Velocity );

	/// <summary>
	/// Applies this frame's look input (radians; pitch positive looks down) and the recentre. Input never takes the
	/// pitch outside the offsets that keep the camera's elevation inside its limits.
	/// </summary>
	internal void Update( float yaw, float pitch, bool recentre, float lowestPitch, float highestPitch, float delta )
	{
		if ( yaw != 0f || pitch != 0f )
		{
			_idle = 0f;
			_recentring = 0f;
			_yaw.Reset( _yaw.Value + yaw );
			_pitch.Reset( Math.Clamp( _pitch.Value + pitch, lowestPitch, highestPitch ) );
			return;
		}
		_idle += delta;
		if ( !recentre || _idle < RecentreDelay )
		{
			_recentring = 0f;
			_yaw.Reset( _yaw.Value );
			_pitch.Reset( _pitch.Value );
			return;
		}
		// The pull builds up from the moment recentring may start (the mouse resting, or just getting on the board).
		var build = CameraMath.Ease( _recentring / RecentreBuild );
		_recentring += delta;
		var pull = RecentreFrequency * MathF.Sqrt( build );
		var tuning = new SpringTuning( pull * pull, 2f * pull );
		_yaw.Step( 0f, delta, tuning );
		_pitch.Step( 0f, delta, tuning );
	}
}
