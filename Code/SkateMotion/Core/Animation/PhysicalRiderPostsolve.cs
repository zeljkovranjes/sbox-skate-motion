using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Orders contact classification before pose-error feedback on one physical update.</summary>
internal sealed class PhysicalRiderPostsolve
{
	private readonly PhysicalPoseFeedback _feedback;

	internal PhysicalRiderPostsolve( LimbSolverMath math ) => _feedback = new( math );

	internal void Update( PhysicalContactFeedbackState contacts, PhysicalPoseFeedbackState feedback,
		PhysicalPoseResetState readback, PhysicalPosePreparationState preparation,
		LimbRigidTransform[] bodyPoses, LimbRigidTransform[] mutablePose, float[] bodyWeights,
		PhysicalContactFeedbackSettings settings, PhysicalContactFeedbackInput contactInput,
		PhysicalPoseFeedbackInput poseInput, PhysicalRiderContact[] worldContacts )
	{
		UpdateCore( contacts, feedback, readback, preparation, bodyPoses, mutablePose, bodyWeights,
			settings, contactInput, poseInput, worldContacts, null );
	}

	internal void UpdateForActor( PhysicalContactFeedbackState contacts, PhysicalPoseFeedbackState feedback,
		PhysicalPoseResetState readback, PhysicalPosePreparationState preparation,
		LimbRigidTransform[] bodyPoses, LimbRigidTransform[] mutablePose, float[] bodyWeights,
		PhysicalContactFeedbackSettings settings, PhysicalContactActorState actor, PhysicalRigBodyModes modes,
		Vector3 velocity, PhysicalPoseFeedbackInput poseInput, bool resetTimer, PhysicalRiderContact[] worldContacts )
	{
		var input = PhysicalContactInputBuilder.Build( actor, readback, feedback, modes,
			velocity, poseInput.ContactNormal, poseInput.DeltaTime, resetTimer );
		UpdateCore( contacts, feedback, readback, preparation, bodyPoses, mutablePose, bodyWeights,
			settings, input, poseInput, worldContacts, modes );
	}

	internal void UpdateFromWorld( PhysicalContactFeedbackState contacts, PhysicalPoseFeedbackState feedback,
		PhysicalPoseResetState readback, PhysicalPosePreparationState preparation,
		LimbRigidTransform[] bodyPoses, LimbRigidTransform[] mutablePose, float[] bodyWeights,
		PhysicalContactFeedbackSettings settings, PhysicalContactActorState actor, PhysicalRigBodyModes modes,
		Vector3 velocity, PhysicalPoseFeedbackInput poseInput, bool resetTimer, PhysicalContactOwner owner )
	{
		ArgumentNullException.ThrowIfNull( owner );
		var input = PhysicalContactInputBuilder.Build( actor, readback, feedback, modes,
			velocity, poseInput.ContactNormal, poseInput.DeltaTime, resetTimer );
		UpdateCore( contacts, feedback, readback, preparation, bodyPoses, mutablePose, bodyWeights,
			settings, input, poseInput, owner.RiderContacts, modes );
	}

	private void UpdateCore( PhysicalContactFeedbackState contacts, PhysicalPoseFeedbackState feedback,
		PhysicalPoseResetState readback, PhysicalPosePreparationState preparation,
		LimbRigidTransform[] bodyPoses, LimbRigidTransform[] mutablePose, float[] bodyWeights,
		PhysicalContactFeedbackSettings settings, PhysicalContactFeedbackInput contactInput,
		PhysicalPoseFeedbackInput poseInput, ReadOnlySpan<PhysicalRiderContact> worldContacts, PhysicalRigBodyModes modes )
	{
		if ( contactInput.DeltaTime != poseInput.DeltaTime )
			throw new ArgumentException( "Contact and pose feedback must share a physical update interval." );
		PhysicalContactFeedback.UpdateSpan( contacts, readback, bodyPoses, bodyWeights, settings, contactInput, worldContacts );
		modes?.AdvanceCollisionCounters();
		_feedback.UpdateFromContacts( feedback, readback, preparation, mutablePose, poseInput, contacts );
	}
}
