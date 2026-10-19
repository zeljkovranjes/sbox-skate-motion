namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal static class DriveCompiler
{
	internal static DriveConstraint Compile( in DynamicBody a, in DynamicBody b,
		in DriveGeometry geometry, in DriveSettings linear, in DriveSettings angular, float step )
	{
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );
		Validate( linear );
		Validate( angular );
		var frameA = QuaternionFrame.Compose( a.Rotation, geometry.LocalFrameA );
		var frameB = QuaternionFrame.Compose( b.Rotation, geometry.LocalFrameB );
		var pair = new ConstraintPair
		{
			BodyA = geometry.BodyA, BodyB = geometry.BodyB,
			OffsetA = TransformAnchor( geometry.LocalAnchorA, a ),
			OffsetB = TransformAnchor( geometry.LocalAnchorB, b ),
			LinearAxes = QuaternionFrame.Rotation( frameB ),
			InverseInertiaA = a.InverseMass > 0f ? a.WorldInverseInertia : default,
			InverseInertiaB = b.InverseMass > 0f ? b.WorldInverseInertia : default
		};
		var frame = QuaternionFrame.Between( frameA, frameB );
		var squared = Vector3.Clamp( frame.Error * frame.Error, Vector3.Zero, Vector3.One );
		var singular = squared.X == 1f || squared.Y == 1f || squared.Z == 1f;
		var factors = singular ? Vector3.One : new Vector3(
			ConstraintReciprocal.InverseSquareRoot( 1f - squared.X ),
			ConstraintReciprocal.InverseSquareRoot( 1f - squared.Y ),
			ConstraintReciprocal.InverseSquareRoot( 1f - squared.Z ) );
		// At an exact half turn the compiler substitutes all three frame-B axes.
		pair.AngularAxes = singular ? pair.LinearAxes : Matrix4x4.CreateScale( factors ) * frame.Axes;
		var linearScales = EffectiveScales( pair, a.InverseMass, b.InverseMass, false );
		var angularScales = EffectiveScales( pair, 0f, 0f, true );
		pair.LinearCoefficients = Matrix4x4.Transpose( pair.LinearAxes ) * Matrix4x4.CreateScale( linearScales );
		pair.AngularCoefficients = Matrix4x4.Transpose( pair.AngularAxes ) * Matrix4x4.CreateScale( angularScales );

		var separation = (b.Position + pair.OffsetB) - (a.Position + pair.OffsetA);
		var pointVelocity = PointMotion( b.Velocity, b.AngularVelocity, pair.OffsetB ) * step
			- PointMotion( a.Velocity, a.AngularVelocity, pair.OffsetA ) * step;
		var pointAcceleration = PointMotion( b.Acceleration, b.AngularAcceleration, pair.OffsetB ) * step * step
			- PointMotion( a.Acceleration, a.AngularAcceleration, pair.OffsetA ) * step * step;
		var linearResponse = Response( linear, step );
		var angularResponse = Response( angular, step );
		var linearMotion = Accumulate( pointAcceleration, linearResponse.Acceleration,
			Accumulate( Correction( separation, linear, step ), linearResponse.Position,
				pointVelocity * linearResponse.Velocity ) );
		var angularVelocity = ConstraintVectorMath.Project( (b.AngularVelocity - a.AngularVelocity) * step, pair.AngularAxes );
		var angularAcceleration = ConstraintVectorMath.Project( (b.AngularAcceleration - a.AngularAcceleration) * step * step, pair.AngularAxes );
		var angularMotion = Accumulate( angularAcceleration, angularResponse.Acceleration,
			Accumulate( Correction( frame.Error * factors * 2f, angular, step ), angularResponse.Position,
				angularVelocity * angularResponse.Velocity ) );
		return new DriveConstraint
		{
			Pair = pair,
			LinearBias = ConstraintVectorMath.Project( linearMotion, pair.LinearAxes ) * linearScales,
			AngularBias = angularMotion * angularScales,
			LinearLimit = linearScales * (linear.MaximumAcceleration * step * step),
			AngularLimit = angularScales * (angular.MaximumAcceleration * step * step),
			LinearGain = linearResponse.Acceleration,
			AngularGain = angularResponse.Acceleration
		};
	}

	private static Vector3 TransformAnchor( Vector3 anchor, in DynamicBody body )
	{
		if ( body.RotationBasis is not { } basis )
			return Vector3.Transform( anchor, body.Rotation );
		// Physical endpoints retain their stored axes and the ordered accumulation.
		return new Vector3(
			MathF.FusedMultiplyAdd( basis.M31, anchor.Z, MathF.FusedMultiplyAdd( basis.M21, anchor.Y, basis.M11 * anchor.X ) ),
			MathF.FusedMultiplyAdd( basis.M32, anchor.Z, MathF.FusedMultiplyAdd( basis.M22, anchor.Y, basis.M12 * anchor.X ) ),
			MathF.FusedMultiplyAdd( basis.M33, anchor.Z, MathF.FusedMultiplyAdd( basis.M23, anchor.Y, basis.M13 * anchor.X ) ) );
	}

	private static Vector3 EffectiveScales( in ConstraintPair pair, float inverseMassA, float inverseMassB, bool angular )
	{
		var axes = angular ? pair.AngularAxes : pair.LinearAxes;
		return new Vector3(
			InverseResponse( new Vector3( axes.M11, axes.M12, axes.M13 ), pair, inverseMassA, inverseMassB, angular ),
			InverseResponse( new Vector3( axes.M21, axes.M22, axes.M23 ), pair, inverseMassA, inverseMassB, angular ),
			InverseResponse( new Vector3( axes.M31, axes.M32, axes.M33 ), pair, inverseMassA, inverseMassB, angular ) );
	}

	private static float InverseResponse( Vector3 axis, in ConstraintPair pair,
		float inverseMassA, float inverseMassB, bool angular )
	{
		var torqueA = angular ? axis : ConstraintVectorMath.Cross( pair.OffsetA, axis );
		var torqueB = angular ? axis : ConstraintVectorMath.Cross( pair.OffsetB, axis );
		// Each endpoint adds its mass to its own rotational response before combining bodies.
		var response = (inverseMassA + RotationalResponse( torqueA, pair.InverseInertiaA ))
			+ (inverseMassB + RotationalResponse( torqueB, pair.InverseInertiaB ));
		if ( !float.IsFinite( response ) || response <= 0f )
			throw new ArgumentException( "A drive axis requires positive finite effective response." );
		return ConstraintReciprocal.Inverse( response );
	}

	private static float RotationalResponse( Vector3 torque, Matrix4x4 inertia )
	{
		var response = ConstraintVectorMath.Transform( torque, inertia );
		var product = torque * response;
		return (product.X + product.Y) + product.Z;
	}

	private static Vector3 PointMotion( Vector3 linear, Vector3 angular, Vector3 offset ) => new(
		MathF.FusedMultiplyAdd( angular.Y, offset.Z, MathF.FusedMultiplyAdd( -angular.Z, offset.Y, linear.X ) ),
		MathF.FusedMultiplyAdd( angular.Z, offset.X, MathF.FusedMultiplyAdd( -angular.X, offset.Z, linear.Y ) ),
		MathF.FusedMultiplyAdd( angular.X, offset.Y, MathF.FusedMultiplyAdd( -angular.Y, offset.X, linear.Z ) ) );

	private static Vector3 Accumulate( Vector3 value, float scale, Vector3 addend ) => new(
		MathF.FusedMultiplyAdd( value.X, scale, addend.X ),
		MathF.FusedMultiplyAdd( value.Y, scale, addend.Y ),
		MathF.FusedMultiplyAdd( value.Z, scale, addend.Z ) );

	private static (float Position, float Velocity, float Acceleration) Response( in DriveSettings settings, float step )
	{
		var damping = settings.Damping * step;
		if ( settings.Mode != DriveMode.Spring )
			return (ConstraintReciprocal.Inverse( 1f + damping ), 1f, 1f);
		var stiffness = settings.Strength * step * step;
		var inverse = ConstraintReciprocal.Inverse( 1f + stiffness + damping );
		return (stiffness * inverse, damping * inverse, (stiffness + damping) * inverse);
	}

	private static Vector3 Correction( Vector3 value, in DriveSettings settings, float step )
	{
		if ( settings.Mode != DriveMode.SpeedLimited )
			return value;
		var limit = settings.Strength * step;
		var components = value * value;
		var squared = (components.X + components.Y) + components.Z;
		return squared > limit * limit ? value * (limit * ConstraintReciprocal.InverseSquareRoot( squared )) : value;
	}

	private static void Validate( in DriveSettings settings )
	{
		// Retained pose outputs can extrapolate to signed coefficients and limits.
		if ( settings.Mode < DriveMode.Position || settings.Mode > DriveMode.SpeedLimited
			|| !float.IsFinite( settings.Strength )
			|| !float.IsFinite( settings.Damping )
			|| !float.IsFinite( settings.MaximumAcceleration ) )
			throw new ArgumentOutOfRangeException( nameof( settings ) );
	}
}
