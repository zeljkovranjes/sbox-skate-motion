using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Resolves movement-state contact gates and retained physical reference motion.</summary>
internal static class PhysicalContactInputBuilder
{
	internal static PhysicalContactFeedbackInput Build( PhysicalContactActorState actor,
		PhysicalPoseResetState readback, PhysicalPoseFeedbackState feedback, PhysicalRigBodyModes modes,
		Vector3 velocity, Vector3 normal, float deltaTime, bool resetTimer )
	{
		var alternateReference = actor.State is 501 or 503;
		var special = actor.ParentState == 500 && !alternateReference;
		var position = actor.State is 601 or 602 ? actor.AlternatePosition : actor.Position;
		if ( special && (actor.MotionBits & (1ul << 33)) != 0 )
		{
			position = actor.SpecialPosition;
			normal = actor.SpecialNormal;
		}
		return new( position, normal, actor.State == 201 ? readback.Deltas[ 23 ] : velocity,
			feedback.Velocity, deltaTime, modes.Secondary,
			(actor.ActionBits & (1ul << 47)) != 0, (actor.ActionBits & (1ul << 60)) != 0,
			special, alternateReference, actor.ParentState == 600, resetTimer );
	}
}
