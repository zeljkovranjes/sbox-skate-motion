namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal static class JointLinearCompiler
{
	// Compiles the translational portion shared by the angular joint compilers.
	internal static JointConstraint Compile( in DynamicBody a, in DynamicBody b,
		in JointGeometry geometry, float step )
	{
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );

		var offsetA = TransformAnchor( geometry.LocalAnchorA, a );
		var offsetB = TransformAnchor( geometry.LocalAnchorB, b );
		var rotation = QuaternionFrame.Compose( b.Rotation, geometry.LinearFrameB );
		var axes = QuaternionFrame.Rotation( rotation );
		var inverseAxes = Matrix4x4.Transpose( axes );
		var inertiaA = a.InverseMass > 0f ? a.WorldInverseInertia : default;
		var inertiaB = b.InverseMass > 0f ? b.WorldInverseInertia : default;
		var inverseDenominators = new Vector3(
			InverseDenominator( new Vector3( axes.M11, axes.M12, axes.M13 ), offsetA, offsetB, a, b, inertiaA, inertiaB ),
			InverseDenominator( new Vector3( axes.M21, axes.M22, axes.M23 ), offsetA, offsetB, a, b, inertiaA, inertiaB ),
			InverseDenominator( new Vector3( axes.M31, axes.M32, axes.M33 ), offsetA, offsetB, a, b, inertiaA, inertiaB ) );
		var predictedA = PredictedMotion( a, offsetA, step );
		var predictedB = PredictedMotion( b, offsetB, step );
		var relativeMotion = predictedB - predictedA;
		var motion = ConstraintVectorMath.Project( relativeMotion, axes );
		var separation = (b.Position + offsetB + predictedB) - (a.Position + offsetA + predictedA);
		var predicted = ConstraintVectorMath.Project( separation, axes );
		var minimum = predicted - geometry.LinearExtent;
		var maximum = predicted + geometry.LinearExtent;
		var allowance = geometry.MaximumLinearSpeed * step;
		return new JointConstraint
		{
			Pair = new ConstraintPair
			{
				BodyA = geometry.BodyA, BodyB = geometry.BodyB,
				OffsetA = offsetA, OffsetB = offsetB,
				LinearAxes = axes,
				LinearCoefficients = inverseAxes * Matrix4x4.CreateScale( inverseDenominators ),
				InverseInertiaA = inertiaA, InverseInertiaB = inertiaB
			},
			PositiveLinearBias = Vector3.Clamp( motion - allowance, minimum, maximum ) * inverseDenominators,
			NegativeLinearBias = Vector3.Clamp( motion + allowance, minimum, maximum ) * inverseDenominators
		};
	}

	private static Vector3 TransformAnchor( Vector3 anchor, in DynamicBody body )
	{
		if ( body.RotationBasis is not { } basis )
			return Vector3.Transform( anchor, body.Rotation );
		return new Vector3(
			MathF.FusedMultiplyAdd( basis.M31, anchor.Z, MathF.FusedMultiplyAdd( basis.M21, anchor.Y, basis.M11 * anchor.X ) ),
			MathF.FusedMultiplyAdd( basis.M32, anchor.Z, MathF.FusedMultiplyAdd( basis.M22, anchor.Y, basis.M12 * anchor.X ) ),
			MathF.FusedMultiplyAdd( basis.M33, anchor.Z, MathF.FusedMultiplyAdd( basis.M23, anchor.Y, basis.M13 * anchor.X ) ) );
	}

	private static Vector3 PredictedMotion( in DynamicBody body, Vector3 offset, float step )
	{
		var velocity = PointMotion( body.Velocity, body.AngularVelocity, offset );
		var acceleration = PointMotion( body.Acceleration, body.AngularAcceleration, offset );
		return new Vector3(
			MathF.FusedMultiplyAdd( acceleration.X, step, velocity.X ) * step,
			MathF.FusedMultiplyAdd( acceleration.Y, step, velocity.Y ) * step,
			MathF.FusedMultiplyAdd( acceleration.Z, step, velocity.Z ) * step );
	}

	private static Vector3 PointMotion( Vector3 linear, Vector3 angular, Vector3 offset ) => new(
		MathF.FusedMultiplyAdd( angular.Y, offset.Z, MathF.FusedMultiplyAdd( -angular.Z, offset.Y, linear.X ) ),
		MathF.FusedMultiplyAdd( angular.Z, offset.X, MathF.FusedMultiplyAdd( -angular.X, offset.Z, linear.Y ) ),
		MathF.FusedMultiplyAdd( angular.X, offset.Y, MathF.FusedMultiplyAdd( -angular.Y, offset.X, linear.Z ) ) );

	private static float InverseDenominator( Vector3 axis, Vector3 offsetA, Vector3 offsetB,
		in DynamicBody a, in DynamicBody b, in Matrix4x4 inertiaA, in Matrix4x4 inertiaB )
	{
		var torqueA = ConstraintVectorMath.Cross( offsetA, axis );
		var torqueB = ConstraintVectorMath.Cross( offsetB, axis );
		var responseA = torqueA * ConstraintVectorMath.Transform( torqueA, inertiaA );
		var responseB = ConstraintVectorMath.Transform( torqueB, inertiaB );
		var response = new Vector3(
			MathF.FusedMultiplyAdd( torqueB.X, responseB.X, responseA.X ),
			MathF.FusedMultiplyAdd( torqueB.Y, responseB.Y, responseA.Y ),
			MathF.FusedMultiplyAdd( torqueB.Z, responseB.Z, responseA.Z ) );
		var denominator = (a.InverseMass + b.InverseMass + response.X) + (response.Y + response.Z);
		if ( !float.IsFinite( denominator ) || denominator <= 0f )
			throw new ArgumentException( "A joint direction requires positive finite effective inverse mass." );
		return 1f / denominator;
	}
}
