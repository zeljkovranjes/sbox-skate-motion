namespace SkateMotion.Core.Animation;

/// <summary>Coordinates deck-driven placement, board target handoff and the physical character rig.</summary>
internal sealed class PhysicalRidingCharacterController
{
	private readonly PhysicalRiderFrameController _placement;
	private readonly PhysicalRigController _rig;
	private readonly BoardFrameCorrection _boardCorrection;

	internal PhysicalRidingCharacterController( PhysicalRiderFrameController placement,
		PhysicalRigController rig, BoardFrameCorrection boardCorrection )
	{
		ArgumentNullException.ThrowIfNull( placement );
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( boardCorrection );
		_placement = placement;
		_rig = rig;
		_boardCorrection = boardCorrection;
	}

	internal void Update( PhysicalCharacterState state, PhysicalRiderFrameState placement,
		PhysicalRidingCharacterInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( placement );
		_placement.Update( placement, state.BoardBodies[ 6 ], input.OrientationSource, input.PlacementDeltaTime );
		var anchor = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, placement.Anchor );
		state.RootFrame = anchor.Compose( placement.RelativeFrame.Compose( state.Rig.Rig.Skeleton.MutableBones[ 0 ] ) );
		state.Output.Frame = state.RootFrame;
		state.Output.UpdateFlags |= 0x80000UL;

		var preparation = state.Rig.Preparation;
		preparation.SecondaryTarget = state.RootFrame;
		preparation.ModelOffset = placement.RootFrame;
		preparation.ModelToWorld = placement.ModelToWorld;
		PhysicalFeedbackFrames.Update( preparation, state.Rig.Reset.WorldReference, 0f );

		// The frame source may share the output owner, so read it after publishing the root.
		var board = PhysicalBoardFrame.Update( _boardCorrection, state.BoardTarget,
			state.BoardSource.Frame, state.BoardOrientation, state.BoardCorrectionFlag, input.BoardCorrectionMode != 0 );
		state.BoardOrientation = board.Orientation;
		state.BoardCorrectionFlag = board.CorrectionFlag;
		state.AuthoredFrame = board.Frame;
		var frames = input.RigInput.Frames with
		{
			ModelToWorld = placement.ModelToWorld,
			WorldToModel = placement.WorldToModel,
			Authored = board.Frame,
			Contact = placement.WorldToBoard,
			Offset = placement.BoardFrame
		};
		_rig.Update( state.Rig, input.RigInput with { Frames = frames } );
		state.RelativeBodyFrame = LimbRigidTransform.Identity;
	}
}
