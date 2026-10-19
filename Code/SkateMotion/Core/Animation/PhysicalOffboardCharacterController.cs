namespace SkateMotion.Core.Animation;

/// <summary>Orders pre-adoption targets, completed walking frame, root publication, rig and direction updates.</summary>
internal sealed class PhysicalOffboardCharacterController
{
	private readonly PhysicalLocomotionFrameAdoption _adoption;
	private readonly PhysicalRigController _rig;
	private readonly BoardFrameCorrection _boardCorrection;
	private readonly PhysicalDirectionController _direction;

	internal PhysicalOffboardCharacterController( PhysicalLocomotionFrameAdoption adoption,
		PhysicalRigController rig, BoardFrameCorrection boardCorrection, PhysicalDirectionController direction )
	{
		ArgumentNullException.ThrowIfNull( adoption );
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( boardCorrection );
		ArgumentNullException.ThrowIfNull( direction );
		_adoption = adoption;
		_rig = rig;
		_boardCorrection = boardCorrection;
		_direction = direction;
	}

	internal void Update( PhysicalOffboardCharacterState state, PhysicalCharacterState character,
		PhysicalLocomotionFrameState placement, PhysicalDirectionState direction,
		in PhysicalOffboardCharacterInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( character );
		ArgumentNullException.ThrowIfNull( placement );
		ArgumentNullException.ThrowIfNull( direction );
		var preparation = character.Rig.Preparation;
		// These targets intentionally retain the previous frame's orientation.
		preparation.SecondaryTarget = preparation.ModelToWorld.Compose( character.Rig.Cache.Bones[ 0 ] );
		PhysicalFeedbackFrames.Update( preparation, input.FeedbackReference, .24f );
		_adoption.Update( placement, character.Rig, input.Solved, input.ActionFlags );

		if ( (input.RigFlags & 2UL) == 0 )
			state.LocalRootFrame = character.Rig.Rig.Skeleton.MutableBones[ 0 ];
		var model = preparation.ModelToWorld;
		character.RootFrame = model.Compose( state.LocalRootFrame );
		character.Output.Frame = character.RootFrame;
		character.Output.UpdateFlags |= 0x80000UL;

		if ( (input.RigFlags & ((1UL << 48) | 2UL)) != 0 )
		{
			// Reading after root publication preserves a shared output/board-source owner.
			var board = PhysicalBoardFrame.Update( _boardCorrection, character.BoardTarget,
				character.BoardSource.Frame, character.BoardOrientation, character.BoardCorrectionFlag, true );
			character.BoardOrientation = board.Orientation;
			character.BoardCorrectionFlag = board.CorrectionFlag;
			character.AuthoredFrame = board.Frame;
		}

		var frames = input.RigInput.Frames with
		{
			ModelToWorld = model, WorldToModel = placement.WorldToModel, Authored = character.AuthoredFrame
		};
		_rig.Update( character.Rig, input.RigInput with { Frames = frames } );
		character.RelativeBodyFrame = LimbRigidTransform.Identity;
		_direction.Update( direction, new( model.Rotation.Y, model.Rotation.Z, .5f,
			(input.MotionFlags & (1UL << 20)) != 0, true, input.Control ) );
	}
}
