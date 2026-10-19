namespace SkateMotion.Core.Animation;

/// <summary>Places the character, updates the board and runs the complete physical rig sequence.</summary>
internal sealed class PhysicalCharacterController
{
	private readonly PhysicalRigController _rig;
	private readonly BoardFrameCorrection _boardCorrection;

	internal PhysicalCharacterController( PhysicalRigController rig, BoardFrameCorrection boardCorrection )
	{
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( boardCorrection );
		_rig = rig;
		_boardCorrection = boardCorrection;
	}

	internal void Update( PhysicalCharacterState state, PhysicalCharacterInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.Placement = PhysicalRigPlacement.Create( input.SourceFrame, input.LocalFrame,
			input.ReferencePosition, state.Rig.Cache.WeightedPosition );
		state.RootFrame = state.Placement.ModelToWorld.Compose( state.Rig.Rig.Skeleton.MutableBones[ 0 ] );
		state.Output.Frame = state.RootFrame;
		state.Output.UpdateFlags |= 0x80000UL;

		// Read after writing the root: both fields may refer to the same frame owner.
		var board = PhysicalBoardFrame.Update( _boardCorrection, state.BoardTarget,
			state.BoardSource.Frame, state.BoardOrientation, state.BoardCorrectionFlag, true );
		state.BoardOrientation = board.Orientation;
		state.BoardCorrectionFlag = board.CorrectionFlag;
		state.AuthoredFrame = board.Frame;
		PhysicalBoardFeedback.Update( state.BoardBodies, state.BoardBodies.Length,
			board.Frame.Translation, input.BoardDeltaTime, input.BoardFeedbackGain );
		state.Rig.Preparation.SecondaryTarget = board.Frame;

		var frames = input.RigInput.Frames with
		{
			ModelToWorld = state.Placement.ModelToWorld,
			WorldToModel = state.Placement.WorldToModel,
			Authored = board.Frame
		};
		_rig.UpdateWithFeedbackTargets( state.Rig, input.RigInput with { Frames = frames }, 0f );
		state.RelativeBodyFrame = LimbRigidTransform.Identity;
	}
}
