using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// Third-person follow camera. The mouse (or a free right stick) orbits it. While riding it follows the board's
/// heading and swings back behind the rider once the mouse rests; on foot it stays where the player turned it, so
/// walking directions stay put relative to the view.
/// </summary>
internal sealed class SkateCameraRig
{
	private Vector3 _focus;
	private float _heading;
	private float _yaw;
	private float _pitch;
	private float _offset;
	private float _idle;
	private bool _started;

	// Seconds the mouse must rest before a riding camera swings back behind the board, and how fast it does.
	private const float RecentreDelay = 1.2f;
	private const float RecentreRate = 2.5f;

	private const float PitchUp = -30f;
	private const float PitchDown = 35f;

	/// <param name="follow">True while riding: the view follows the board's heading.</param>
	/// <param name="look">This frame's orbit input, in degrees.</param>
	internal void Update( CameraComponent camera, GameObject target, float distance, float height, float delta,
		GameObject ignoredVisual, bool follow, Angles look )
	{
		var focus = target.WorldPosition + Vector3.Up * 22f;
		var heading = target.WorldRotation.Yaw();
		if ( !_started )
		{
			_focus = focus;
			_heading = heading;
			_yaw = heading;
			_started = true;
		}
		_focus = Vector3.Lerp( _focus, focus, 1f - MathF.Exp( -14f * delta ) );
		_heading = LerpDegrees( _heading, heading, 1f - MathF.Exp( -6f * delta ) );
		_idle = look.yaw != 0f || look.pitch != 0f ? 0f : _idle + delta;
		_pitch = Math.Clamp( _pitch + look.pitch, PitchUp, PitchDown );
		if ( follow )
		{
			_offset = Angles.NormalizeAngle( _offset + look.yaw );
			if ( _idle > RecentreDelay )
			{
				var recentre = 1f - MathF.Exp( -RecentreRate * delta );
				_offset *= 1f - recentre;
				_pitch *= 1f - recentre;
			}
			_yaw = _heading + _offset;
		}
		else
		{
			_yaw += look.yaw;
			_offset = Angles.NormalizeAngle( _yaw - _heading );
		}
		var length = MathF.Sqrt( distance * distance + height * height );
		var rest = MathF.Atan2( height, distance ) * (180f / MathF.PI);
		var view = Rotation.From( new Angles( rest + _pitch, _yaw, 0f ) );
		var wanted = _focus - view.Forward * length;
		var query = target.Scene.Trace.Sphere( 4f, _focus, wanted ).IgnoreGameObjectHierarchy( target );
		if ( ignoredVisual.IsValid() ) query = query.IgnoreGameObjectHierarchy( ignoredVisual );
		var trace = query.Run();
		camera.WorldPosition = trace.Hit ? trace.EndPosition : wanted;
		camera.WorldRotation = Rotation.LookAt( _focus - camera.WorldPosition, Vector3.Up );
	}

	private static float LerpDegrees( float from, float to, float amount ) =>
		from + Angles.NormalizeAngle( to - from ) * amount;
}
