namespace SkateMotion.Core.Animation;

/// <summary>Persistent mode, weight, pose and foot-contact state for one rig.</summary>
internal sealed class EffectorRigState
{
	internal EffectorWeightState[] Weights { get; }
	internal EffectorPoseState[] Poses { get; }
	internal FootReachState[] Feet { get; }
	internal EffectorRigSkeleton Skeleton { get; }
	internal FootReachFlags FootFlags { get; set; }

	internal EffectorRigState( EffectorWeightState[] weights, EffectorPoseState[] poses, FootReachState[] feet,
		EffectorRigSkeleton skeleton, FootReachFlags footFlags )
	{
		ArgumentNullException.ThrowIfNull( weights );
		ArgumentNullException.ThrowIfNull( poses );
		ArgumentNullException.ThrowIfNull( feet );
		ArgumentNullException.ThrowIfNull( skeleton.Bindings );
		ArgumentNullException.ThrowIfNull( skeleton.Parents );
		ArgumentNullException.ThrowIfNull( skeleton.BindTransforms );
		ArgumentNullException.ThrowIfNull( skeleton.AnimatedBones );
		ArgumentNullException.ThrowIfNull( skeleton.MutableBones );
		if ( weights.Length != 4 || poses.Length != 4 || feet.Length != 2 || skeleton.Bindings.Length != 4
			|| skeleton.Parents.Length != skeleton.AnimatedBones.Length
			|| skeleton.BindTransforms.Length != skeleton.AnimatedBones.Length
			|| skeleton.MutableBones.Length != skeleton.AnimatedBones.Length )
			throw new ArgumentException( "Rig state needs four effectors, two feet and matching skeleton arrays." );
		Weights = weights;
		Poses = poses;
		Feet = feet;
		Skeleton = skeleton;
		FootFlags = footFlags;
	}
}
