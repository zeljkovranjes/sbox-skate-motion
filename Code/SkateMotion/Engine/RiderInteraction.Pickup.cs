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
	// has arrived. Called from afar it first shakes where it lies for _shakeLength seconds (the flight's time runs from
	// minus that), then takes off from a little above where it lay.
	private float _flightTime;
	private float _flightLength;
	private bool _flightDone;
	private float _shakeLength;
	private bool _shakenLoose;

	// The shake: how far (inches) and how far round (degrees) it trembles at its strongest, a quarter of that as it begins,
	// and how high (inches) it rises off the ground by the end.
	private const float ShakeReach = 1.5f;
	private const float ShakeTurn = 14f;
	private const float ShakeLift = 5f;

	// Without a reach animation to say when the hand is out, the board is called this many seconds into the pickup; with
	// one that never calls it (cut short), after ReachCallAtLatest.
	private const float CallAtLatest = 0.34f;
	private const float ReachCallAtLatest = 1.5f;

	// A board farther than this (inches) is called as the reach begins, CallAtLatest into the pickup, not when the reach
	// animation's hand is out: it takes its flight's time to come, and the hand is out by the time it arrives.
	private const float FarCall = 200f;

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
			var far = (_board.WorldPosition - _motor.CarryPose.Position).Length > FarCall;
			if ( !_pickupRequested && _elapsed < (_reaching && !far ? ReachCallAtLatest : CallAtLatest) )
				return;
			_startBoard = _board.WorldTransform;
			_shakeLength = far ? MathF.Max( Shake, 0f ) : 0f;
			_shakenLoose = _shakeLength <= 0f;
			_flightTime = -_shakeLength;
			_flightLength = BoardReach.RecallSeconds( SkateUnits.Metres( _motor.CarryPose.Position - _startBoard.Position ).Length() );
			_captured = true;
			CaptureBoard = true;
			Called?.Invoke( _shakeLength, _flightLength );
			return;
		}
		_flightTime += delta;
		// Shaken loose, it flies from where the shake lifted it.
		if ( !_shakenLoose && _flightTime >= 0f )
		{
			_shakenLoose = true;
			_startBoard = _startBoard.WithPosition( _startBoard.Position + Vector3.Up * ShakeLift );
		}
		_flightDone = _flightTime >= _flightLength;
		_board.WorldTransform = Flight( _motor.CarryPose );
	}

	private Transform Flight( Transform hand )
	{
		if ( _flightTime < 0f && _shakeLength > 0f )
			return Shaken( _startBoard, 1f + _flightTime / _shakeLength, Time.Now );
		var progress = _flightLength > 0f ? _flightTime / _flightLength : 1f;
		var position = SkateUnits.Inches( BoardReach.Along( SkateUnits.Metres( _startBoard.Position ), SkateUnits.Metres( hand.Position ), progress ) );
		return new Transform( position, Rotation.Slerp( _startBoard.Rotation, hand.Rotation, Ease( progress ) ), _startBoard.Scale );
	}

	// The board trembling where it lies before it flies, as if tugged at: the tremor grows with the square of how far
	// through the shake it is (a quarter of its strength as it begins) and lifts it off the ground; its place and turn
	// jitter at three unrelated rates (about 23, 38 and 61 a second), smooth in time so every frame shows it.
	private static Transform Shaken( Transform rest, float share, float now )
	{
		var grow = Math.Clamp( share, 0f, 1f );
		grow *= grow;
		float Wobble( float phase ) => MathF.Sin( now * 145f + phase ) * 0.5f + MathF.Sin( now * 237f + phase * 1.7f ) * 0.3f
			+ MathF.Sin( now * 385f + phase * 2.3f ) * 0.2f;
		var strength = 0.25f + 0.75f * grow;
		var shift = new Vector3( Wobble( 0f ), Wobble( 2.1f ), Wobble( 4.2f ) ) * (ShakeReach * strength) + Vector3.Up * (ShakeLift * grow);
		var turn = new Angles( Wobble( 1.3f ), Wobble( 3.4f ) * 0.5f, Wobble( 5.5f ) ) * (ShakeTurn * strength);
		return new Transform( rest.Position + shift, rest.Rotation * turn.ToRotation(), rest.Scale );
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
