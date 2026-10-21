using Sandbox;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;

namespace SkateMotion.Engine;

internal sealed partial class RiderInteraction
{
	private readonly BoardPickupPlayback _pickup;
	private bool _pickupRequested;

	// Whether a reach animation is playing for this pickup: it calls the board itself once its hand is out.
	private bool _reaching;
	internal bool UsesSourcePickup => Action == RiderAction.PickUp && _pickup is not null;

	// The board's own flight to the hand, without a retrieval profile: its start, time so far and length, and whether it
	// has arrived.
	private float _flightTime;
	private float _flightLength;
	private bool _flightDone;

	// Without a reach animation to say when the hand is out, the board is called this many seconds into the pickup; with
	// one that never calls it (cut short), after ReachCallAtLatest.
	private const float CallAtLatest = 0.34f;
	private const float ReachCallAtLatest = 1.5f;

	/// <param name="requested">Whether the reach animation calls the board now.</param>
	/// <param name="reaching">Whether a reach animation is playing.</param>
	internal void ObserveRetrievalRequest( bool requested, bool reaching = false )
	{
		if ( Action != RiderAction.PickUp )
			return;
		_pickupRequested |= requested;
		_reaching |= reaching;
	}

	private void StepPickup( float delta )
	{
		if ( !_motor.HasCarryPose )
			return;
		var target = ToPhysical( _motor.CarryPose );
		if ( !_captured )
		{
			if ( !_pickupRequested )
				return;
			_startBoard = _board.WorldTransform;
			_pickup.Begin( ToPhysical( _startBoard ), target, SkateUnits.ToCore( _board.WorldRotation.Forward ) );
			_captured = true;
			CaptureBoard = true;
			return;
		}
		AdvancePickup( delta );
	}

	internal void AdvancePickup( float delta )
	{
		var target = ToPhysical( _motor.CarryPose );
		_pickup.Advance( delta, target );
		Place( _pickup.Sample( target ) );
	}

	// The board is called once the reaching hand is out (the reach animation asks for it once its hold has taken over, or
	// after CallAtLatest without one), then flies to the hand over a time that grows with the distance. The rider holds it
	// only once it is there, so the reach comes back in with the board in the hand.
	private void StepFlight( float delta )
	{
		if ( !_captured )
		{
			if ( !_pickupRequested && _elapsed < (_reaching ? ReachCallAtLatest : CallAtLatest) )
				return;
			_startBoard = _board.WorldTransform;
			_flightTime = 0f;
			_flightLength = BoardReach.FlightSeconds( SkateUnits.Metres( _motor.CarryPose.Position - _startBoard.Position ).Length() );
			_captured = true;
			CaptureBoard = true;
			return;
		}
		_flightTime += delta;
		_flightDone = _flightTime >= _flightLength;
		_board.WorldTransform = Flight( _motor.CarryPose );
	}

	private Transform Flight( Transform hand )
	{
		var progress = _flightLength > 0f ? _flightTime / _flightLength : 1f;
		var position = SkateUnits.Inches( BoardReach.Along( SkateUnits.Metres( _startBoard.Position ), SkateUnits.Metres( hand.Position ), progress ) );
		return new Transform( position, Rotation.Slerp( _startBoard.Rotation, hand.Rotation, Ease( progress ) ), _startBoard.Scale );
	}

	/// <summary>
	/// Shows the flying board for this frame. Retrieval advances on fixed updates, but the hand it flies into moves
	/// every rendered frame, so the board is blended toward where the hand holds it now rather than where it held it
	/// at the last fixed update; arriving at several inches per update, the stale target left it short of the hand.
	/// </summary>
	internal void ShowPickup()
	{
		if ( Action != RiderAction.PickUp || !_captured || !_motor.HasCarryPose )
			return;
		if ( UsesSourcePickup )
			Place( _pickup.Sample( ToPhysical( _motor.CarryPose ) ) );
		else
			_board.WorldTransform = Flight( _motor.CarryPose );
	}

	private void Place( LimbRigidTransform frame )
	{
		var rotation = PhysicalPoseOutput.Rotation( frame.Rotation );
		_board.WorldTransform = new Transform( SkateUnits.Inches( frame.Translation ),
			new Rotation( rotation.X, rotation.Y, rotation.Z, rotation.W ), _startBoard.Scale );
	}

	private static LimbRigidTransform ToPhysical( Transform transform )
	{
		var rotation = SkateUnits.ToCore( transform.Rotation );
		return new( new LimbFrame(
			System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitX, rotation ),
			System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitY, rotation ),
			System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitZ, rotation ) ),
			SkateUnits.Metres( transform.Position ) );
	}
}
