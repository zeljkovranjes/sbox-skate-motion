namespace SkateMotion.Core.Camera;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Decides each frame where a skate follow camera wants to be: which framing (riding, grinding, manual, on foot,
/// bailed), the heading it settles behind, the height it follows, the player's orbit, the drop look-down and how far
/// ahead it aims. The engine side then fits the result to the world through <see cref="Boom"/>.
/// </summary>
internal sealed class CameraDirector
{
	/// <summary>Elevation limits (radians) for the camera above the focus, and how far from them it starts to ease in.</summary>
	internal const float LowestElevation = -12f * CameraMath.Degrees;
	internal const float HighestElevation = 70f * CameraMath.Degrees;
	internal const float ElevationKnee = 8f * CameraMath.Degrees;

	// Measured riding heading response: an overdamped chase that settles in about 0.75 s and trails a steady turn by
	// about 0.8 s of it.
	private static readonly SpringTuning Chase = new( 19.68f, 16.09f );

	// Height follow (rad/s) while the shot's own follow is quicker.
	private const float VerticalFollow = 8f;

	// Measured framing growth per m/s of speed (m), up to SpeedRange (m/s).
	private const float DistancePerSpeed = 0.0125f;
	private const float HeightPerSpeed = 0.013f;
	private const float SpeedRange = 9f;

	// How quickly (rad/s) the framing takes up a change of speed, so a sudden stop never jumps the view.
	private static readonly SpringTuning SpeedFollow = SpringTuning.Critical( 2f );

	// How quickly (1/s) the fed-forward velocity takes up a change, so an impact eases the camera instead of jolting it.
	private const float FeedResponse = 30f;

	// Farthest the view aims ahead of the focus (m).
	private const float MostLookAhead = 1f;

	// A subject that moved further in one frame than PlacedReach times its speed allows, and more than PlacedLeast
	// (m), was placed there: the camera starts again.
	private const float PlacedReach = 3f;
	private const float PlacedLeast = 0.5f;

	// Height (m) of the board above the ground below it until it has been seen on the ground, and the most counted.
	private const float UsualClearance = 0.1f;
	private const float MostClearance = 0.5f;

	private readonly CameraSpring _x = new();
	private readonly CameraSpring _y = new();
	private readonly CameraSpring _z = new();
	private readonly CameraSpring _heading = new( angle: true );
	private readonly CameraSpring _speed = new();
	private readonly TravelHeading _travel = new();
	private readonly RideHeight _height = new();
	private readonly ShotBlend _shots = new();
	private readonly DropLook _drop = new();
	private Vector3 _base;
	private Vector3 _feed;
	private Vector3 _source;
	private CameraSubjectMode _mode;
	private float _held;
	private float _clearance = UsualClearance;
	private float _takeoffGround = float.PositiveInfinity;
	private float _lastSpeed;
	private bool _started;

	/// <summary>The player's own turn of the camera.</summary>
	internal CameraOrbit Orbit { get; } = new();

	/// <summary>Distance and craning against the world.</summary>
	internal CameraBoom Boom { get; } = new();

	/// <summary>The framing in use.</summary>
	internal CameraShot Shot => _shots.Value;

	/// <summary>Follow distance (m) behind the focus at rest, as set by the designer.</summary>
	internal float Distance { get; set; } = 2.6f;

	/// <summary>Camera height (m) above the focus at rest, as set by the designer.</summary>
	internal float Height { get; set; } = 0.65f;

	/// <summary>
	/// Works out this frame's aim from the subject, the ground ahead of it and the look input (radians; pitch positive
	/// looks down).
	/// </summary>
	internal CameraAim Update( CameraSubject subject, CameraGround ground, float lookYaw, float lookPitch, float delta )
	{
		var source = subject.Mode == CameraSubjectMode.Riding ? subject.Board : subject.Rider;
		var moved = (source - _source).Length();
		var reach = PlacedReach * MathF.Max( subject.Velocity.Length(), _lastSpeed ) * delta + PlacedLeast;
		// A tumbling ragdoll is never placed; it is only flung about.
		var teleported = !_started || moved > reach && subject.Mode == _mode && subject.Mode != CameraSubjectMode.Bailed;
		_lastSpeed = subject.Velocity.Length();
		if ( teleported )
		{
			// Placed: no time passes for the follow this frame, so it starts exactly where the subject is.
			Start( subject );
			delta = 0f;
		}
		else if ( subject.Mode != _mode )
			ChangeMode( subject );
		_source = source;
		var shot = _shots.Update( Choose( subject ), subject.Mode != _mode, delta );
		_mode = subject.Mode;

		var position = Vector3.Lerp( subject.Rider, subject.Board, shot.Board );
		var velocity = delta > 0f ? (position - _base) / delta : _feed;
		_base = position;
		_feed += (velocity - _feed) * (1f - MathF.Exp( -FeedResponse * delta ));
		var riding = subject.Mode == CameraSubjectMode.Riding;
		var height = _height.Update( position.Z, subject.Airborne, subject.Mode != CameraSubjectMode.Bailed, delta );
		var follow = SpringTuning.Critical( shot.Follow );
		_x.Step( position.X, delta, follow, _feed.X * shot.FeedForward );
		_y.Step( position.Y, delta, follow, _feed.Y * shot.FeedForward );
		_z.Step( height, delta, SpringTuning.Critical( MathF.Min( shot.Follow, VerticalFollow ) ) );

		var heading = riding ? _travel.Update( subject.Velocity, subject.Facing, subject.Airborne, delta ) : _held;
		_heading.Step( heading, delta, Chase );

		var planar = new Vector3( subject.Velocity.X, subject.Velocity.Y, 0f ).Length();
		var speed = _speed.Step( MathF.Min( planar, SpeedRange ), delta, SpeedFollow );
		_drop.Update( ground, DropReference( subject, ground, position.Z ), subject.Mode != CameraSubjectMode.Riding, planar,
			delta );
		var distance = Distance + DistancePerSpeed * speed;
		var rise = Height + HeightPerSpeed * speed;
		var rest = MathF.Atan2( rise, distance ) * shot.Elevation;
		Orbit.Update( lookYaw, lookPitch, riding, LowestElevation - rest, HighestElevation - rest, delta );
		var elevation = CameraMath.SoftClamp( rest + Orbit.Pitch + _drop.Value, LowestElevation, HighestElevation,
			ElevationKnee );

		var followed = new Vector3( _x.Value, _y.Value, _z.Value );
		var focus = followed + Vector3.UnitZ * (subject.FocusHeight * shot.Focus);
		var yaw = CameraMath.Wrap( _heading.Value + Orbit.Yaw );
		// The view aims ahead only along itself, so looking ahead never turns it: the mouse stays one to one and
		// walking directions stay put.
		var view = new Vector3( MathF.Cos( yaw ), MathF.Sin( yaw ), 0f );
		var ahead = MathF.Max( 0f, Vector3.Dot( new Vector3( _x.Velocity, _y.Velocity, 0f ), view ) ) * shot.LookAhead;
		return new CameraAim( followed, focus, focus + view * MathF.Min( ahead, MostLookAhead ), yaw, elevation,
			MathF.Sqrt( distance * distance + rise * rise ) * shot.Distance, teleported );
	}

	// Drops are measured from the ground the subject stands on, and in the air from the ground it left (an air off flat
	// ground looks down into nothing) or, lower, from where it is now (falling off a ledge, the look-down eases off as
	// the ground comes up). Ground far below a subject not reported in the air (placed in mid-air) is not stood on.
	private float DropReference( CameraSubject subject, CameraGround ground, float height )
	{
		if ( !subject.Airborne && !float.IsNaN( ground.Below ) && height - ground.Below <= MostClearance )
		{
			_clearance = Math.Clamp( height - ground.Below, 0f, MostClearance );
			_takeoffGround = ground.Below;
		}
		return subject.Airborne ? MathF.Min( _takeoffGround, height - _clearance ) : height - _clearance;
	}

	private static CameraShot Choose( CameraSubject subject ) => subject.Mode switch
	{
		CameraSubjectMode.Bailed => CameraShot.Bail,
		CameraSubjectMode.OnFoot => CameraShot.OnFoot,
		_ when subject.Grinding => CameraShot.Grind,
		_ when subject.Manual && !subject.Airborne => CameraShot.Manual,
		_ => CameraShot.Chase
	};

	// Everything starts again where the subject is, behind the way it travels (or faces), with the player's turn and
	// any craning cleared.
	private void Start( CameraSubject subject )
	{
		_started = true;
		_mode = subject.Mode;
		var shot = Choose( subject );
		_shots.Reset( shot );
		_base = Vector3.Lerp( subject.Rider, subject.Board, shot.Board );
		var planar = new Vector3( subject.Velocity.X, subject.Velocity.Y, 0f );
		var heading = planar.Length() > 1f ? CameraMath.Heading( planar ) : subject.Facing;
		_feed = new Vector3( subject.Velocity.X, subject.Velocity.Y, 0f );
		_x.Reset( _base.X, _feed.X * shot.FeedForward );
		_y.Reset( _base.Y, _feed.Y * shot.FeedForward );
		_z.Reset( _base.Z );
		_height.Reset( _base.Z );
		_travel.Reset( heading );
		_heading.Reset( heading );
		_speed.Reset( MathF.Min( planar.Length(), SpeedRange ) );
		_held = heading;
		_takeoffGround = float.PositiveInfinity;
		Orbit.Reset();
		_drop.Reset();
		Boom.Reset( MathF.Sqrt( Distance * Distance + Height * Height ) * shot.Distance );
	}

	// Leaving the board the view holds its heading; getting on, the follow heading takes over the board's direction
	// while the player's offset absorbs the difference, so nothing moves until the camera eases round behind.
	private void ChangeMode( CameraSubject subject )
	{
		if ( subject.Mode != CameraSubjectMode.Riding )
		{
			_held = _heading.Value;
			return;
		}
		if ( _mode == CameraSubjectMode.Riding )
			return;
		var view = CameraMath.Wrap( _heading.Value + Orbit.Yaw );
		var planar = new Vector3( subject.Velocity.X, subject.Velocity.Y, 0f );
		var heading = planar.Length() > 1f ? CameraMath.Heading( planar ) : CameraMath.NearerEnd( subject.Facing, view );
		_travel.Reset( heading );
		Orbit.Shift( CameraMath.Wrap( _heading.Value - heading ) );
		_heading.Reset( heading, _heading.Velocity );
	}
}
