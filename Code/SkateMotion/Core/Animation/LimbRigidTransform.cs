namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>A limb rotation and translation, without segment scaling.</summary>
internal readonly record struct LimbRigidTransform( LimbFrame Rotation, Vector3 Translation )
{
	internal static LimbRigidTransform Identity => new(
		new LimbFrame( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ ), Vector3.Zero );

	internal Vector3 Transform( Vector3 point ) => Rotation.Transform( point, Translation );

	internal LimbRigidTransform Compose( LimbRigidTransform right ) => new(
		new LimbFrame(
			Rotation.Transform( right.Rotation.X, Vector3.Zero ),
			Rotation.Transform( right.Rotation.Y, Vector3.Zero ),
			Rotation.Transform( right.Rotation.Z, Vector3.Zero ) ), Transform( right.Translation ) );

	internal LimbRigidTransform RigidInverse()
	{
		var inverse = new LimbFrame(
			new Vector3( Rotation.X.X, Rotation.Y.X, Rotation.Z.X ),
			new Vector3( Rotation.X.Y, Rotation.Y.Y, Rotation.Z.Y ),
			new Vector3( Rotation.X.Z, Rotation.Y.Z, Rotation.Z.Z ) );
		var translation = Scale( inverse.Z, 0f - Translation.Z );
		translation = AddScaled( inverse.Y, 0f - Translation.Y, translation );
		translation = AddScaled( inverse.X, 0f - Translation.X, translation );
		return new LimbRigidTransform( inverse, translation );
	}

	internal LimbRigidTransform ApplyToBone( LimbRigidTransform bone )
	{
		var axes = new LimbFrame(
			Rotation.Transform( bone.Rotation.X, Vector3.Zero ),
			Rotation.Transform( bone.Rotation.Y, Vector3.Zero ),
			Rotation.Transform( bone.Rotation.Z, Vector3.Zero ) );
		return new LimbRigidTransform( axes.Orthonormalized(), Transform( bone.Translation ) );
	}
}
