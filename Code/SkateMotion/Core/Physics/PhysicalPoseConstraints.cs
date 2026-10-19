using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Quaternion = System.Numerics.Quaternion;
using Vector4 = System.Numerics.Vector4;

/// <summary>Connects retained physical pose outputs to a shared solver's registered bodies.</summary>
internal static class PhysicalPoseConstraints
{
	// The owner selects registered attachments and resolves canonical bones to solver indices.
	internal static DriveConstraint Compile( PhysicalSolverWorld world, PhysicalJointState joint,
		bool secondAttachment, int childBody, int attachedBody, float step )
	{
		ArgumentNullException.ThrowIfNull( world );
		ArgumentNullException.ThrowIfNull( joint );
		var frame = secondAttachment ? joint.SecondFrame : joint.FirstFrame;
		var linear = secondAttachment ? joint.ThirdConstraint : joint.FirstConstraint;
		var angular = secondAttachment ? joint.FourthConstraint : joint.SecondConstraint;
		var geometry = new DriveGeometry( childBody, attachedBody, frame.ChildPosition, frame.BodyPosition,
			Rotation( frame.ChildRotation ), Rotation( frame.BodyRotation ) );
		return world.CompileDrive( geometry, Settings( linear ), Settings( angular ), step );
	}

	private static Quaternion Rotation( Vector4 value ) => new( value.X, value.Y, value.Z, value.W );

	private static DriveSettings Settings( PhysicalJointDrive drive ) => new(
		(DriveMode)drive.Mode, drive.VelocityGain, drive.MaximumForce, drive.PositionGain );
}
