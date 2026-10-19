using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Expresses an animated hand attachment in the board's local frame.</summary>
internal static class BoardHeldFrame
{
	internal static PhysicalJointFrame Create( LimbRigidTransform board, LimbRigidTransform hand )
	{
		var relative = board.RigidInverse().Compose( hand );
		return new PhysicalJointFrame(
			PhysicalPoseOutput.Rotation( LimbRigidTransform.Identity.Rotation ), Vector3.Zero,
			PhysicalPoseOutput.Rotation( relative.Rotation ), relative.Translation );
	}
}
