namespace SkateMotion.Core.Animation;

/// <summary>Constructs base motions with the retained indexed reference and owner posture wrappers.</summary>
internal sealed class AnimationBaseMotionTreeFactory : IAnimationBasePoseTreeFactory, IAnimationAttributeOverrideConsumer
{
	private readonly AnimationMotionTreeFactory _motions;
	private readonly AnimationIndexedReferenceState _indexed;
	private readonly AnimationOwnerTreeComposition _composition;

	internal AnimationBaseMotionTreeFactory( AnimationMotionTreeFactory motions,
		AnimationIndexedReferenceState indexed, AnimationOwnerTreeComposition composition )
	{
		_motions = motions ?? throw new ArgumentNullException( nameof( motions ) );
		_indexed = indexed ?? throw new ArgumentNullException( nameof( indexed ) );
		_composition = composition ?? throw new ArgumentNullException( nameof( composition ) );
	}

	/// <summary>Wraps the authored root before the base owner applies its requested rate and start time.</summary>
	public IAnimationPlaybackTree Create( int storageSlot, int definition ) =>
		_motions.CreateComposed( storageSlot, definition, null, Compose );

	private IAnimationPosePlaybackNode Compose( IAnimationPosePlaybackNode motion ) =>
		_composition.Compose( _indexed.Compose( motion ), false );

	/// <summary>Publishes the retained base owner root through the motion factory's existing receiver.</summary>
	public void Publish( IAnimationPosePlaybackNode root ) => _motions.Publish( root );

	/// <summary>Preserves parameter updates for all retained outgoing and incoming motions.</summary>
	public bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> attributes ) =>
		_motions.UpdateOverrides( attributes );
}
