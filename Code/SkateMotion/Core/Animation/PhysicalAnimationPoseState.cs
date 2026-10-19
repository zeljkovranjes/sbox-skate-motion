namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Shared animation poses and retained motion publication around board attachment.</summary>
internal sealed class PhysicalAnimationPoseState
{
	internal EffectorRigSkeleton Skeleton { get; }
	internal EffectorPoseCache Cache { get; }
	internal PhysicalAnimationAttributeState Attributes { get; }
	internal LimbRigidTransform[] Authored { get; }
	internal LimbRigidTransform RelativeBodyFrame { get; set; }
	internal LimbRigidTransform ReferenceFrame { get; set; }
	internal LimbRigidTransform InverseReferenceFrame { get; set; }
	internal LimbRigidTransform GatheredBoard { get; set; }
	internal LimbRigidTransform CachedBoard { get; set; }
	internal LimbRigidTransform CachedRoot { get; set; }
	internal Vector3 BoardVelocity { get; set; }
	internal Vector3 MotionVelocity { get; set; }
	internal float DeltaTime { get; set; }
	internal float PreviousBoardAxisY { get; set; }
	internal float BoardAxisDelta { get; set; }
	internal Vector4 PublishedExternalMotion { get; set; }
	internal Vector3 PublishedRootOffset { get; set; }
	internal Vector3 PublishedRootOffsetDelta { get; set; }

	internal PhysicalAnimationPoseState( EffectorRigSkeleton skeleton, EffectorPoseCache cache,
		PhysicalAnimationAttributeState attributes, LimbRigidTransform[] authored )
	{
		ArgumentNullException.ThrowIfNull( cache );
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( authored );
		if ( skeleton.MutableBones.Length != 24 || skeleton.AnimatedBones.Length != 24
			|| skeleton.BindTransforms.Length != 24 || cache.Bones.Length != 24 || authored.Length != 4 )
			throw new ArgumentException( "Animation input requires twenty-four solver bones and four authored targets." );
		Skeleton = skeleton;
		Cache = cache;
		Attributes = attributes;
		Authored = authored;
	}
}
