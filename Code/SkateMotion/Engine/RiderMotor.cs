using Sandbox;

namespace SkateMotion.Engine;

internal sealed class RiderMotor
{
	private readonly GameObject _rider;
	private readonly GameObject _board;
	private readonly Rigidbody _body;
	private readonly CharacterController _walker;
	private GameObject _boardParent;
	private Transform? _carryPose;
	private bool _retrieving;
	internal SkateRiderMode Mode { get; private set; } = SkateRiderMode.Riding;
	internal Vector3 Velocity => Mode == SkateRiderMode.Riding ? _body.Velocity : _walker.Velocity;
	internal bool Grounded => _walker.IsOnGround;

	/// <summary>
	/// Where the rider is shown after a fixed step of <paramref name="step"/> seconds. Standing on a slope the body's box
	/// rests on its uphill edge, above the ground under its middle, so the rider is shown down on that ground instead of
	/// hovering over it; the drop eases in and out, so landing on a slope or leaving one never jumps.
	/// </summary>
	internal Transform ShownTransform( float step )
	{
		var transform = _rider.WorldTransform;
		var gap = Mode is SkateRiderMode.Riding or SkateRiderMode.Bailed || !_walker.IsOnGround ? 0f : GroundGap();
		var drop = gap > 0f && gap <= _walker.Radius ? gap : 0f;
		var most = DropRate * step;
		_shownDrop += Math.Clamp( drop - _shownDrop, -most, most );
		return _shownDrop > 0f ? transform.WithPosition( transform.Position + Vector3.Down * _shownDrop ) : transform;
	}

	// How far (inches) the rider is shown below its body on a slope, and how fast (inches per second) that changes.
	private float _shownDrop;
	private const float DropRate = 100f;

	/// <summary>
	/// True while the walking rider is in the air for its animation: rising off the ground, or clear of it by more than a
	/// step (stepping down off the deck is not a fall); once in the air, until it touches down. Coming down from a jump
	/// it would otherwise land a step above the ground, while the jump's clip, timed to the touch down, was still in the
	/// air: the landing cut in up to 29 degrees at the legs.
	/// </summary>
	internal bool Airborne => !_walker.IsOnGround && (_flying || Core.Animation.BipedFooting.Airborne(
		_walker.Velocity.z / SkateUnits.InchesPerMetre, GroundGap() / SkateUnits.InchesPerMetre ));

	// Whether the walking rider has been in the air since it last stood on the ground.
	private bool _flying;
	// A board can be called back to the hand from as far as RecallRange (metres), in sight unless RecallNeedsSight is off,
	// rolling or not.
	internal bool CanPickUp => Mode == SkateRiderMode.Walking
		&& Reachable( RecallRange * SkateUnits.InchesPerMetre, RecallNeedsSight );

	/// <summary>Farthest (metres) a board is called back to the hand from.</summary>
	internal float RecallRange { get; set; } = Core.Physics.BoardReach.Farthest;

	/// <summary>Whether only a board in sight can be called back; off, it comes through whatever is in the way.</summary>
	internal bool RecallNeedsSight { get; set; } = true;
	// A board lying flat either way up can be got on: upside down, it is turned back over as the rider steps on.
	internal bool CanMount => Mode == SkateRiderMode.Walking && Reachable( MountReach )
		&& MathF.Abs( Vector3.Dot( _board.WorldRotation.Up, Vector3.Up ) ) >= 0.6f;

	// Farthest (inches) from a board on the ground the rider steps onto it.
	private const float MountReach = 64f;
	internal Transform CarryPose => _carryPose is { } pose ? _rider.WorldTransform.ToWorld( pose ) : _rider.WorldTransform.ToWorld(
		new Transform( new Vector3( 0f, -17f, 29f ), Rotation.FromRoll( 90f ) ) );
	internal bool HasCarryPose => _carryPose.HasValue;
	internal Transform DropPose => new( _rider.WorldPosition + _rider.WorldRotation.Forward * 30f + Vector3.Up * 8f,
		_rider.WorldRotation );
	internal Vector3 DismountPosition { get; private set; }
	internal Vector3 RidingPosition => _board.WorldPosition + _board.WorldRotation.Up * 0.6f;

	internal RiderMotor( GameObject rider, GameObject board, Rigidbody body, CharacterController walker )
	{
		_rider = rider;
		_board = board;
		_body = body;
		_walker = walker;
		_walker.Radius = WalkRadius;
		_walker.Height = 68f;
		_walker.StepHeight = 14f;
		_walker.Acceleration = 20f;
		_walker.Enabled = false;
		_board.Tags.Add( SkateTags.Board );
	}

	// The walking body's half width (inches), and how narrow it is while stepping off the board: narrow, it drops past a
	// rail, a ledge or the deck as soon as it is beside them, instead of standing on them with the edge of its hull.
	private const float WalkRadius = 12f;
	private const float StepRadius = 4f;

	// Points tried along the step for a clear place to start it.
	private const int ClearSamples = 8;

	// Farthest below the rider (inches) the ground is looked for.
	private const float GroundReach = 2000f;

	// Metres per second squared.
	private const float Gravity = 9.81f;

	internal void FollowBoard()
	{
		if ( Mode != SkateRiderMode.Riding || !_board.IsValid() )
			return;
		_rider.WorldPosition = _board.WorldPosition + _board.WorldRotation.Up * 0.6f;
		// The rider stays upright through flip tricks; feet are released by the animation adapter.
		_rider.WorldRotation = Rotation.FromYaw( _board.WorldRotation.Yaw() );
	}

	internal void UpdateCarryPose( Transform? worldPose, bool follow )
	{
		_carryPose = Mode != SkateRiderMode.Riding && worldPose is { } pose
			? _rider.WorldTransform.ToLocal( pose ) : null;
		// Pickup and drop retain ownership until their swept movement finishes.
		if ( follow && Mode == SkateRiderMode.Carrying && _carryPose.HasValue )
			_board.WorldTransform = CarryPose;
	}

	// Walking and running (m/s): a gentle walk that quickens with the stick, a run at full stick, and no faster than
	// CarrySpeed with the board in hand. Speed changes at up to Acceleration (m/s^2).
	private static readonly Core.Vehicle.LinearCurve StickSpeed = new(
		new[] { 0.036f, 0.938f, 0.943f },
		new[] { 1.49f, 2.68f, 6f } );
	private const float CarrySpeed = 4.32f;
	private const float RunSpeed = 6f;
	private readonly Core.Vehicle.SprintBoost _sprint = new();
	private const float Acceleration = 12f;
	private const float TravelAcceleration = 80f;

	// Fastest the body turns toward the walking direction (degrees per second) against speed (m/s).
	private static readonly Core.Vehicle.LinearCurve TurnSpeed = new(
		new[] { 0f, 4.65f, 6.2f, 10.7f },
		new[] { 500f, 462f, 288f, 205f } );

	/// <summary>
	/// Whether walking answers the controls as a game's own player controller does: speed taken up and let go within a
	/// fraction of a second, turns kept at speed with the body swinging round with them, and the stick taking over from a
	/// landing's clip. Off, the rider eases into and out of a walk as a skater on foot does.
	/// </summary>
	internal bool Responsive { get; set; }

	// Responsive walking: speed changes (m/s^2) starting and stopping, how fast the way it goes turns toward the stick
	// and how fast the body follows (degrees per second).
	private const float ResponsiveStart = 30f;
	private const float ResponsiveStop = 25f;
	private const float ResponsiveSteer = 1080f;
	private const float ResponsiveTurn = 900f;

	// Jump take-off speed (m/s) and the share of ground speed kept into it.
	private const float JumpSpeed = 4.2f;
	private const float JumpCarry = 0.8f;

	/// <param name="turn">Whether the body turns toward the way it walks (false while something else turns it).</param>
	/// <param name="travel">While a landing's clip carries the body on over its planted feet, how its root travels (inches
	/// per second): the walker goes so instead of as the stick has it.</param>
	internal void Walk( Vector3 direction, bool jump, float delta, bool sprint = false, bool turn = true, Vector3? travel = null )
	{
		if ( Mode is SkateRiderMode.Riding or SkateRiderMode.Bailed )
		{
			_sprint.Reset();
			_flying = false;
			return;
		}
		if ( _walker.Radius < WalkRadius )
			Widen();
		var stick = direction.WithZ( 0 ).ClampLength( 1f );
		var speed = stick.Length < 0.036f ? 0f : StickSpeed.Evaluate( stick.Length );
		// Sprinting lifts a run's top speed, and it eases back down after letting go.
		var running = speed >= RunSpeed;
		var top = _sprint.TopSpeed( sprint && running && _walker.IsOnGround, RunSpeed, delta );
		if ( running )
			speed = top;
		if ( Mode == SkateRiderMode.Carrying )
			speed = MathF.Min( speed, CarrySpeed );
		var wish = stick.Normal * (speed * SkateUnits.InchesPerMetre);
		// Pushing the stick, responsive walking goes where it points rather than where a landing's clip carries it.
		if ( Responsive && wish.LengthSquared > 1f )
			travel = null;
		if ( _walker.IsOnGround && Responsive && travel is null )
			_walker.Velocity = Steer( _walker.Velocity.WithZ( 0f ), wish, delta ).WithZ( _walker.Velocity.z );
		else if ( _walker.IsOnGround )
		{
			// Walking speed moves toward the wished velocity at a set rate: starting, turning, and stopping when the
			// stick is let go or a runout ends.
			var planar = _walker.Velocity.WithZ( 0f );
			// From a standstill the rider steps straight off at the slowest walking pace.
			var slowest = StickSpeed.Evaluate( 0f ) * SkateUnits.InchesPerMetre;
			if ( travel is null && wish.LengthSquared > 1f && planar.Length < slowest )
				planar = wish.Normal * slowest;
			// A landing goes as its clip's root travels, taken up at TravelAcceleration (a landing from a stand does not
			// lurch on in a frame), so the feet it plants stay where they land.
			var step = (travel?.WithZ( 0f ) ?? wish) - planar;
			var most = (travel is null ? Acceleration : TravelAcceleration) * SkateUnits.InchesPerMetre * delta;
			if ( step.Length > most )
				step = step.Normal * most;
			_walker.Velocity = (planar + step).WithZ( _walker.Velocity.z );
		}
		else
		{
			_walker.ApplyFriction( 0.1f, 140f );
			_walker.Accelerate( wish );
		}
		if ( jump && _walker.IsOnGround )
		{
			_walker.Velocity = _walker.Velocity.WithZ( 0f ) * JumpCarry;
			_walker.Punch( Vector3.Up * (JumpSpeed * SkateUnits.InchesPerMetre) );
		}
		_walker.Velocity += Vector3.Down * (Gravity * SkateUnits.InchesPerMetre * delta);
		var before = _rider.WorldPosition;
		_walker.Move();
		Unwedge( before );
		_flying = Airborne;
		MeasureGround( delta );
		MeasureEdge();
		// The body turns toward the walking direction no faster than a rate that falls with speed, so it never
		// snaps round.
		if ( turn && wish.LengthSquared > 1f )
		{
			var rate = TurnSpeed.Evaluate( _walker.Velocity.WithZ( 0f ).Length / SkateUnits.InchesPerMetre );
			if ( Responsive )
				rate = MathF.Max( rate, ResponsiveTurn );
			var current = _rider.WorldRotation.Yaw();
			var wanted = Rotation.LookAt( wish ).Yaw();
			var change = Math.Clamp( MathX.DeltaDegrees( current, wanted ), -rate * delta, rate * delta );
			_rider.WorldRotation = Rotation.FromYaw( current + change );
		}
	}

	// Responsive walking on the ground: the way it goes turns toward the stick at ResponsiveSteer keeping its speed, and
	// the speed goes to the stick's at ResponsiveStart or ResponsiveStop, so a turn never slows it into a stop.
	private static Vector3 Steer( Vector3 planar, Vector3 wish, float delta )
	{
		var speed = planar.Length;
		var wanted = wish.Length;
		var way = speed > 1f ? planar / speed : wish.LengthSquared > 1f ? wish.Normal : Vector3.Zero;
		if ( wanted > 1f && speed > 1f )
		{
			var from = MathF.Atan2( way.y, way.x ) * 180f / MathF.PI;
			var to = MathF.Atan2( wish.y, wish.x ) * 180f / MathF.PI;
			var turned = from + Math.Clamp( MathX.DeltaDegrees( from, to ), -ResponsiveSteer * delta, ResponsiveSteer * delta );
			way = Rotation.FromYaw( turned ).Forward;
		}
		var rate = (wanted > speed ? ResponsiveStart : ResponsiveStop) * SkateUnits.InchesPerMetre * delta;
		speed = wanted > speed ? MathF.Min( speed + rate, wanted ) : MathF.Max( speed - rate, wanted );
		return way * speed;
	}

	// The ground the rider walks on, classed for the walking clips: flat, a ramp or stairs, up or down.
	private readonly Core.Animation.GroundSlope _slope = new();

	/// <summary>The ground the rider walks on as the walking clips name it (flat, ramp-up-shallow, stair-dn-steep, ...).</summary>
	internal string GroundSlope => _slope.Current;

	// The ground is measured along the way the rider walks, over SlopeAhead inches, once it walks faster than SlopeSpeed
	// (inches per second); ground whose up axis is within TreadUp of straight up is a stair's tread, not a ramp.
	private const float SlopeAhead = 16f;
	private const float SlopeSpeed = 10f;
	private const float TreadUp = 0.985f;

	private void MeasureGround( float delta )
	{
		var planar = _walker.Velocity.WithZ( 0f );
		var measured = Core.Animation.GroundSlope.Flat;
		if ( _walker.IsOnGround && planar.Length > SlopeSpeed )
		{
			var way = planar.Normal;
			var position = _rider.WorldPosition;
			if ( GroundAt( position ) is { } here && GroundAt( position + way * (SlopeAhead * 0.5f) ) is { } middle
				&& GroundAt( position + way * SlopeAhead ) is { } ahead )
			{
				// Stairs: flat treads at every sample, with the ground stepping between them.
				var stepped = here.Up > TreadUp && middle.Up > TreadUp && ahead.Up > TreadUp;
				measured = Core.Animation.GroundSlope.Classify( (ahead.Height - here.Height) / SkateUnits.InchesPerMetre,
					SlopeAhead / SkateUnits.InchesPerMetre, stepped );
			}
		}
		_slope.Update( measured, delta );
	}

	/// <summary>
	/// Metres ahead to the lip of a ramp the rider could drop in from with the board in hand (see DropInEdge), or infinity
	/// with none; with <see cref="EdgeLip"/> where the lip is at the feet's height and <see cref="EdgeWay"/> the level way
	/// down over it.
	/// </summary>
	internal float EdgeDistance { get; private set; } = float.PositiveInfinity;

	internal Vector3 EdgeLip { get; private set; }

	internal Vector3 EdgeWay { get; private set; }

	// The ground ahead is sampled every EdgeSpacing inches for a lip, looked for as far down as EdgeDepth inches.
	private const float EdgeSpacing = 4f;
	private const float EdgeDepth = 120f;
	private readonly float[] _edgeHeights = new float[ (int)(Core.Vehicle.DropInEdge.Reach * SkateUnits.InchesPerMetre / EdgeSpacing) + 6 ];
	private readonly float[] _edgeUps = new float[ (int)(Core.Vehicle.DropInEdge.Reach * SkateUnits.InchesPerMetre / EdgeSpacing) + 6 ];

	private void MeasureEdge()
	{
		EdgeDistance = float.PositiveInfinity;
		if ( Mode != SkateRiderMode.Carrying || !_walker.IsOnGround )
			return;
		var way = _rider.WorldRotation.Forward.WithZ( 0f ).Normal;
		var feet = _rider.WorldPosition;
		for ( var i = 0; i < _edgeHeights.Length; i++ )
		{
			var point = feet + way * (EdgeSpacing * (i + 1));
			var hit = _rider.Scene.Trace.Ray( point + Vector3.Up * _walker.StepHeight, point + Vector3.Down * EdgeDepth )
				.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
			_edgeHeights[ i ] = hit.Hit && !hit.StartedSolid ? (hit.HitPosition.z - feet.z) / SkateUnits.InchesPerMetre : float.NaN;
			_edgeUps[ i ] = hit.Hit && !hit.StartedSolid ? hit.Normal.z : float.NaN;
		}
		if ( Core.Vehicle.DropInEdge.Find( _edgeHeights, _edgeUps, EdgeSpacing / SkateUnits.InchesPerMetre ) is not { } distance )
			return;
		EdgeDistance = distance;
		EdgeLip = feet + way * (distance * SkateUnits.InchesPerMetre);
		EdgeWay = way;
	}

	/// <summary>Drops in from where the held board is (see the overload taking where it is shown).</summary>
	internal bool DropIn( Vector3 lip, Vector3 way, float rideHeight, float speed ) =>
		DropIn( lip, way, _board.WorldTransform, rideHeight, speed );

	/// <summary>
	/// Drops in: puts the held board on the ramp under where it is shown (<paramref name="shown"/>, tipped over the lip as
	/// the clips tip it), or else just past the lip, lying along the slope, its tail toward the lip, and gets on it, rolling
	/// down the slope at <paramref name="speed"/> inches per second.
	/// </summary>
	internal bool DropIn( Vector3 lip, Vector3 way, Transform shown, float rideHeight, float speed )
	{
		if ( Mode != SkateRiderMode.Carrying )
			return false;
		var over = lip + way * DropInPastLip;
		var under = SurfaceUnder( shown );
		// Under the shown board only on the slope (or the wall): the deck the rider stands on is no place to drop in on.
		if ( under is { } beneath && beneath.Normal.z > SlopeLeast )
			under = null;
		// Then the nearest point of that slope to the shown board, straight across to it: on a steep wall the board's own down
		// meets it well below the lip (19 in on the quarterpipe), while the clip has the board beside it, by the lip.
		if ( under is { } face )
		{
			var across = _rider.Scene.Trace.Ray( shown.Position + face.Normal * 12f, shown.Position - face.Normal * 30f )
				.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
			if ( across.Hit && !across.StartedSolid && across.Normal.z <= SlopeLeast )
				under = (across.HitPosition, across.Normal);
		}
		if ( under is null )
		{
			var slope = _rider.Scene.Trace.Ray( over + Vector3.Up * 24f, over + Vector3.Down * EdgeDepth )
				.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
			if ( !slope.Hit || slope.StartedSolid )
				return false;
			under = (slope.HitPosition, slope.Normal);
		}
		var (point, up) = under.Value;
		// Down the slope: the way down it, or on near-level ground the way over the lip.
		var fall = Vector3.Down - up * Vector3.Dot( Vector3.Down, up );
		var down = fall.Length > 0.2f ? fall.Normal : (way - up * Vector3.Dot( way, up )).Normal;
		Reparent( _boardParent.IsValid() ? _boardParent : _rider.Scene );
		_board.WorldTransform = new Transform( point + up * rideHeight, Rotation.LookAt( down, up ), _board.WorldScale );
		_body.MotionEnabled = true;
		_body.Velocity = down * speed;
		_body.AngularVelocity = Vector3.Zero;
		Mode = SkateRiderMode.Riding;
		_walker.Enabled = false;
		FollowBoard();
		return true;
	}

	// How far past the lip (inches) a board dropping in is put on the ramp: its tail just over the lip.
	internal const float DropInPastLip = 14f;

	// Steepest ground (its up's height, 1 flat) a board dropping in does not count as the slope below the lip: 10 degrees.
	private const float SlopeLeast = 0.985f;

	/// <summary>
	/// The ground under a board shown at <paramref name="shown"/>, looked for along its own down from a little above it
	/// (a board tipped onto a steep ramp meets the ramp, not the ground far below): where it is and which way is up there.
	/// </summary>
	internal (Vector3 Point, Vector3 Normal)? SurfaceUnder( Transform shown )
	{
		var up = shown.Rotation.Up;
		var hit = _rider.Scene.Trace.Ray( shown.Position + up * 12f, shown.Position - up * 30f )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		return hit.Hit && !hit.StartedSolid ? (hit.HitPosition, hit.Normal) : null;
	}

	// The ground's height under a point and how upright it is there, looked for from a step above to a step and a half
	// below; null where there is none, or the look starts inside something.
	private (float Height, float Up)? GroundAt( Vector3 point )
	{
		var hit = _rider.Scene.Trace.Ray( point + Vector3.Up * (_walker.StepHeight + 2f), point + Vector3.Down * (_walker.StepHeight * 1.5f + 16f) )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		return hit.Hit && !hit.StartedSolid ? (hit.HitPosition.z, hit.Normal.z) : null;
	}

	// Sideways step (m/s) taken when stepping off a board moving faster than RunoutStepSpeed (inches per second).
	private const float StepAway = 0.75f;
	private const float RunoutStepSpeed = 40f;

	internal bool Dismount( bool immediate = true )
	{
		if ( Mode != SkateRiderMode.Riding )
			return false;
		// Room beside the board is measured for the whole body.
		_walker.Radius = WalkRadius;
		for ( var side = 1; side >= -1; side -= 2 )
		{
			if ( StepOffSpot( side ) is not { } position )
				continue;
			DismountPosition = position;
			if ( immediate ) _rider.WorldPosition = position;
			// The rider keeps the board's momentum and steps a little away from it.
			_walker.Velocity = _body.Velocity + (_body.Velocity.Length > RunoutStepSpeed
				? _rider.WorldRotation.Left * (side * StepAway * SkateUnits.InchesPerMetre) : Vector3.Zero);
			// Footing is found again from here, not kept from wherever the rider last walked.
			_walker.IsOnGround = false;
			_walker.Enabled = true;
			Mode = SkateRiderMode.Walking;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Takes a riding rider off its board onto its feet at <paramref name="place"/>, wherever the board is: a game putting
	/// it where a player stands needs no room beside the board.
	/// </summary>
	internal bool StandAt( Transform place )
	{
		if ( Mode != SkateRiderMode.Riding )
			return false;
		_walker.Radius = WalkRadius;
		_rider.WorldTransform = place;
		_rider.Transform.ClearInterpolation();
		_walker.Velocity = Vector3.Zero;
		_walker.IsOnGround = false;
		_walker.Enabled = true;
		Mode = SkateRiderMode.Walking;
		return true;
	}

	/// <summary>
	/// Starts a step off the board from where the rider rode: the body narrows and passes over its own board, and starts
	/// from the first clear point toward where it steps, at its own height, if it rode inside something (along a wall).
	/// </summary>
	internal void BeginStepOff()
	{
		_stepTarget = null;
		_walker.Radius = StepRadius;
		_walker.IgnoreLayers.Add( SkateTags.Board );
		var start = _rider.WorldPosition;
		var aside = DismountPosition.WithZ( start.z );
		for ( var i = 0; i <= ClearSamples; i++ )
		{
			var at = Vector3.Lerp( start, aside, i / (float)ClearSamples );
			if ( Clear( at ) )
			{
				_rider.WorldPosition = at;
				return;
			}
		}
	}

	/// <summary>
	/// Moves the stepping rider one fixed step. Across the ground it follows the step toward <paramref name="target"/>;
	/// up and down it keeps the speed it left the board with and falls under gravity until it lands, so stepping off in
	/// the air never holds the rider where it let go.
	/// </summary>
	internal void StepOff( Vector3 target, Rotation rotation, float step )
	{
		var planar = (target - _rider.WorldPosition).WithZ( 0f ) / step;
		_walker.Velocity = planar.WithZ( _walker.Velocity.z ) + Vector3.Down * (Gravity * SkateUnits.InchesPerMetre * step);
		var before = _rider.WorldPosition;
		_walker.Move();
		Unwedge( before );
		_rider.WorldRotation = rotation;
		// Between steps, and once the step is done, the body moves at the step's own speed across the ground, not at what it
		// took to catch up with it: a step held back a moment (beside a wall ridden off) left the body that catch-up speed,
		// and once the step was done it ran on at it (14 m/s off a wall ride).
		if ( _stepTarget is { } previous )
			_walker.Velocity = ((target - previous).WithZ( 0f ) / step).WithZ( _walker.Velocity.z );
		_stepTarget = target;
	}

	// Where the step off put the body the step before.
	private Vector3? _stepTarget;

	/// <summary>Adds upward speed (inches per second) to a rider stepping off while already rising or falling off the deck.</summary>
	internal void Rise( float speed ) => _walker.Velocity += Vector3.Up * speed;

	/// <summary>Ends a step off the board: the body meets its board again, and widens back as there is room.</summary>
	internal void EndStepOff() => _walker.IgnoreLayers.Remove( SkateTags.Board );

	/// <summary>
	/// Lets the walking body pass over its own board while stepping onto it, so the step is not caught on the deck beside
	/// the feet (it slid the body back and forth across a frame or two).
	/// </summary>
	internal void PassOverBoard( bool pass )
	{
		if ( pass )
			_walker.IgnoreLayers.Add( SkateTags.Board );
		else
			_walker.IgnoreLayers.Remove( SkateTags.Board );
	}

	// How far (inches) a wedged body is eased out from the surface under it each step.
	private const float WedgeStep = 1f;

	// A body off the ground that a move left exactly where it was, though gravity pulled it, is wedged against steep
	// surfaces (its box in the crease of a curved ramp): it is eased out from the surface under it, so it falls and
	// slides on instead of hanging there.
	private void Unwedge( Vector3 before )
	{
		if ( _walker.IsOnGround || (_rider.WorldPosition - before).LengthSquared > 0.0001f )
			return;
		var position = _rider.WorldPosition;
		var trace = _rider.Scene.Trace.Ray( position, position + Vector3.Down * WedgeStep * 4f ).Size( _walker.BoundingBox )
			.IgnoreGameObjectHierarchy( _rider );
		if ( _walker.IgnoreLayers.Has( SkateTags.Board ) )
			trace = trace.IgnoreGameObjectHierarchy( _board );
		var below = trace.Run();
		var away = below.Hit ? below.Normal.WithZ( 0f ) : Vector3.Zero;
		if ( away.LengthSquared < 0.01f )
			return;
		_rider.WorldPosition = position + away.Normal * WedgeStep;
	}

	// Whether the body fits here, passing over its board while stepping off and before the rider walks.
	private bool Clear( Vector3 position )
	{
		var trace = _rider.Scene.Trace.Ray( position, position ).Size( _walker.BoundingBox ).IgnoreGameObjectHierarchy( _rider );
		if ( Mode == SkateRiderMode.Riding || _walker.IgnoreLayers.Has( SkateTags.Board ) )
			trace = trace.IgnoreGameObjectHierarchy( _board );
		var hit = trace.Run();
		return !hit.StartedSolid && !hit.Hit;
	}

	// Inches from the rider down to the ground under it; infinite with nothing below in reach.
	private float GroundGap()
	{
		var position = _rider.WorldPosition;
		var ground = _rider.Scene.Trace.Ray( position + Vector3.Up, position + Vector3.Down * GroundReach )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		return ground.Hit ? position.z - ground.HitPosition.z : float.PositiveInfinity;
	}

	// Where the rider can stand beside the board on one side, or null with no room there. On the ground beside it, a
	// slope too, where the body's box rests on its uphill edge and so is raised as far as the slope needs; beside a
	// surface too steep to stand on (high on a quarterpipe) it is moved out from the surface and falls or slides from there.
	private Vector3? StepOffSpot( int side )
	{
		var position = _board.WorldPosition + _rider.WorldRotation.Left * (28f * side) + Vector3.Up * 2f;
		var floor = _rider.Scene.Trace.Ray( position + Vector3.Up * 24f, position - Vector3.Up * 80f )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		var away = Vector3.Up;
		if ( floor.Hit && !floor.StartedSolid )
		{
			if ( floor.Normal.z > StandableUp )
				position = floor.HitPosition + Vector3.Up * 0.1f;
			else
				away = floor.Normal;
		}
		for ( var lift = 0f; lift <= MostLift; lift += LiftStep )
		{
			if ( Clear( position + away * lift ) )
				return position + away * lift;
		}
		return null;
	}

	// Steepest ground (cosine of its slope) the rider stands on beside the board, and how far (inches) the body may be
	// raised off a slope or moved out from a steep surface to find room, in steps of LiftStep.
	private const float StandableUp = 0.7f;
	private const float MostLift = 18f;
	private const float LiftStep = 1f;

	// How much wider (inches) the body grows each step back to its walking width after stepping off.
	private const float WidenStep = 1f;

	// Back on foot the body widens again a little each step, rising over a slope as it does, so it never ends up inside
	// the ground, the board or a wall it stands beside; until there is room it stays narrow.
	private void Widen()
	{
		var narrow = _walker.Radius;
		_walker.Radius = MathF.Min( WalkRadius, narrow + WidenStep );
		var position = _rider.WorldPosition;
		for ( var lift = 0f; lift <= 1.5f * WidenStep; lift += 0.25f * WidenStep )
		{
			if ( !Clear( position + Vector3.Up * lift ) )
				continue;
			_rider.WorldPosition = position + Vector3.Up * lift;
			return;
		}
		_walker.Radius = narrow;
	}

	internal void Bail()
	{
		Mode = SkateRiderMode.Bailed;
		_walker.Enabled = false;
	}

	/// <summary>Another player's rider: shows the mode its owner is in, without simulating it here.</summary>
	internal void ShowMode( SkateRiderMode mode )
	{
		Mode = mode;
		_walker.Enabled = false;
	}

	internal void FollowRagdoll( Vector3 position ) => _rider.WorldPosition = position;

	internal bool Recover( RiderRagdoll ragdoll )
	{
		if ( Mode != SkateRiderMode.Bailed || ragdoll is null || !ragdoll.CanRecover ) return false;
		_walker.Radius = WalkRadius;
		var center = ragdoll.Position;
		for ( var index = 0; index < 5; index++ )
		{
			var offset = index == 0 ? Vector3.Zero : Rotation.FromYaw( (index - 1) * 90f ).Forward * 24f;
			var floor = _rider.Scene.Trace.Ray( center + offset + Vector3.Up * 24f, center + offset - Vector3.Up * 80f )
				.IgnoreGameObjectHierarchy( ragdoll.Object ).IgnoreGameObjectHierarchy( _rider )
				.IgnoreGameObjectHierarchy( _board ).Run();
			if ( !floor.Hit || floor.StartedSolid || floor.Normal.z < 0.7f ) continue;
			var position = floor.HitPosition + Vector3.Up * 0.1f;
			var space = _rider.Scene.Trace.Ray( position, position ).Size( _walker.BoundingBox )
				.IgnoreGameObjectHierarchy( ragdoll.Object ).IgnoreGameObjectHierarchy( _rider ).Run();
			if ( space.Hit || space.StartedSolid ) continue;
			_rider.WorldPosition = position;
			_walker.Velocity = Vector3.Zero;
			_walker.Enabled = true;
			Mode = SkateRiderMode.Walking;
			return true;
		}
		return false;
	}

	/// <summary>Turns the rider about its own up axis by <paramref name="degrees"/> (left positive).</summary>
	internal void Turn( float degrees )
	{
		if ( MathF.Abs( degrees ) > 0f )
			_rider.WorldRotation = Rotation.FromYaw( _rider.WorldRotation.Yaw() + degrees );
	}

	/// <summary>
	/// Moves the walking body one fixed step at <paramref name="planar"/> (inches per second) across the ground, falling
	/// under gravity, without turning it: getting on from the hand the rider carries on its way.
	/// </summary>
	internal void Glide( Vector3 planar, float delta )
	{
		if ( Mode is SkateRiderMode.Riding or SkateRiderMode.Bailed )
			return;
		_walker.Velocity = planar.WithZ( _walker.Velocity.z ) + Vector3.Down * (Gravity * SkateUnits.InchesPerMetre * delta);
		var before = _rider.WorldPosition;
		_walker.Move();
		Unwedge( before );
	}

	/// <summary>Makes the step off under way end where the rider is, rather than beside the board (it takes the board along).</summary>
	internal void KeepPlace() => DismountPosition = _rider.WorldPosition;

	/// <summary>Takes the board into the hand at once, wherever it is (the hands already hold it in a grab).</summary>
	internal bool TakeBoard()
	{
		if ( Mode is not (SkateRiderMode.Walking or SkateRiderMode.Riding) )
			return false;
		_boardParent = _board.Parent;
		_body.MotionEnabled = false;
		Reparent( _rider );
		Mode = SkateRiderMode.Carrying;
		return true;
	}

	/// <summary>
	/// Lets go of the held board at <paramref name="pose"/> (where the hand holds it), moving at <paramref name="velocity"/>
	/// and turning at <paramref name="spin"/>. A pose inside something is moved back toward the rider until it is clear.
	/// </summary>
	internal bool Release( Transform pose, Vector3 velocity, Vector3 spin )
	{
		if ( Mode != SkateRiderMode.Carrying )
			return false;
		Reparent( _boardParent.IsValid() ? _boardParent : _rider.Scene );
		var chest = _rider.WorldPosition + Vector3.Up * ChestHeight;
		var clear = _rider.Scene.Trace.Ray( chest, pose.Position ).Size( HeldBox )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		var position = clear.Hit || clear.StartedSolid ? clear.EndPosition : pose.Position;
		_board.WorldTransform = new Transform( position, pose.Rotation, _board.WorldScale );
		_body.MotionEnabled = true;
		_body.Velocity = velocity;
		_body.AngularVelocity = spin;
		Mode = SkateRiderMode.Walking;
		return true;
	}

	/// <summary>
	/// Puts the held board down under the rider at <paramref name="pose"/> (the way the hand set it down, upright and on the
	/// ground below when there is ground there) and gets on it, both moving at <paramref name="velocity"/>.
	/// </summary>
	internal bool MountHeld( Transform pose, Vector3 velocity, float rideHeight )
	{
		if ( Mode != SkateRiderMode.Carrying )
			return false;
		// It goes down under the rider, where riding it puts the rider, so the rider is not moved across onto it; the hand
		// only says when (the clips bring it down beside the feet, for a rider of their own build). Brought over to the
		// clip's own board as the mount ends, the pose is where the clip has it under the body: it goes down there, across
		// the ground (within MountShift), so the body the clip stands on it is not moved and the board shown is not either
		// (put down under the rider it still jumped 3.6-4.5 in from there in a frame).
		var under = _rider.WorldPosition;
		var across = (pose.Position - under).WithZ( 0f );
		if ( across.Length <= MountShift )
			under += across;
		var above = under.WithZ( MathF.Max( pose.Position.z, under.z ) ) + Vector3.Up * 12f;
		var floor = _rider.Scene.Trace.Ray( above, above + Vector3.Down * (12f + rideHeight + MountFloorReach) )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		var position = floor.Hit && !floor.StartedSolid ? floor.HitPosition + Vector3.Up * rideHeight : under + Vector3.Up * rideHeight;
		// In the air it goes under the feet where the pose has it, at its height (at the rider's own height it jumped 8.8 in),
		// never below the ground under it.
		if ( Airborne && MathF.Abs( pose.Position.z - position.z ) <= MountRise )
			position = position.WithZ( floor.Hit && !floor.StartedSolid
				? MathF.Max( pose.Position.z, floor.HitPosition.z + rideHeight ) : pose.Position.z );
		Reparent( _boardParent.IsValid() ? _boardParent : _rider.Scene );
		// The board goes down heading the way the rider faces, which riding it then keeps: the hand holds it on its side,
		// where its own heading says little, and taking that turned the whole rider up to 50 degrees in a frame.
		_board.WorldTransform = new Transform( position, Rotation.FromYaw( _rider.WorldRotation.Yaw() ), _board.WorldScale );
		_body.MotionEnabled = true;
		_body.Velocity = velocity;
		_body.AngularVelocity = Vector3.Zero;
		Mode = SkateRiderMode.Riding;
		_walker.Enabled = false;
		FollowBoard();
		return true;
	}

	// Height (inches) of the chest a held board is let go from, the box (inches) it must fit through, and how far (inches)
	// below the hand's pose the ground is looked for when the board is put down to get on, and how far (inches) across the
	// ground from under the rider it may go down where the pose has it, and how far (inches) up or down from there it may
	// go to the pose's height in the air.
	private const float ChestHeight = 48f;
	private static readonly Vector3 HeldBox = new( 6f, 6f, 2f );
	private const float MountFloorReach = 24f;
	private const float MountShift = 8f;
	private const float MountRise = 24f;

	internal bool PickUp( bool immediate = true )
	{
		if ( !CanPickUp )
			return false;
		_boardParent = _board.Parent;
		_body.MotionEnabled = false;
		Reparent( _rider );
		if ( immediate ) _board.WorldTransform = CarryPose;
		Mode = SkateRiderMode.Carrying;
		return true;
	}

	// Moves the board under another parent where it stands. Its shown transform is otherwise blended for a frame or two
	// between its place under the old parent and under the new one, as if both were the same space: picking the board up,
	// it was shown up to 434 inches off for a frame.
	private void Reparent( GameObject parent )
	{
		_board.SetParent( parent, true );
		_board.Transform.ClearInterpolation();
	}

	internal bool BeginRetrieval()
	{
		if ( !CanPickUp || _retrieving )
			return false;
		_boardParent = _board.Parent;
		_body.MotionEnabled = false;
		Reparent( _rider );
		_retrieving = true;
		return true;
	}

	internal void CompleteRetrieval()
	{
		if ( !_retrieving )
			return;
		_retrieving = false;
		Mode = SkateRiderMode.Carrying;
	}

	internal bool Drop()
	{
		if ( Mode != SkateRiderMode.Carrying )
			return false;
		var position = _rider.WorldPosition + _rider.WorldRotation.Forward * 30f + Vector3.Up * 8f;
		var clearance = _rider.Scene.Trace.Ray( _rider.WorldPosition + Vector3.Up * 12f, position )
			.Size( new Vector3( 34f, 12f, 5f ) ).IgnoreGameObjectHierarchy( _rider ).Run();
		if ( clearance.StartedSolid || clearance.Hit )
			return false;
		Reparent( _boardParent.IsValid() ? _boardParent : _rider.Scene );
		_board.WorldPosition = position;
		_board.WorldRotation = _rider.WorldRotation;
		_body.MotionEnabled = true;
		_body.Velocity = _walker.Velocity;
		_body.AngularVelocity = Vector3.Zero;
		Mode = SkateRiderMode.Walking;
		return true;
	}

	internal bool Mount()
	{
		if ( !CanMount )
			return false;
		Mode = SkateRiderMode.Riding;
		_walker.Enabled = false;
		FollowBoard();
		return true;
	}

	internal void MoveForInteraction( Vector3 position, Rotation rotation, float step )
	{
		var before = _rider.WorldPosition;
		_walker.MoveTo( position, true );
		_walker.Velocity = (_rider.WorldPosition - before) / step;
		_rider.WorldRotation = rotation;
	}

	private bool Reachable( float range, bool sight = true )
	{
		if ( !_board.IsValid() || (_board.WorldPosition - _rider.WorldPosition).Length > range )
			return false;
		if ( !sight )
			return true;
		var trace = _rider.Scene.Trace.Ray( _rider.WorldPosition + Vector3.Up * 32f, _board.WorldPosition )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		return !trace.Hit && !trace.StartedSolid;
	}
}
