namespace SkateMotion.Core.Physics;

using SkateMotion.Core.Animation;
using SkateMotion.Core.Vehicle;

/// <summary>Binds ownership decisions and state entry to retained motion, target and hand drives.</summary>
internal sealed class PhysicalBoardSession
{
	private readonly BoardRetrievalInitializer _initializer;
	private readonly BoardRetrievalMotion _retrieval;
	private readonly BoardPlacementMotion _placement;
	private readonly PhysicalBoardActuators _actuators;
	private readonly PhysicalBoardTickActions _tick;
	private readonly float _placementRange;
	private PhysicalBoardInput _input;
	private PhysicalBoardPoses _poses;

	internal int State { get; private set; }
	internal int ThrowUpdatesRemaining { get; private set; }
	internal PhysicalBoardMotionState Motion { get; } = new();
	internal BoardHeldConstraints Hands { get; }
	internal BoardTargetDrive TargetDrive { get; }

	internal PhysicalBoardSession( BoardRetrievalInitializer initializer, BoardRetrievalMotion retrieval,
		BoardPlacementMotion placement, float placementRange, BoardTargetDrive targetDrive,
		BoardHeldConstraints hands, PhysicalBoardActuators actuators )
	{
		ArgumentNullException.ThrowIfNull( initializer );
		ArgumentNullException.ThrowIfNull( retrieval );
		ArgumentNullException.ThrowIfNull( placement );
		ArgumentNullException.ThrowIfNull( targetDrive );
		ArgumentNullException.ThrowIfNull( hands );
		ArgumentNullException.ThrowIfNull( actuators );
		_initializer = initializer;
		_retrieval = retrieval;
		_placement = placement;
		_placementRange = placementRange;
		TargetDrive = targetDrive;
		Hands = hands;
		_actuators = actuators;
		_tick = new( IsBeyondPlacementRange, EnterState, actuators.ClearVelocity, UpdateHeldFeedback,
			UpdateContactDirections, UpdateHeldFrame, UpdateFreeBoard, UpdatePlacement, UpdateRetrieval );
	}

	internal void Update( bool enabled, PhysicalBoardInput input, PhysicalBoardPoses poses )
	{
		if ( !enabled )
			return;
		_input = input with { State = State, RetrievalProgress = Motion.Progress };
		_poses = poses;
		State = PhysicalBoardTick.Update( true, _input, _tick );
	}

	internal void ChangeState( int state, PhysicalBoardInput input, PhysicalBoardPoses poses )
	{
		_input = input;
		_poses = poses;
		EnterState( State, state );
	}

	private void EnterState( int previous, int next )
	{
		ThrowUpdatesRemaining = 0;
		if ( previous == next )
			return;
		switch ( next )
		{
			case 0:
			case 2:
				Release( previous );
				break;
			case 1:
				TargetDrive.FollowPose( 1f );
				_actuators.PrepareHeldBodies();
				_actuators.SetJointsEnabled( true );
				State = 1;
				var hand = (int)(( _input.ActionFlags >> 2 ^ 1UL ) & 1UL);
				Hands.Activate( hand, _poses.AnimatedBoard, HandFrame( hand ) );
				break;
			case 3:
				_actuators.SetJointsEnabled( false );
				TargetDrive.ReleaseLinear();
				_actuators.ClearContactDirectionFlag();
				Hands.Release();
				BoardPlacementMotion.Begin( Motion, _poses.ProcessedBoard );
				TargetDrive.FollowOrientation( 1f );
				break;
			case 4:
				BeginRetrieval( previous );
				break;
			case 5:
				Hands.Release();
				TargetDrive.FollowSpeed();
				_actuators.PrepareHeldBodies();
				_actuators.SetJointsEnabled( true );
				break;
		}
		State = next;
	}

	private void Release( int previous )
	{
		TargetDrive.Release();
		_actuators.PrepareReleasedBodies();
		_actuators.SetJointsEnabled( true );
		_actuators.ClearContactDirectionFlag();
		Hands.Release();
		if ( previous == 4 )
			_actuators.ClearVelocity();
		if ( ( _input.ActionFlags & 0x1000UL ) == 0 )
			return;
		ThrowUpdatesRemaining = 8;
		_actuators.BeginThrow();
	}

	private void BeginRetrieval( int previous )
	{
		_actuators.SetJointsEnabled( false );
		_actuators.ClearContactDirectionFlag();
		TargetDrive.FollowOrientation( 1f );
		var previousPosition = Motion.Source.Translation;
		BoardPlacementMotion.Begin( Motion, _poses.ProcessedBoard );
		Motion.Target = _poses.RetrievalAttachment;
		var entry = _initializer.Begin( previous, Motion.Source, previousPosition, Motion.Target,
			_poses.RetrievalFallback, ( _input.RigFlags & ( 1UL << 51 ) ) != 0 );
		Motion.Source = entry.Source;
		if ( entry.Relocate )
			_actuators.Relocate( entry.Source.Translation );
		Motion.Duration = entry.Duration;
	}

	private bool IsBeyondPlacementRange() => BoardPlacementMotion.IsBeyondRange(
		_poses.ProcessedBoard.Translation, _poses.ReferencePosition, _placementRange );

	private LimbRigidTransform HandFrame( int hand ) => hand == 0 ? _poses.FirstHand : _poses.SecondHand;

	private void UpdateHeldFrame() => Hands.Update( _poses.AnimatedBoard, HandFrame( Hands.SelectedHand ) );

	private void UpdateHeldFeedback()
	{
		var hand = Hands.SelectedHand == 0 ? _poses.FirstFeedbackHand : _poses.SecondFeedbackHand;
		_actuators.WriteContactFeedback( BoardHeldFeedback.Create( _poses.ProcessedBoard.Translation,
			hand, _poses.ReferencePosition, _actuators.ReadBodyFlags() ) );
	}

	private void UpdateContactDirections( float threshold ) => _actuators.WriteContactFeedback(
		new( _poses.ContactDirection, _poses.ContactDirection, threshold, false,
			_actuators.ReadBodyFlags() | ( 1UL << 62 ) ) );

	private void UpdateFreeBoard()
	{
		if ( ThrowUpdatesRemaining <= 0 )
			return;
		--ThrowUpdatesRemaining;
		_actuators.UpdateThrow( ThrowUpdatesRemaining );
	}

	private void UpdatePlacement()
	{
		_placement.Update( Motion, _poses.PlacementPosition, _poses.PlacementDirection );
		_actuators.WriteTargetFrame( Motion.Output );
		_actuators.Relocate( Motion.Output.Translation );
		_actuators.ClearVelocity();
	}

	private void UpdateRetrieval()
	{
		Motion.Target = _poses.RetrievalAttachment;
		var result = _retrieval.Update( Motion.Source, Motion.Target, Motion.Duration, Motion.Elapsed );
		Motion.Elapsed = result.Elapsed;
		Motion.Progress = result.Progress;
		Motion.Weight = result.Weight;
		Motion.Output = result.Frame;
		_actuators.WriteTargetFrame( Motion.Output );
		_actuators.ApplyRetrievalFeedback( Motion.Output.Translation );
	}
}
