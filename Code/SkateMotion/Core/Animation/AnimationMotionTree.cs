namespace SkateMotion.Core.Animation;

/// <summary>A named authored motion with independent clocks and an explicit owner composition.</summary>
internal sealed class AnimationMotionTree : IAnimationPlaybackTree
{
	private readonly AnimationMotionTreeNode _motion;

	internal AnimationMotionTree( AnimationMotionTreeNode motion, AnimationBoneTransform[] reference )
		: this( motion, new AnimationCompositionPosePlayback( motion.Playback,
			new AnimationReferencePosePlayback( reference ), true ) )
	{
	}

	internal AnimationMotionTree( AnimationMotionTreeNode motion, IAnimationPosePlaybackNode root )
	{
		_motion = motion;
		PoseRoot = root ?? throw new ArgumentNullException( nameof( root ) );
	}

	internal IAnimationPosePlaybackNode PoseRoot { get; }
	public IAnimationPlaybackNode Root => PoseRoot;
	internal bool Update( ReadOnlyMemory<AnimationAttributeOverride> parameters ) => _motion.Update( parameters );
}
