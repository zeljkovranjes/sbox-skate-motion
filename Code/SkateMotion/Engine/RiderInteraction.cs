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
	internal RiderAction Action { get; private set; }
	internal bool Active => Action != RiderAction.None;
	internal float Progress => Math.Clamp( _elapsed / _duration, 0f, 1f );
	internal bool CaptureBoard { get; private set; }
	internal bool Finished => Active && (UsesSourcePickup ? _captured && _pickup.Complete : _elapsed >= _duration);
	internal Vector3 StepDestination => Action == RiderAction.Mount ? _motor.RidingPosition : _motor.DismountPosition;
	internal Rotation StartRotation => _startRider.Rotation;
	internal Vector3 ReachPosition => _board.WorldPosition + Vector3.Up * 8f + _rider.WorldRotation.Forward;
	internal bool AtDestination => Action switch
	{
		RiderAction.Mount => (_rider.WorldPosition - _motor.RidingPosition).Length < 1f,
		RiderAction.PickUp => UsesSourcePickup ? _pickup.Complete : (_board.WorldPosition - _motor.CarryPose.Position).Length < 0.1f,
		RiderAction.Drop => (_board.WorldPosition - _motor.DropPose.Position).Length < 0.1f,
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
		if ( action == RiderAction.Mount && !_motor.CanMount ) return false;
		if ( action == RiderAction.PickUp && !_motor.CanPickUp ) return false;
		if ( action == RiderAction.Drop && _motor.Mode != SkateRiderMode.Carrying ) return false;
		if ( action == RiderAction.Dismount && _motor.Mode != SkateRiderMode.Riding ) return false;
		_startRider = _rider.WorldTransform;
		_startBoard = _board.WorldTransform;
		_runout = action == RiderAction.Dismount ? _body.Velocity.WithZ( 0f ) : Vector3.Zero;
		if ( action == RiderAction.Dismount && !_motor.Dismount( false ) ) return false;
		Action = action;
		_duration = action switch
		{
			RiderAction.PickUp => 0.85f,
			RiderAction.Drop => 0.55f,
			_ => 0.55f
		};
		_elapsed = 0f;
		_captured = false;
		_pickupRequested = false;
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
		_elapsed = Math.Min( _duration, _elapsed + delta );
		var progress = Progress;
		if ( Action is RiderAction.Mount or RiderAction.Dismount )
		{
			// Stepping off a moving board the rider runs it out: the whole step carries on with the board's speed,
			// slowing at a steady rate, so the rider never stops dead while the board rolls away.
			var position = Vector3.Lerp( _startRider.Position, StepDestination, Ease( progress ) ) + RunoutTravel( _elapsed );
			var rotation = Action == RiderAction.Mount ? Rotation.FromYaw( _board.WorldRotation.Yaw() ) : _startRider.Rotation;
			_motor.MoveForInteraction( position, Rotation.Slerp( _startRider.Rotation, rotation, Ease( progress ) ), delta );
			return;
		}
		if ( Action == RiderAction.PickUp && progress < 0.4f ) return;
		if ( Action == RiderAction.PickUp && !_captured )
		{
			CaptureBoard = true;
			_captured = true;
			_startBoard = _board.WorldTransform;
			return;
		}
		var target = Action == RiderAction.PickUp ? _motor.CarryPose : _motor.DropPose;
		var blend = Ease( Action == RiderAction.PickUp ? (progress - 0.4f) / 0.6f : progress );
		var desired = new Transform( Vector3.Lerp( _startBoard.Position, target.Position, blend ),
			Rotation.Slerp( _startBoard.Rotation, target.Rotation, blend ), _startBoard.Scale );
		var query = _rider.Scene.Trace.Sweep( _body, _board.WorldTransform, desired )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board ).Run();
		if ( !query.Hit && !query.StartedSolid ) _board.WorldTransform = desired;
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
		Action = RiderAction.None;
		CaptureBoard = false;
	}

	internal static float Ease( float value )
	{
		value = Math.Clamp( value, 0f, 1f );
		return value * value * (3f - 2f * value);
	}
}
