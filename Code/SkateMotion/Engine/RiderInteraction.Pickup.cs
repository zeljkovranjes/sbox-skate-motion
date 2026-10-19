using Sandbox;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;

namespace SkateMotion.Engine;

internal sealed partial class RiderInteraction
{
	private readonly BoardPickupPlayback _pickup;
	private bool _pickupRequested;
	internal bool UsesSourcePickup => Action == RiderAction.PickUp && _pickup is not null;

	internal void ObserveRetrievalRequest( bool requested )
	{
		if ( UsesSourcePickup )
			_pickupRequested |= requested;
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

	/// <summary>
	/// Shows the flying board for this frame. Retrieval advances on fixed updates, but the hand it flies into moves
	/// every rendered frame, so the board is blended toward where the hand holds it now rather than where it held it
	/// at the last fixed update; arriving at several inches per update, the stale target left it short of the hand.
	/// </summary>
	internal void ShowPickup()
	{
		if ( !UsesSourcePickup || !_captured || !_motor.HasCarryPose )
			return;
		Place( _pickup.Sample( ToPhysical( _motor.CarryPose ) ) );
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
