namespace SkateMotion.Core.Animation;

/// <summary>Coordinates airborne placement, board feedback and the physical character rig.</summary>
internal sealed class PhysicalAirCharacterController
{
	private readonly PhysicalAirFrameController _placement;
	private readonly PhysicalRigController _rig;
	private readonly BoardFrameCorrection _boardCorrection;

	internal PhysicalAirCharacterController( PhysicalAirFrameController placement,
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
		PhysicalAirCharacterInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( placement );
		_placement.Update( placement, new( input.OrientationSource, input.PredictedPosition,
			state.Rig.Cache.WeightedPosition, input.AdvanceRotation, input.PositiveRotation, input.RotationSteps ) );
		state.RootFrame = placement.ModelToWorld.Compose( state.Rig.Rig.Skeleton.MutableBones[ 0 ] );
		state.Output.Frame = state.RootFrame;
		state.Output.UpdateFlags |= 0x80000UL;

		// Read after publishing the root because the source can share the output owner.
		var board = PhysicalBoardFrame.Update( _boardCorrection, state.BoardTarget,
			state.BoardSource.Frame, state.BoardOrientation, state.BoardCorrectionFlag, true );
		state.BoardOrientation = board.Orientation;
		state.BoardCorrectionFlag = board.CorrectionFlag;
		state.AuthoredFrame = board.Frame;
		PhysicalBoardFeedback.Update( state.BoardBodies, state.BoardBodies.Length,
			board.Frame.Translation, input.BoardDeltaTime, input.BoardFeedbackGain );

		var preparation = state.Rig.Preparation;
		preparation.SecondaryTarget = board.Frame;
		preparation.ModelOffset = placement.RootFrame;
		preparation.ModelToWorld = placement.ModelToWorld;
		PhysicalFeedbackFrames.Update( preparation, state.Rig.Reset.WorldReference, 0f );
		placement.Anchor = board.Frame.Translation;
		placement.PreserveAnchor = true;

		var frames = input.RigInput.Frames with
		{
			ModelToWorld = placement.ModelToWorld,
			WorldToModel = placement.WorldToModel,
			Authored = board.Frame,
			Contact = placement.WorldToBoard,
			Offset = placement.BoardFrame
		};
		_rig.Update( state.Rig, input.RigInput with { Frames = frames } );
	}
}
