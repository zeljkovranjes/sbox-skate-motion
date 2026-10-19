namespace SkateMotion.Core.Animation;

/// <summary>Binds the owner's authored body and attribute maps to each requested posture wrapper.</summary>
internal sealed class AnimationPostureTreeProvider : IAnimationPostureTreeProvider
{
	private readonly AnimationPostureBoneMap _bones;
	private readonly AnimationPostureAttributeMap _attributes;

	internal AnimationPostureTreeProvider( AnimationPostureBoneMap bones, AnimationPostureAttributeMap attributes )
	{
		_bones = bones ?? throw new ArgumentNullException( nameof( bones ) );
		_attributes = attributes ?? throw new ArgumentNullException( nameof( attributes ) );
	}

	public IAnimationPosePlaybackNode Wrap( IAnimationPosePlaybackNode motion, int mode ) =>
		new AnimationPosturePosePlayback( motion, _bones, _attributes, mode );
}
