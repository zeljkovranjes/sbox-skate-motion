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
	internal bool CanPickUp => Mode == SkateRiderMode.Walking && Reachable() && _body.Velocity.Length <= 160f;
	internal bool CanMount => Mode == SkateRiderMode.Walking && Reachable()
		&& Vector3.Dot( _board.WorldRotation.Up, Vector3.Up ) >= 0.6f;
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
		_walker.Radius = 12f;
		_walker.Height = 68f;
		_walker.StepHeight = 14f;
		_walker.Acceleration = 20f;
		_walker.Enabled = false;
	}

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

	// Fastest the body turns toward the walking direction (degrees per second) against speed (m/s).
	private static readonly Core.Vehicle.LinearCurve TurnSpeed = new(
		new[] { 0f, 4.65f, 6.2f, 10.7f },
		new[] { 500f, 462f, 288f, 205f } );

	// Jump take-off speed (m/s) and the share of ground speed kept into it.
	private const float JumpSpeed = 4.2f;
	private const float JumpCarry = 0.8f;

	internal void Walk( Vector3 direction, bool jump, float delta, bool sprint = false )
	{
		if ( Mode is SkateRiderMode.Riding or SkateRiderMode.Bailed )
		{
			_sprint.Reset();
			return;
		}
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
		if ( _walker.IsOnGround )
		{
			// Walking speed moves toward the wished velocity at a set rate: starting, turning, and stopping when the
			// stick is let go or a runout ends.
			var planar = _walker.Velocity.WithZ( 0f );
			// From a standstill the rider steps straight off at the slowest walking pace.
			var slowest = StickSpeed.Evaluate( 0f ) * SkateUnits.InchesPerMetre;
			if ( wish.LengthSquared > 1f && planar.Length < slowest )
				planar = wish.Normal * slowest;
			var step = wish - planar;
			var most = Acceleration * SkateUnits.InchesPerMetre * delta;
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
		_walker.Velocity += Vector3.Down * (9.81f * SkateUnits.InchesPerMetre * delta);
		_walker.Move();
		// The body turns toward the walking direction no faster than a rate that falls with speed, so it never
		// snaps round.
		if ( wish.LengthSquared > 1f )
		{
			var rate = TurnSpeed.Evaluate( _walker.Velocity.WithZ( 0f ).Length / SkateUnits.InchesPerMetre );
			var current = _rider.WorldRotation.Yaw();
			var wanted = Rotation.LookAt( wish ).Yaw();
			var change = Math.Clamp( MathX.DeltaDegrees( current, wanted ), -rate * delta, rate * delta );
			_rider.WorldRotation = Rotation.FromYaw( current + change );
		}
	}

	// Sideways step (m/s) taken when stepping off a board moving faster than RunoutStepSpeed (inches per second).
	private const float StepAway = 0.75f;
	private const float RunoutStepSpeed = 40f;

	internal bool Dismount( bool immediate = true )
	{
		if ( Mode != SkateRiderMode.Riding )
			return false;
		for ( var side = 1; side >= -1; side -= 2 )
		{
			var position = _board.WorldPosition + _rider.WorldRotation.Left * (28f * side) + Vector3.Up * 2f;
			var floor = _rider.Scene.Trace.Ray( position + Vector3.Up * 24f, position - Vector3.Up * 80f )
				.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
			if ( floor.Hit && !floor.StartedSolid && floor.Normal.z > 0.7f ) position = floor.HitPosition + Vector3.Up * 0.1f;
			var clearance = _rider.Scene.Trace.Ray( position, position )
				.Size( _walker.BoundingBox ).IgnoreGameObjectHierarchy( _rider )
				.IgnoreGameObjectHierarchy( _board ).Run();
			if ( clearance.StartedSolid || clearance.Hit )
				continue;
			DismountPosition = position;
			if ( immediate ) _rider.WorldPosition = position;
			// The rider keeps the board's momentum and steps a little away from it.
			_walker.Velocity = _body.Velocity + (_body.Velocity.Length > RunoutStepSpeed
				? _rider.WorldRotation.Left * (side * StepAway * SkateUnits.InchesPerMetre) : Vector3.Zero);
			_walker.Enabled = true;
			Mode = SkateRiderMode.Walking;
			return true;
		}
		return false;
	}

	internal void Bail()
	{
		Mode = SkateRiderMode.Bailed;
		_walker.Enabled = false;
	}

	internal void FollowRagdoll( Vector3 position ) => _rider.WorldPosition = position;

	internal bool Recover( RiderRagdoll ragdoll )
	{
		if ( Mode != SkateRiderMode.Bailed || ragdoll is null || !ragdoll.CanRecover ) return false;
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

	internal bool PickUp( bool immediate = true )
	{
		if ( !CanPickUp )
			return false;
		_boardParent = _board.Parent;
		_body.MotionEnabled = false;
		_board.SetParent( _rider, true );
		if ( immediate ) _board.WorldTransform = CarryPose;
		Mode = SkateRiderMode.Carrying;
		return true;
	}

	internal bool BeginRetrieval()
	{
		if ( !CanPickUp || _retrieving )
			return false;
		_boardParent = _board.Parent;
		_body.MotionEnabled = false;
		_board.SetParent( _rider, true );
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
		_board.SetParent( _boardParent.IsValid() ? _boardParent : _rider.Scene, true );
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

	private bool Reachable()
	{
		if ( !_board.IsValid() || (_board.WorldPosition - _rider.WorldPosition).Length > 64f )
			return false;
		var trace = _rider.Scene.Trace.Ray( _rider.WorldPosition + Vector3.Up * 32f, _board.WorldPosition )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		return !trace.Hit && !trace.StartedSolid;
	}
}
