using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Reads physical controller state into a constraint solver endpoint.</summary>
internal static class PhysicalSolverBody
{
	internal static DynamicBody Read( PhysicalBodyState body )
	{
		ArgumentNullException.ThrowIfNull( body );
		var responds = (body.MotionMode & 4) != 0;
		var diagonal = body.InverseInertiaDiagonal;
		var offDiagonal = body.InverseInertiaOffDiagonal;
		var orientation = body.Orientation;
		var basis = body.Pose.Rotation;
		return new DynamicBody
		{
			Position = body.Pose.Translation,
			Rotation = new Quaternion( orientation.X, orientation.Y, orientation.Z, orientation.W ),
			RotationBasis = new Matrix4x4(
				basis.X.X, basis.X.Y, basis.X.Z, 0f,
				basis.Y.X, basis.Y.Y, basis.Y.Z, 0f,
				basis.Z.X, basis.Z.Y, basis.Z.Z, 0f,
				0f, 0f, 0f, 1f ),
			Velocity = body.Velocity,
			AngularVelocity = body.AngularVelocity,
			Acceleration = body.LinearAccumulator,
			AngularAcceleration = body.AngularAccumulator,
			InverseMass = responds ? body.InverseMass : 0f,
			WorldInverseInertia = responds ? new Matrix4x4(
				diagonal.X, offDiagonal.X, offDiagonal.Y, 0f,
				offDiagonal.X, diagonal.Y, offDiagonal.Z, 0f,
				offDiagonal.Y, offDiagonal.Z, diagonal.Z, 0f,
				0f, 0f, 0f, 1f ) : default,
			MotionMetric = body.MotionMetric,
			QuietSteps = body.AccumulatorState,
			MotionFlags = body.MotionMode
		};
	}

	// Write the integration result directly; target-frame normalization would change it again.
	internal static void WriteDynamics( PhysicalBodyState body, in DynamicBody result )
	{
		var q = result.Rotation;
		var basis = result.RotationBasis ?? Matrix4x4.CreateFromQuaternion( q );
		body.Orientation = new Vector4( q.X, q.Y, q.Z, q.W );
		body.Pose = new LimbRigidTransform( new LimbFrame(
			new Vector3( basis.M11, basis.M12, basis.M13 ),
			new Vector3( basis.M21, basis.M22, basis.M23 ),
			new Vector3( basis.M31, basis.M32, basis.M33 ) ), result.Position );
		body.Velocity = result.Velocity;
		body.AngularVelocity = result.AngularVelocity;
		body.LinearAccumulator = result.Acceleration;
		body.AngularAccumulator = result.AngularAcceleration;
		body.MotionMetric = result.MotionMetric;
		body.AccumulatorState = result.QuietSteps;
		var inertia = result.WorldInverseInertia;
		body.InverseInertiaDiagonal = new Vector3( inertia.M11, inertia.M22, inertia.M33 );
		body.InverseInertiaOffDiagonal = new Vector3( inertia.M12, inertia.M13, inertia.M23 );
	}
}
