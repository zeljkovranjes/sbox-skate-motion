namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Character placement and reset helper frames derived from a weighted pose reference.</summary>
internal readonly record struct PhysicalRigPlacement(
	LimbRigidTransform ModelToWorld,
	LimbRigidTransform WorldToModel,
	LimbRigidTransform RelativeFrame,
	LimbRigidTransform FirstHelperFrame,
	LimbRigidTransform SecondHelperFrame )
{
	internal static PhysicalRigPlacement Create( LimbRigidTransform source, LimbRigidTransform offset,
		Vector3 referencePosition, Vector3 weightedPosePosition )
	{
		var rotation = source.Compose( offset ).Rotation;
		var translation = referencePosition - rotation.Transform( weightedPosePosition, Vector3.Zero );
		var world = new LimbRigidTransform( rotation, translation );
		return new PhysicalRigPlacement( world, world.RigidInverse(), LimbRigidTransform.Identity,
			LimbRigidTransform.Identity, LimbRigidTransform.Identity );
	}
}
