namespace SkateMotion.Core.Animation;

/// <summary>A playback node with immediate body preparation and separate auxiliary stream traversal.</summary>
internal interface IAnimationPosePlaybackNode : IAnimationPlaybackNode, IAnimationPreparedPoseNode
{
	void SamplePoseStreams( AnimationPoseStreamContext context );
}
