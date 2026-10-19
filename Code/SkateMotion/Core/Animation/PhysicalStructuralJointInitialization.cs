namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Constructs structural joints from canonical authored records and bind poses.</summary>
internal static class PhysicalStructuralJointInitialization
{
	private const int BoneCount = 24;
	private const int JointCapacity = 22;
	private const float VelocityScale = 59.999996185302734f;

	internal static PhysicalStructuralJoint[] Build( PhysicalStructuralBone[] bones,
		LimbRigidTransform[] poses, int[] parents, int[] canonicalToClip, int[] clipToCanonical,
		PhysicalStructuralJointOverride[] overrides, PhysicalStructuralJointTuning tuning, LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( bones );
		ArgumentNullException.ThrowIfNull( poses );
		ArgumentNullException.ThrowIfNull( parents );
		ArgumentNullException.ThrowIfNull( canonicalToClip );
		ArgumentNullException.ThrowIfNull( clipToCanonical );
		ArgumentNullException.ThrowIfNull( overrides );
		ArgumentNullException.ThrowIfNull( math );
		if ( bones.Length != BoneCount || poses.Length != BoneCount
			|| parents.Length != BoneCount || canonicalToClip.Length != BoneCount )
			throw new ArgumentException( "Structural construction requires the full canonical skeleton." );

		var count = 0;
		for ( var child = 0; child < BoneCount; ++child )
		{
			var parent = parents[ child ];
			if ( parent < -1 || parent >= BoneCount )
				throw new ArgumentOutOfRangeException( nameof( parents ) );
			if ( parent == -1 )
				continue;
			var clip = canonicalToClip[ parent ];
			if ( clip < -1 || clip >= clipToCanonical.Length )
				throw new ArgumentOutOfRangeException( nameof( canonicalToClip ) );
			if ( clip == -1 )
				continue;
			if ( clipToCanonical[ clip ] < 0 || clipToCanonical[ clip ] >= BoneCount )
				throw new ArgumentOutOfRangeException( nameof( clipToCanonical ) );
			++count;
		}
		if ( count > JointCapacity || overrides.Length < count )
			throw new ArgumentException( "Retained structural joints require matching overrides and at most 22 slots." );

		var result = new PhysicalStructuralJoint[ count ];
		var index = 0;
		for ( var child = 0; child < BoneCount; ++child )
		{
			var parent = parents[ child ];
			if ( parent == -1 || canonicalToClip[ parent ] == -1 )
				continue;
			var frame = BuildFrame( bones[ child ], bones[ parent ], poses[ child ], poses[ parent ] );
			result[ index ] = new( clipToCanonical[ canonicalToClip[ parent ] ], child, frame,
				new Vector4( 0f, 0f, 0f, 1f ), Limits( bones[ child ], overrides[ index ], tuning, math ) );
			++index;
		}
		return result;
	}

	private static PhysicalJointFrame BuildFrame( PhysicalStructuralBone child, PhysicalStructuralBone parent,
		LimbRigidTransform childPose, LimbRigidTransform parentPose )
	{
		var childBody = new LimbRigidTransform( Rotation( child.BodyRotation ), child.BodyPosition ).RigidInverse();
		var parentBody = new LimbRigidTransform( Rotation( parent.BodyRotation ), parent.BodyPosition ).RigidInverse();
		var joint = new LimbRigidTransform( Rotation( child.JointRotation ), Vector3.Zero );
		var parentToBone = new LimbRigidTransform( Rotation( child.ParentToBoneRotation ), Vector3.Zero ).RigidInverse();
		var childFrame = childBody.Compose( joint );
		var parentFrame = parentBody.Compose( parentToBone ).Compose( joint );
		var anchor = parentPose.RigidInverse().Transform( childPose.Translation );
		return new( PhysicalPoseOutput.Rotation( childFrame.Rotation ), childBody.Translation,
			PhysicalPoseOutput.Rotation( parentFrame.Rotation ), parentBody.Transform( anchor ) );
	}

	private static PhysicalStructuralJointLimits Limits( PhysicalStructuralBone bone,
		PhysicalStructuralJointOverride settings, PhysicalStructuralJointTuning tuning, LimbSolverMath math )
	{
		var swing = settings.Locked ? 0f : MathF.Max( .01f, bone.SwingAngle * settings.SwingScale );
		var twist = settings.Locked ? 0f : MathF.Max( .01f, bone.TwistAngle * settings.TwistScale );
		return new( tuning.LinearSpeed * VelocityScale, tuning.SwingSpeed * VelocityScale,
			tuning.TwistSpeed * VelocityScale, swing, twist,
			settings.Locked ? 1f : math.SinCos( swing ).Cosine,
			settings.Locked ? 1f : math.SinCos( twist ).Cosine,
			settings.Locked || tuning.LockSwing ? 4 : 1, settings.Locked || tuning.LockTwist ? 2 : 1 );
	}

	private static LimbFrame Rotation( Vector4 orientation )
	{
		var scaled = orientation * 1.4142135381698608f;
		var xx = SubtractProduct( scaled.X, scaled.X, .5f );
		var yy = SubtractProduct( scaled.Y, scaled.Y, .5f );
		var zz = SubtractProduct( scaled.Z, scaled.Z, .5f );
		var xy = MultiplyAdd( scaled.X, scaled.Y, 0f );
		var xz = MultiplyAdd( scaled.X, scaled.Z, 0f );
		var yz = MultiplyAdd( scaled.Y, scaled.Z, 0f );
		var wx = MultiplyAdd( scaled.W, scaled.X, 0f );
		var wy = MultiplyAdd( scaled.W, scaled.Y, 0f );
		var wz = MultiplyAdd( scaled.W, scaled.Z, 0f );
		return new( new( yy + zz, xy + wz, xz - wy ),
			new( xy - wz, xx + zz, yz + wx ), new( xz + wy, yz - wx, xx + yy ) );
	}
}
