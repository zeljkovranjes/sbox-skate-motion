namespace SkateMotion.Core.Animation;

/// <summary>Immediate pose preparation; attribute traversal is a separate operation.</summary>
internal interface IAnimationPreparedPoseNode
{
	SkeletonPose Pose { get; }
	bool Prepare( bool enabled, bool consumeHistory, uint flags );
}
