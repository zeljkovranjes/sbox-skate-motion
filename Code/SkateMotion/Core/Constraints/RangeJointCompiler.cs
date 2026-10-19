namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Compiles cone and twist ranges together with linear joint constraints.</summary>
internal static class RangeJointCompiler
{
	internal static JointConstraint Compile( in DynamicBody a, in DynamicBody b,
		in JointGeometry linear, in RangeJointGeometry angular, float step )
	{
		if ( angular.SwingMode is not (1 or 4) || angular.TwistMode is not (1 or 2) )
			throw new ArgumentException( "The range compiler requires cone or speed-only angular modes.", nameof( angular ) );
		if ( !float.IsFinite( angular.SwingCosine ) || !float.IsFinite( angular.TwistCosine )
			|| !float.IsFinite( angular.MaximumSwingSpeed ) || angular.MaximumSwingSpeed < 0f
			|| !float.IsFinite( angular.MaximumTwistSpeed ) || angular.MaximumTwistSpeed < 0f )
			throw new ArgumentException( "Angular ranges require finite limits and nonnegative finite speeds.", nameof( angular ) );

		var result = JointLinearCompiler.Compile( a, b, linear, step );
		var pair = result.Pair;
		var frameA = QuaternionFrame.Compose( a.Rotation, angular.LocalFrameA );
		var frameB = QuaternionFrame.Compose( b.Rotation, angular.LocalFrameB );
		var axesA = QuaternionFrame.Rotation( frameA );
		var axesB = QuaternionFrame.Rotation( frameB );
		var error = QuaternionFrame.Between( frameA, frameB ).Error;
		var scalar = ((frameA.W * frameB.W + frameA.Z * frameB.Z)
			+ frameA.Y * frameB.Y) + frameA.X * frameB.X;
		var relative = QuaternionFrame.Rotation( new Quaternion( error, scalar ) );
		var axisA = Row( axesA, 0 );
		var x = Row( axesB, 0 );
		var y = Row( axesB, 1 );
		var z = Row( axesB, 2 );
		var upper = new Vector3( float.MaxValue );
		var lower = -upper;

		// Speed-only modes retain unrestricted positional bounds. They are not locks.
		if ( angular.SwingMode == 1 && relative.M11 < .99999f )
		{
			var squared = MathF.FusedMultiplyAdd( relative.M21, relative.M21, relative.M31 * relative.M31 );
			var length = squared * ConstraintReciprocal.InverseSquareRoot( squared );
			var inverse = ConstraintReciprocal.Inverse( length );
			y = ConstraintVectorMath.Cross( axisA, x ) * inverse;
			z = ConstraintVectorMath.Cross( y, axisA );
			lower.Y = (angular.SwingCosine - relative.M11) * inverse;
		}
		if ( angular.TwistMode == 1 )
		{
			x = axisA;
			var denominator = relative.M23 - relative.M32;
			if ( denominator != 0f )
			{
				var numerator = MathF.FusedMultiplyAdd( relative.M11 + 1f, angular.TwistCosine,
					-(relative.M22 + relative.M33) );
				var correction = ConstraintReciprocal.Inverse( denominator ) * numerator;
				if ( denominator < 0f )
					upper.X = correction;
				else
					lower.X = correction;
			}
		}

		var axes = new Matrix4x4(
			x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, 0f, 0f, 0f, 1f );
		var inverseAxes = Matrix4x4.Transpose( axes );
		var scales = new Vector3( Scale( x, pair ), Scale( y, pair ), Scale( z, pair ) );
		var motion = ConstraintVectorMath.Project( (b.AngularVelocity - a.AngularVelocity
			+ (b.AngularAcceleration - a.AngularAcceleration) * step) * step, axes );
		var allowance = new Vector3( angular.MaximumTwistSpeed, angular.MaximumSwingSpeed, angular.MaximumSwingSpeed ) * step;
		pair.AngularAxes = axes;
		pair.AngularCoefficients = inverseAxes * Matrix4x4.CreateScale( scales );
		result.Pair = pair;
		result.PositiveAngularBias = Vector3.Max( Vector3.Min( motion - allowance, motion + upper ), motion + lower ) * scales;
		result.NegativeAngularBias = Vector3.Max( Vector3.Min( motion + allowance, motion + upper ), motion + lower ) * scales;
		return result;
	}

	private static Vector3 Row( Matrix4x4 value, int index ) => index switch
	{
		0 => new( value.M11, value.M12, value.M13 ),
		1 => new( value.M21, value.M22, value.M23 ),
		_ => new( value.M31, value.M32, value.M33 )
	};

	private static float Scale( Vector3 axis, in ConstraintPair pair )
	{
		var response = Vector3.TransformNormal( axis, pair.InverseInertiaA )
			+ Vector3.TransformNormal( axis, pair.InverseInertiaB );
		var denominator = Vector3.Dot( axis, response );
		if ( !float.IsFinite( denominator ) || denominator <= 0f )
			throw new ArgumentException( "A joint axis requires positive finite angular response." );
		return .5f / denominator;
	}
}
