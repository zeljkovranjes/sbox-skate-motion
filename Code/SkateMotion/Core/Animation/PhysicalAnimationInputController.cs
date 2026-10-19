namespace SkateMotion.Core.Animation;

using static LimbSolverMath;

/// <summary>Orders body preparation, pose inputs, attachment, publication and reset requests.</summary>
internal sealed class PhysicalAnimationInputController
{
	private readonly BoardAttachmentSequence _attachment;
	private readonly PhysicalJointDrive _resetDrive;

	internal PhysicalAnimationInputController( BoardAttachmentSequence attachment, PhysicalJointDrive resetDrive )
	{
		ArgumentNullException.ThrowIfNull( attachment );
		_attachment = attachment;
		_resetDrive = resetDrive;
	}

	internal void Update( PhysicalAnimationInputState state, PhysicalAnimationInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		var velocity = state.BoardReferenceBody.Velocity;
		PhysicalAnimationBodyPreparation.Update( state.RiderBodies, input.RiderBodyCount );
		var pose = state.Pose;
		PhysicalAnimationPoseProcessor.Prepare( pose, input.Pose with { BoardVelocity = velocity } );
		var attachment = BindAttachment( pose, input );
		_attachment.Update( attachment, state.Grind, ref pose.Skeleton.MutableBones[ 0 ], pose.Authored );
		PhysicalAnimationPoseProcessor.Publish( pose, input.Weights, input.CacheFrame,
			input.ContactActive, input.PublishExternalMotion, input.ExternalMotion );
		if ( state.ResetRequested )
		{
			state.MotionFlag = 0;
			state.ResetRequested = false;
			state.FootActivation = 1;
			state.SecondDrive = _resetDrive;
			state.FirstDrive = _resetDrive;
		}
	}

	private static BoardAttachmentInput BindAttachment( PhysicalAnimationPoseState pose, PhysicalAnimationInput input )
	{
		var attributes = pose.Attributes;
		var motion = attributes.MotionFlags;
		var action = attributes.ActionFlags;
		var rig = attributes.RigFlags;
		var board = pose.Skeleton.MutableBones[ 0 ];
		var firstHand = (action & 4UL) != 0;
		var supplied = input.Attachment;
		return supplied with
		{
			Hand = supplied.AttachFromHand ? input.Pose.Clips[ firstHand ? input.FirstHandBone : input.SecondHandBone ] : default,
			Helper = supplied.AttachFromHand ? input.Pose.Clips[ input.Pose.HelperClipBones[ firstHand ? 2 : 3 ] ] : default,
			DescendingMode = (action & (1UL << 8)) != 0,
			SuppressDescending = (action & (1UL << 16)) != 0,
			Grind = supplied.Grind with
			{
				Suppressed = (motion & (1UL << 3)) != 0 || (action & (1UL << 47)) != 0 || (rig & (1UL << 58)) != 0,
				Approach = supplied.Grind.Approach with { CandidateZero = (rig & (1UL << 22)) != 0 },
				MutableBoard = board
			},
			Descending = supplied.Descending with { Blocked = (rig & (1UL << 34)) != 0, Board = board },
			Vertical = supplied.Vertical with
			{
				Trigger = attributes.Balance,
				PrimaryActive = (action & (1UL << 28)) != 0,
				SecondaryActive = (action & (1UL << 32)) != 0,
				Suppress = (action & (1UL << 30)) != 0,
				StopTracking = (motion & (1UL << 5)) != 0,
				ForceRequest = (motion & (1UL << 21)) != 0,
				CurrentHeight = pose.Cache.WeightedPosition.Y - board.Translation.Y,
				ProjectedSpeed = Dot( input.VerticalAxis, input.VerticalVelocity )
			}
		};
	}
}
