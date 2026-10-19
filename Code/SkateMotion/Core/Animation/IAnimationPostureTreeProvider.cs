namespace SkateMotion.Core.Animation;

/// <summary>An authored posture mapping resolved before selecting a wrapper's mode.</summary>
internal interface IAnimationPostureTreeProvider
{
	IAnimationPosePlaybackNode Wrap( IAnimationPosePlaybackNode motion, int mode );
}
