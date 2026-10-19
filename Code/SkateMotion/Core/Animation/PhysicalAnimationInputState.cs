namespace SkateMotion.Core.Animation;

/// <summary>Shared pose, body and attachment state retained across animation input updates.</summary>
internal sealed class PhysicalAnimationInputState
{
	internal PhysicalAnimationPoseState Pose { get; }
	internal GrindApproachState Grind { get; }
	internal PhysicalBodyBinding[] RiderBodies { get; }
	internal PhysicalBodyState BoardReferenceBody { get; }
	internal bool ResetRequested { get; set; }
	internal byte MotionFlag { get; set; }
	internal byte FootActivation { get; set; }

	// Retain the earlier storage name for callers while exposing its actual consumer.
	internal byte RigResetFlag
	{
		get => FootActivation;
		set => FootActivation = value;
	}

	internal PhysicalJointDrive FirstDrive { get; set; }
	internal PhysicalJointDrive SecondDrive { get; set; }

	internal PhysicalAnimationInputState( PhysicalAnimationPoseState pose, GrindApproachState grind,
		PhysicalBodyBinding[] riderBodies, PhysicalBodyState boardReferenceBody )
	{
		ArgumentNullException.ThrowIfNull( pose );
		ArgumentNullException.ThrowIfNull( grind );
		ArgumentNullException.ThrowIfNull( riderBodies );
		ArgumentNullException.ThrowIfNull( boardReferenceBody );
		Pose = pose;
		Grind = grind;
		RiderBodies = riderBodies;
		BoardReferenceBody = boardReferenceBody;
	}
}
