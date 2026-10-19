namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Shared pose, physical bindings and retained state across complete rig updates.</summary>
internal sealed class PhysicalRigState
{
	internal EffectorRigState Rig { get; }
	internal EffectorPoseCache Cache { get; }
	internal PhysicalPosePreparationState Preparation { get; }
	internal PhysicalPoseResetState Reset { get; }
	internal PhysicalRigFeedbackState FeedbackState { get; set; }
	internal PhysicalBodyBinding[] Targets { get; }
	internal PhysicalBodyBinding[] Bodies { get; }
	internal PhysicalBodyBinding[] Feedback { get; }
	internal PhysicalJointState[] Joints { get; }
	internal PhysicalJointPreset[] Presets { get; }
	internal LimbRigidTransform[] Inverses { get; }
	internal EffectorRigFrameSet Frames { get; set; }
	internal PhysicalJointDrive RootDrive { get; set; }
	internal Vector3 PreviousPosition { get; set; }
	internal Vector3 Velocity { get; set; }
	internal Vector3 WeightAnchor { get; set; }
	internal byte ResetFlag { get; set; }

	internal PhysicalRigState( EffectorRigState rig, EffectorPoseCache cache,
		PhysicalPosePreparationState preparation, PhysicalPoseResetState reset,
		PhysicalBodyBinding[] targets, PhysicalBodyBinding[] bodies,
		PhysicalJointState[] joints, PhysicalJointPreset[] presets )
	{
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( cache );
		ArgumentNullException.ThrowIfNull( preparation );
		ArgumentNullException.ThrowIfNull( reset );
		ArgumentNullException.ThrowIfNull( targets );
		ArgumentNullException.ThrowIfNull( bodies );
		ArgumentNullException.ThrowIfNull( joints );
		ArgumentNullException.ThrowIfNull( presets );
		var count = rig.Skeleton.MutableBones.Length;
		if ( targets.Length != 4 || bodies.Length != count + 2 || cache.Bones.Length != count
			|| reset.Poses.Length != bodies.Length || presets.Length != 2 )
			throw new ArgumentException( "Rig poses, physical bindings and cache sizes must agree." );

		Rig = rig;
		Cache = cache;
		Preparation = preparation;
		Reset = reset;
		Targets = targets;
		Bodies = bodies;
		Feedback = new[] { bodies[ ^2 ], bodies[ ^1 ] };
		Joints = joints;
		Presets = presets;
		Inverses = new LimbRigidTransform[ count ];
	}
}
