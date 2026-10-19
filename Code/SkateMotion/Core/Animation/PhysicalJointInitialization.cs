namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Initializes both physical attachments from the canonical bind pose and body mapping.</summary>
internal static class PhysicalJointInitialization
{
	private const int BoneCount = 24;
	private const int ReferenceBone = 23;
	private const float VelocityScale = 59.999996185302734f;
	private const float PositionScale = 3599.99951171875f;

	internal static void Initialize( PhysicalJointState[] joints, LimbRigidTransform[] poses,
		int[] firstBodyBones, PhysicalJointPreset[] presets )
	{
		ArgumentNullException.ThrowIfNull( joints );
		ArgumentNullException.ThrowIfNull( poses );
		ArgumentNullException.ThrowIfNull( firstBodyBones );
		ArgumentNullException.ThrowIfNull( presets );
		if ( joints.Length != BoneCount || poses.Length != BoneCount || firstBodyBones.Length != BoneCount || presets.Length != 2 )
			throw new ArgumentException( "Initialization requires the full canonical pose, joint storage, body mapping and two presets." );
		for ( var childBone = 0; childBone < BoneCount; ++childBone )
		{
			if ( firstBodyBones[ childBone ] < -1 || firstBodyBones[ childBone ] >= BoneCount )
				throw new ArgumentOutOfRangeException( nameof( firstBodyBones ) );
			if ( firstBodyBones[ childBone ] >= 0 )
				ArgumentNullException.ThrowIfNull( joints[ childBone ] );
		}

		for ( var childBone = 0; childBone < BoneCount; ++childBone )
		{
			var firstBodyBone = firstBodyBones[ childBone ];
			if ( firstBodyBone < 0 )
				continue;
			var joint = joints[ childBone ];
			var child = poses[ childBone ].RigidInverse();
			var anchor = poses[ childBone ].Translation;
			joint.ChildBone = childBone;
			joint.FirstBodyBone = firstBodyBone;
			joint.SecondBodyBone = ReferenceBone;
			joint.FirstEnabled = true;
			joint.SecondEnabled = true;
			joint.FirstFrame = Frame( child, poses[ firstBodyBone ].RigidInverse(), anchor );
			joint.SecondFrame = Frame( child, poses[ ReferenceBone ].RigidInverse(), anchor );
			// The first attachment starts with angular drive only. The reference attachment
			// drives both channels; its initialization leaves the joint in mode zero.
			joint.FirstConstraint = new( 0f, 0f, 0f, 2 );
			joint.SecondConstraint = AngularDrive( presets[ 0 ] );
			joint.ThirdConstraint = new( presets[ 1 ].FirstVelocity * VelocityScale,
				0f, presets[ 1 ].FirstPosition * PositionScale, 2 );
			joint.FourthConstraint = AngularDrive( presets[ 1 ] );
			joint.Mode = 0;
			joint.Dirty = false;
		}
	}

	private static PhysicalJointDrive AngularDrive( PhysicalJointPreset preset ) => new(
		preset.SecondVelocity * VelocityScale, 0f, preset.SecondPosition * PositionScale, 2 );

	private static PhysicalJointFrame Frame( LimbRigidTransform child, LimbRigidTransform body,
		Vector3 anchor ) => new( PhysicalPoseOutput.Rotation( child.Rotation ),
		child.Transform( anchor ), PhysicalPoseOutput.Rotation( body.Rotation ), body.Transform( anchor ) );
}
