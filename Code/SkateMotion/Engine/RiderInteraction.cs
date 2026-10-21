using Sandbox;

namespace SkateMotion.Engine;

// Estimated presentation timing. Free skating continues in the physical solver;
// only an explicitly held board is moved by the interaction.
internal sealed partial class RiderInteraction
{
	private readonly GameObject _rider;
	private readonly GameObject _board;
	private readonly Rigidbody _body;
	private readonly RiderMotor _motor;
	private Transform _startRider;
	private Transform _startBoard;
	private float _elapsed;
	private float _duration;
	private bool _captured;

	// Stepping off, how long the rider has stood since it last touched down and how long it has been off the board.
	private float _standing;
	private float _offBoard;

	// A step off ends once the rider has stood this long (seconds), so a step off in the air lasts until it lands; and in
	// any case after the longest fall allowed for.
	private const float StandBeforeWalking = 0.1f;
	private const float LongestFall = 5f;

	internal RiderAction Action { get; private set; }
	internal bool Active => Action != RiderAction.None;
	internal float Progress => Math.Clamp( _elapsed / _duration, 0f, 1f );
	internal bool CaptureBoard { get; private set; }
	internal bool Finished => Active && Action switch
	{
		RiderAction.PickUp => UsesSourcePickup ? _captured && _pickup.Complete : _captured && _flightDone,
		RiderAction.Drop or RiderAction.Throw => _released,
		RiderAction.Mount when _fromHand => _placed,
		RiderAction.DropIn => _placed || _backedOut,
		RiderAction.Dismount => _elapsed >= _duration && (_standing >= StandBeforeWalking || _offBoard >= LongestFall),
		_ => _elapsed >= _duration
	};

	/// <summary>
	/// True while the interaction keeps the board in the rider's hand (a throw or drop before the hand lets go, a mount from
	/// the hand before the board is down, a step off in a grab that takes the board along): it follows the hand each frame.
	/// </summary>
	internal bool CarriesBoard => Active && _motor.Mode == SkateRiderMode.Carrying && Action switch
	{
		RiderAction.Drop or RiderAction.Throw => !_released,
		RiderAction.Mount => _fromHand && !_placed,
		RiderAction.DropIn => !_placed,
		RiderAction.Dismount => true,
		_ => false
	};

	/// <summary>True while the rider may keep walking under its own control: picking the board up, dropping or throwing it.</summary>
	internal bool AllowsWalking => Action is RiderAction.PickUp or RiderAction.Drop or RiderAction.Throw;

	/// <summary>
	/// While a landing on foot plays, how its clip's root travels (inches per second, world; null otherwise): landing from
	/// an air dismount, the rider goes on so, over the feet the landing plants.
	/// </summary>
	internal Vector3? LandingTravel { get; set; }

	// Stepping off, the feet go to the side at the body's own height: the body itself falls or steps down to the ground.
	internal Vector3 StepDestination => Action == RiderAction.Mount ? _motor.RidingPosition
		: _motor.DismountPosition.WithZ( _rider.WorldPosition.z );
	internal Rotation StartRotation => _startRider.Rotation;
	internal Vector3 ReachPosition => _board.WorldPosition + Vector3.Up * 8f + _rider.WorldRotation.Forward;
	internal bool AtDestination => Action switch
	{
		RiderAction.Mount => _fromHand ? _placed : (_rider.WorldPosition - _motor.RidingPosition).Length < 1f,
		RiderAction.PickUp => UsesSourcePickup ? _pickup.Complete : _flightDone,
		RiderAction.Drop or RiderAction.Throw => _released,
		RiderAction.DropIn => _placed,
		_ => true
	};

	internal RiderInteraction( GameObject rider, GameObject board, Rigidbody body, RiderMotor motor,
		BoardRetrievalProfile retrieval = null )
	{
		_rider = rider;
		_board = board;
		_body = body;
		_motor = motor;
		_pickup = retrieval?.CreatePlayback();
	}

	internal bool Begin( RiderAction action )
	{
		if ( Active ) return false;
		var fromHand = action == RiderAction.Mount && _motor.Mode == SkateRiderMode.Carrying;
		if ( action == RiderAction.Mount && !fromHand && !_motor.CanMount ) return false;
		if ( action == RiderAction.PickUp && !_motor.CanPickUp ) return false;
		if ( action is RiderAction.Drop or RiderAction.Throw && _motor.Mode != SkateRiderMode.Carrying ) return false;
		if ( action == RiderAction.Dismount && _motor.Mode != SkateRiderMode.Riding ) return false;
		if ( action == RiderAction.DropIn && (_motor.Mode != SkateRiderMode.Carrying || !_motor.Grounded
			|| !(_motor.EdgeDistance <= Core.Vehicle.DropInEdge.Reach)) ) return false;
		_startRider = _rider.WorldTransform;
		_startBoard = _board.WorldTransform;
		_runout = action == RiderAction.Dismount ? _body.Velocity.WithZ( 0f ) : Vector3.Zero;
		if ( action == RiderAction.Dismount )
		{
			if ( !_motor.Dismount( false ) ) return false;
			_motor.BeginStepOff();
			_startRider = _rider.WorldTransform;
		}
		Action = action;
		_fromHand = fromHand;
		if ( action == RiderAction.Mount && !fromHand )
			_motor.PassOverBoard( true );
		_duration = action switch
		{
			RiderAction.PickUp => 0.85f,
			RiderAction.Drop or RiderAction.Throw => ReleaseAtLatest,
			RiderAction.Mount when fromHand && _motor.Airborne => AirMountTime,
			RiderAction.Mount when fromHand => HandMountLongest,
			RiderAction.DropIn => DropInLongest,
			_ => 0.55f
		};
		_elapsed = 0f;
		_standing = 0f;
		_offBoard = 0f;
		_captured = false;
		_pickupRequested = false;
		_reaching = false;
		_released = false;
		_placed = false;
		_flightDone = false;
		_airMount = fromHand && _motor.Airborne;
		if ( action == RiderAction.DropIn )
			BeginDropIn();
		_glide = fromHand ? MountGlide() : Vector3.Zero;
		return true;
	}

	internal void Step( float delta )
	{
		if ( !Active ) return;
		CaptureBoard = false;
		if ( UsesSourcePickup )
		{
			StepPickup( delta );
			return;
		}
		var stepping = _elapsed < _duration;
		_elapsed = Math.Min( _duration, _elapsed + delta );
		var progress = Progress;
		switch ( Action )
		{
			case RiderAction.Dismount:
			{
				// Stepping off a moving board the rider runs it out: the whole step carries on with the board's speed,
				// slowing at a steady rate, so the rider never stops dead while the board rolls away. Only the step across
				// the ground is set: the body falls or steps down to wherever the ground is. Once the step is done the rider
				// moves on by itself: still in the air, it falls on until it stands.
				var across = Vector3.Lerp( _startRider.Position, _motor.DismountPosition, Ease( progress ) ) + RunoutTravel( _elapsed );
				if ( stepping )
					_motor.StepOff( across, _startRider.Rotation, delta );
				else
					_motor.Walk( Vector3.Zero, false, delta, travel: LandingTravel );
				_offBoard += delta;
				_standing = _motor.Grounded ? _standing + delta : 0f;
				return;
			}
			case RiderAction.Mount when _fromHand:
				// Getting on from the hand the rider carries on across the ground (a run or sprint onto the board) while the
				// board goes down under its feet.
				_motor.Glide( _glide, delta );
				return;
			case RiderAction.Mount:
			{
				var position = Vector3.Lerp( _startRider.Position, StepDestination, Ease( progress ) );
				var rotation = Rotation.FromYaw( _board.WorldRotation.Yaw() );
				_motor.MoveForInteraction( position, Rotation.Slerp( _startRider.Rotation, rotation, Ease( progress ) ), delta );
				return;
			}
			case RiderAction.PickUp:
				StepFlight( delta );
				return;
			case RiderAction.DropIn:
				StepDropIn( delta );
				return;
		}
	}

	private Vector3 _runout;

	// How quickly (inches per second squared) a rider running out a dismount slows down.
	private const float RunoutDeceleration = 8f * 39.37f;

	private Vector3 RunoutTravel( float time )
	{
		var speed = _runout.Length;
		if ( speed < 1f )
			return Vector3.Zero;
		var moving = Math.Min( time, speed / RunoutDeceleration );
		return _runout / speed * (speed * moving - 0.5f * RunoutDeceleration * moving * moving);
	}

	internal void End()
	{
		if ( Action == RiderAction.Dismount )
			_motor.EndStepOff();
		else if ( Action == RiderAction.Mount && !_fromHand )
			_motor.PassOverBoard( false );
		Action = RiderAction.None;
		CaptureBoard = false;
	}

	internal static float Ease( float value )
	{
		value = Math.Clamp( value, 0f, 1f );
		return value * value * (3f - 2f * value);
	}
}
