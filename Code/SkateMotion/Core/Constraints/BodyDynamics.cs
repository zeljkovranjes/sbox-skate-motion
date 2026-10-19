namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal static class BodyDynamics
{
	internal static void Step( ref DynamicBody body, ref SolverBody workspace,
		in BodyDynamicsSettings settings, float step, Vector3 gravity )
	{
		if ( !float.IsFinite( step ) || step <= 0f )
			throw new ArgumentOutOfRangeException( nameof( step ) );

		var displacement = ConstraintVectorMath.MultiplyAdd(
			ConstraintVectorMath.MultiplyAdd( body.Acceleration, step, body.Velocity ), step, workspace.LinearVelocity );
		var angularDisplacement = ConstraintVectorMath.MultiplyAdd(
			ConstraintVectorMath.MultiplyAdd( body.AngularAcceleration, step, body.AngularVelocity ), step, workspace.AngularVelocity );
		body.Position = (body.Position + workspace.CorrectionVelocity) + displacement;
		body.Rotation = IntegrateRotation( body.Rotation, angularDisplacement + workspace.CorrectionAngularVelocity );
		var basis = QuaternionFrame.Rotation( body.Rotation );
		body.RotationBasis = basis;
		body.WorldInverseInertia = RotateInertia( settings.LocalInverseInertia, basis );

		// Separation changes the pose but must not add kinetic velocity.
		var inverseStep = ConstraintReciprocal.Inverse( step );
		var velocity = displacement * Math.Max( inverseStep - settings.LinearDamping, 0f );
		var angularVelocity = angularDisplacement * Math.Max( inverseStep - settings.AngularDamping, 0f );
		var linearSquared = Math.Min( LengthSquared( velocity ), settings.MaximumSpeed * settings.MaximumSpeed );
		var angularSquared = Math.Min( LengthSquared( angularVelocity ),
			settings.MaximumAngularSpeed * settings.MaximumAngularSpeed );
		var metric = MathF.FusedMultiplyAdd( angularSquared,
			settings.AngularMotionWeight * body.InverseMass, linearSquared );
		body.Velocity = Limit( velocity, settings.MaximumSpeed );
		body.AngularVelocity = Limit( angularVelocity, settings.MaximumAngularSpeed );
		if ( metric < settings.QuietThreshold )
		{
			if ( metric <= body.MotionMetric )
				body.QuietSteps++;
			body.QuietSteps = Math.Min( body.QuietSteps, settings.MaximumQuietSteps );
		}
		else
		{
			body.QuietSteps = 0;
		}
		body.MotionMetric = metric;
		body.Acceleration = gravity;
		body.AngularAcceleration = Vector3.Zero;
		workspace = new SolverBody { InverseMass = workspace.InverseMass };
	}

	private static Vector3 Limit( Vector3 value, float maximum )
	{
		var squared = LengthSquared( value );
		var maximumSquared = maximum * maximum;
		if ( squared <= maximumSquared )
			return value;
		var estimate = 1f / squared;
		var initial = maximumSquared * estimate;
		var ratio = MathF.FusedMultiplyAdd( MathF.FusedMultiplyAdd( -initial, squared, maximumSquared ), estimate, initial );
		var scale = ratio == 0f ? 0f : ratio * ConstraintReciprocal.InverseSquareRoot( ratio );
		return value * scale;
	}

	private static float LengthSquared( Vector3 value )
	{
		var product = value * value;
		return (product.X + product.Y) + product.Z;
	}

	private static Quaternion IntegrateRotation( Quaternion rotation, Vector3 displacement )
	{
		var vector = new Vector3( rotation.X, rotation.Y, rotation.Z );
		var delta = ConstraintVectorMath.MultiplyAdd( displacement, rotation.W,
			ConstraintVectorMath.Cross( displacement, vector ) );
		var product = displacement * vector;
		var dot = (product.X + product.Y) + product.Z;
		var next = new Quaternion( vector + delta * .5f, rotation.W + dot * -.5f );
		var squared = (next.X * next.X + next.Z * next.Z) + (next.Y * next.Y + next.W * next.W);
		return next * ConstraintReciprocal.InverseSquareRoot( squared );
	}

	private static Matrix4x4 RotateInertia( Vector3 diagonal, Matrix4x4 basis )
	{
		var x = new Vector3( basis.M11, basis.M12, basis.M13 );
		var y = new Vector3( basis.M21, basis.M22, basis.M23 );
		var z = new Vector3( basis.M31, basis.M32, basis.M33 );
		var weightedX = x * diagonal.X;
		var weightedY = y * diagonal.Y;
		var weightedZ = z * diagonal.Z;
		// The compact symmetric tensor starts with the Y contribution, then fuses X and Z.
		var first = ConstraintVectorMath.MultiplyAdd( weightedZ, z.X,
			ConstraintVectorMath.MultiplyAdd( weightedX, x.X, weightedY * y.X ) );
		var second = ConstraintVectorMath.MultiplyAdd( weightedZ, z.Y,
			ConstraintVectorMath.MultiplyAdd( weightedX, x.Y, weightedY * y.Y ) );
		var last = MathF.FusedMultiplyAdd( weightedZ.Z, z.Z,
			MathF.FusedMultiplyAdd( weightedX.Z, x.Z, weightedY.Z * y.Z ) );
		return new Matrix4x4(
			first.X, first.Y, first.Z, 0f,
			first.Y, second.Y, second.Z, 0f,
			first.Z, second.Z, last, 0f,
			0f, 0f, 0f, 1f );
	}
}
