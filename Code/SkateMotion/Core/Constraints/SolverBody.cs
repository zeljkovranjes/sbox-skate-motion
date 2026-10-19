namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal struct SolverBody
{
	// Despite the names, the body update adds the linear channels directly to
	// position: they are solver-space
	// displacements, not SI velocities. Do not assign native Rigidbody velocity here.
	internal float InverseMass { get; set; }
	internal Vector3 LinearVelocity { get; set; }
	internal Vector3 AngularVelocity { get; set; }
	internal Vector3 CorrectionVelocity { get; set; }
	internal Vector3 CorrectionAngularVelocity { get; set; }

	internal readonly Vector3 PointVelocity( Vector3 offset ) => LinearVelocity
		+ Vector3.Cross( AngularVelocity, offset );

	internal readonly Vector3 PointCorrection( Vector3 offset ) => CorrectionVelocity
		+ Vector3.Cross( CorrectionAngularVelocity, offset );
}
