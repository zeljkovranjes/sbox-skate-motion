namespace SkateMotion.Core.Animation;

/// <summary>Coordinates grounded placement and rig targets from the physical deck.</summary>
internal sealed class PhysicalGroundCharacterController
{
	private readonly PhysicalRiderFrameController _placement;
	private readonly PhysicalRigController _rig;

	internal PhysicalGroundCharacterController( PhysicalRiderFrameController placement, PhysicalRigController rig )
	{
		ArgumentNullException.ThrowIfNull( placement );
		ArgumentNullException.ThrowIfNull( rig );
		_placement = placement;
		_rig = rig;
	}

	internal void Update( PhysicalCharacterState state, PhysicalRiderFrameState placement,
		PhysicalGroundCharacterInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( placement );
		placement.ResetHeading = true;
		_placement.Update( placement, state.BoardBodies[ 6 ], input.OrientationSource, input.PlacementDeltaTime );
		var anchor = new LimbRigidTransform( LimbRigidTransform.Identity.Rotation, placement.Anchor );
		state.RootFrame = anchor.Compose( placement.RelativeFrame.Compose( state.Rig.Rig.Skeleton.MutableBones[ 0 ] ) );
		state.Output.Frame = state.RootFrame;
		state.Output.UpdateFlags &= ~0x80000UL;

		state.AuthoredFrame = state.BoardBodies[ 6 ].Read();
		var preparation = state.Rig.Preparation;
		preparation.SecondaryTarget = state.AuthoredFrame;
		preparation.ModelOffset = placement.RootFrame;
		preparation.ModelToWorld = placement.ModelToWorld;
		PhysicalFeedbackFrames.Update( preparation, state.Rig.Reset.WorldReference, 0f );

		var frames = input.RigInput.Frames with
		{
			ModelToWorld = placement.ModelToWorld,
			WorldToModel = placement.WorldToModel,
			Authored = state.AuthoredFrame,
			Contact = placement.WorldToBoard,
			Offset = placement.BoardFrame
		};
		_rig.Update( state.Rig, input.RigInput with { Frames = frames } );
		state.RelativeBodyFrame = LimbRigidTransform.Identity;
	}
}
