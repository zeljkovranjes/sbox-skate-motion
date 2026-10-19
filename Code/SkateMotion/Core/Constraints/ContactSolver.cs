namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

internal static class ContactSolver
{
	internal static void Solve( Span<SolverBody> bodies, Span<ContactConstraint> contacts, int passes )
	{
		ConstraintSolver.Solve( bodies, contacts, Span<JointConstraint>.Empty, Span<DriveConstraint>.Empty, passes );
	}

	internal static void SolveContact( ref SolverBody a, ref SolverBody b, ref ContactConstraint contact )
	{
		SolveContactCore( ref a, ref b, ref contact, false );
	}

	internal static void SolveGeneratedContact( ref SolverBody a, ref SolverBody b, ref ContactConstraint contact )
	{
		SolveContactCore( ref a, ref b, ref contact, true );
	}

	private static void SolveContactCore( ref SolverBody a, ref SolverBody b,
		ref ContactConstraint contact, bool generated )
	{
		var relative = b.PointVelocity( contact.OffsetB ) - a.PointVelocity( contact.OffsetA );
		var correction = b.PointCorrection( contact.OffsetB ) - a.PointCorrection( contact.OffsetA );
		var combined = relative + correction;
		var candidate = contact.Impulse + contact.Bias;
		candidate += new Vector4( contact.CoefficientX, contact.CoefficientX.X )
			* new Vector4( combined.X, combined.X, combined.X, correction.X );
		candidate += new Vector4( contact.CoefficientY, contact.CoefficientY.X )
			* new Vector4( combined.Y, combined.Y, combined.Y, correction.Y );
		candidate += new Vector4( contact.CoefficientZ, contact.CoefficientZ.X )
			* new Vector4( combined.Z, combined.Z, combined.Z, correction.Z );

		var projected = generated
			? ContactImpulse.ProjectGenerated( contact.Impulse, candidate, contact.StaticFriction, contact.SlidingFriction )
			: ContactImpulse.Project( contact.Impulse, candidate, contact.StaticFriction, contact.SlidingFriction );
		var delta = projected - contact.Impulse;
		contact.Impulse = projected;
		var inverseMassA = generated ? contact.InverseMassA : a.InverseMass;
		var inverseMassB = generated ? contact.InverseMassB : b.InverseMass;

		var linearImpulse = contact.Normal * delta.X + contact.TangentA * delta.Y + contact.TangentB * delta.Z;
		a.LinearVelocity += linearImpulse * inverseMassA;
		b.LinearVelocity -= linearImpulse * inverseMassB;
		a.AngularVelocity += contact.NormalAngularA * delta.X
			+ contact.TangentAAngularA * delta.Y + contact.TangentBAngularA * delta.Z;
		b.AngularVelocity -= contact.NormalAngularB * delta.X
			+ contact.TangentAAngularB * delta.Y + contact.TangentBAngularB * delta.Z;

		var correctionImpulse = contact.Normal * delta.W;
		a.CorrectionVelocity += correctionImpulse * inverseMassA;
		b.CorrectionVelocity -= correctionImpulse * inverseMassB;
		a.CorrectionAngularVelocity += contact.NormalAngularA * delta.W;
		b.CorrectionAngularVelocity -= contact.NormalAngularB * delta.W;
	}
}
