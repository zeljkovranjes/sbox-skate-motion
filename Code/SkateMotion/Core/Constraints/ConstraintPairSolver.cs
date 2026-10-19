namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal static class ConstraintPairSolver
{
	internal static void SolveJoint( Span<SolverBody> bodies, ref JointConstraint joint )
	{
		var pair = joint.Pair;
		ref var a = ref bodies[ pair.BodyA ];
		ref var b = ref bodies[ pair.BodyB ];
		var candidate = Candidates( a, b, pair );
		var linear = ProjectJoint( candidate.Linear, joint.PositiveLinearBias, joint.NegativeLinearBias );
		var angular = ProjectJoint( candidate.Angular, joint.PositiveAngularBias, joint.NegativeAngularBias );
		Apply( ref a, ref b, ref pair, linear, angular );
		joint.Pair = pair;
	}

	internal static void SolveDrive( Span<SolverBody> bodies, ref DriveConstraint drive )
	{
		var pair = drive.Pair;
		ref var a = ref bodies[ pair.BodyA ];
		ref var b = ref bodies[ pair.BodyB ];
		var candidate = Candidates( a, b, pair );
		// Upper then lower projection also preserves signed limits from extrapolated poses.
		var linear = Vector3.Max( Vector3.Min( ConstraintVectorMath.MultiplyAdd( candidate.Linear, drive.LinearGain, drive.LinearBias ),
			drive.LinearLimit ), -drive.LinearLimit );
		var angular = Vector3.Max( Vector3.Min( ConstraintVectorMath.MultiplyAdd( candidate.Angular, drive.AngularGain, drive.AngularBias ),
			drive.AngularLimit ), -drive.AngularLimit );
		Apply( ref a, ref b, ref pair, linear, angular );
		drive.Pair = pair;
	}

	private static (Vector3 Linear, Vector3 Angular) Candidates(
		in SolverBody a, in SolverBody b, in ConstraintPair pair )
	{
		// Joint and drive rows use only the kinetic displacement channels.
		var relativeLinear = ConstraintVectorMath.CrossAdd( b.AngularVelocity, pair.OffsetB, b.LinearVelocity )
			- ConstraintVectorMath.CrossAdd( a.AngularVelocity, pair.OffsetA, a.LinearVelocity );
		var relativeAngular = b.AngularVelocity - a.AngularVelocity;
		return (ConstraintVectorMath.TransformAdd( relativeLinear, pair.LinearCoefficients, pair.LinearImpulse ),
			ConstraintVectorMath.TransformAdd( relativeAngular, pair.AngularCoefficients, pair.AngularImpulse ));
	}

	private static Vector3 ProjectJoint( Vector3 candidate, Vector3 positiveBias, Vector3 negativeBias )
	{
		return Vector3.Max( candidate + positiveBias, Vector3.Zero )
			+ Vector3.Min( candidate + negativeBias, Vector3.Zero );
	}

	private static void Apply( ref SolverBody a, ref SolverBody b, ref ConstraintPair pair,
		Vector3 linear, Vector3 angular )
	{
		var linearImpulse = ConstraintVectorMath.Transform( linear - pair.LinearImpulse, pair.LinearAxes );
		var angularImpulse = ConstraintVectorMath.Transform( angular - pair.AngularImpulse, pair.AngularAxes );
		pair.LinearImpulse = linear;
		pair.AngularImpulse = angular;
		a.LinearVelocity = ConstraintVectorMath.MultiplyAdd( linearImpulse, a.InverseMass, a.LinearVelocity );
		b.LinearVelocity = ConstraintVectorMath.MultiplyAdd( linearImpulse, -b.InverseMass, b.LinearVelocity );
		var torqueA = ConstraintVectorMath.CrossAdd( pair.OffsetA, linearImpulse, angularImpulse );
		var torqueB = ConstraintVectorMath.CrossAdd( pair.OffsetB, linearImpulse, angularImpulse );
		a.AngularVelocity = ConstraintVectorMath.TransformAdd( torqueA, pair.InverseInertiaA, a.AngularVelocity );
		b.AngularVelocity = ConstraintVectorMath.TransformAdd( -torqueB, pair.InverseInertiaB, b.AngularVelocity );
	}
}
