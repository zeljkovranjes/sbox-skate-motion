namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal static class QuaternionJointCompiler
{
	// Quaternion joint: both swing axes lock; twist may lock or have a speed limit.
	// Cone and angular range branches are separate and are not approximated here.
	internal static JointConstraint Compile( in DynamicBody a, in DynamicBody b,
		in JointGeometry linear, in QuaternionJointGeometry angular, float step )
	{
		var result = JointLinearCompiler.Compile( a, b, linear, step );
		var pair = result.Pair;
		var frameA = a.Rotation * angular.LocalFrameA;
		var frameB = b.Rotation * angular.LocalFrameB;
		var frame = QuaternionFrame.Between( frameA, frameB );
		var axisX = angular.LockTwist
			? new Vector3( frame.Axes.M11, frame.Axes.M12, frame.Axes.M13 )
			: Vector3.Transform( Vector3.UnitX, frameB );
		var axisY = new Vector3( frame.Axes.M21, frame.Axes.M22, frame.Axes.M23 );
		var axisZ = new Vector3( frame.Axes.M31, frame.Axes.M32, frame.Axes.M33 );
		var axes = new Matrix4x4(
			axisX.X, axisX.Y, axisX.Z, 0f,
			axisY.X, axisY.Y, axisY.Z, 0f,
			axisZ.X, axisZ.Y, axisZ.Z, 0f,
			0f, 0f, 0f, 1f );
		var inverseAxes = Matrix4x4.Transpose( axes );
		var scales = new Vector3(
			AngularScale( axisX, pair ), AngularScale( axisY, pair ), AngularScale( axisZ, pair ) );
		var relativeMotion = (b.AngularVelocity - a.AngularVelocity
			+ (b.AngularAcceleration - a.AngularAcceleration) * step) * step;
		var motion = Vector3.TransformNormal( relativeMotion, inverseAxes );
		var positive = motion + frame.Error * 2f;
		var negative = positive;
		if ( !angular.LockTwist )
		{
			if ( !float.IsFinite( angular.MaximumTwistSpeed ) || angular.MaximumTwistSpeed < 0f )
				throw new ArgumentOutOfRangeException( nameof( angular ) );
			positive.X = motion.X - angular.MaximumTwistSpeed * step;
			negative.X = motion.X + angular.MaximumTwistSpeed * step;
		}

		pair.AngularAxes = axes;
		pair.AngularCoefficients = inverseAxes * Matrix4x4.CreateScale( scales );
		result.Pair = pair;
		result.PositiveAngularBias = positive * scales;
		result.NegativeAngularBias = negative * scales;
		return result;
	}

	private static float AngularScale( Vector3 axis, in ConstraintPair pair )
	{
		var response = Vector3.TransformNormal( axis, pair.InverseInertiaA )
			+ Vector3.TransformNormal( axis, pair.InverseInertiaB );
		var denominator = Vector3.Dot( axis, response );
		if ( !float.IsFinite( denominator ) || denominator <= 0f )
			throw new ArgumentException( "A joint axis requires positive finite angular response." );
		return 0.5f / denominator;
	}
}
