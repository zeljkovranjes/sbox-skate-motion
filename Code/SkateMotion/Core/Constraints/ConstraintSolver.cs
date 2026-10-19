namespace SkateMotion.Core.Constraints;

internal static class ConstraintSolver
{
	internal static void Solve( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives, int passes )
	{
		SolveCore( bodies, contacts, joints, drives, passes, false );
	}

	internal static void SolveGenerated( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives, int passes )
	{
		SolveCore( bodies, contacts, joints, drives, passes, true );
	}

	private static void SolveCore( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives, int passes, bool generated )
	{
		if ( passes < 0 || passes > 256 )
			throw new ArgumentOutOfRangeException( nameof( passes ) );

		Validate( bodies.Length, contacts, joints, drives );
		for ( var pass = 0; pass < passes; pass++ )
			SolvePassCore( bodies, contacts, joints, drives, generated ? contacts.Length : 0 );
	}

	internal static void Validate( int bodyCount, ReadOnlySpan<ContactConstraint> contacts,
		ReadOnlySpan<JointConstraint> joints, ReadOnlySpan<DriveConstraint> drives )
	{
		foreach ( ref readonly var contact in contacts )
			ValidatePair( contact.BodyA, contact.BodyB, bodyCount );
		foreach ( ref readonly var joint in joints )
			ValidatePair( joint.Pair.BodyA, joint.Pair.BodyB, bodyCount );
		foreach ( ref readonly var drive in drives )
			ValidatePair( drive.Pair.BodyA, drive.Pair.BodyB, bodyCount );
	}

	/// <summary>One pass over already validated constraints.</summary>
	internal static void SolvePass( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives )
	{
		SolvePassCore( bodies, contacts, joints, drives, 0 );
	}

	internal static void SolveGeneratedPass( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives )
	{
		SolvePassCore( bodies, contacts, joints, drives, contacts.Length );
	}

	internal static void SolveGeneratedPass( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives, int generatedContactCount )
	{
		if ( (uint)generatedContactCount > (uint)contacts.Length )
			throw new ArgumentOutOfRangeException( nameof( generatedContactCount ) );
		SolvePassCore( bodies, contacts, joints, drives, generatedContactCount );
	}

	private static void SolvePassCore( Span<SolverBody> bodies, Span<ContactConstraint> contacts,
		Span<JointConstraint> joints, Span<DriveConstraint> drives, int generatedContactCount )
	{
		// Each pass visits all contacts, then all joints, then all drives.
		for ( var i = 0; i < contacts.Length; ++i )
		{
			ref var contact = ref contacts[ i ];
			if ( i < generatedContactCount )
				ContactSolver.SolveGeneratedContact( ref bodies[ contact.BodyA ], ref bodies[ contact.BodyB ], ref contact );
			else
				ContactSolver.SolveContact( ref bodies[ contact.BodyA ], ref bodies[ contact.BodyB ], ref contact );
		}
		foreach ( ref var joint in joints )
			ConstraintPairSolver.SolveJoint( bodies, ref joint );
		foreach ( ref var drive in drives )
			ConstraintPairSolver.SolveDrive( bodies, ref drive );
	}

	private static void ValidatePair( int a, int b, int count )
	{
		if ( (uint)a >= (uint)count || (uint)b >= (uint)count || a == b )
			throw new ArgumentException( "A constraint must identify two distinct bodies in the workspace." );
	}
}
