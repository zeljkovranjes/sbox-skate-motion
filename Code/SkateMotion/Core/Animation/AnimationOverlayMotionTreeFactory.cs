namespace SkateMotion.Core.Animation;

/// <summary>Connects channel storage to authored motion construction and the actor's retained wrappers.</summary>
internal sealed class AnimationOverlayMotionTreeFactory : IAnimationOverlayTreeFactory, IAnimationAttributeOverrideConsumer
{
	private readonly AnimationMotionTreeFactory _motions;
	private readonly AnimationOwnerTreeComposition _composition;

	internal AnimationOverlayMotionTreeFactory( AnimationMotionTreeFactory motions,
		AnimationOwnerTreeComposition composition )
	{
		_motions = motions ?? throw new ArgumentNullException( nameof( motions ) );
		_composition = composition ?? throw new ArgumentNullException( nameof( composition ) );
	}

	/// <summary>Creates a fresh rated motion and applies owner wrappers before it enters the channel.</summary>
	public IAnimationPlaybackTree Create( int storage, AnimationChannelReplacement request ) =>
		_motions.CreateComposed( storage, request.Definition, request.Rate,
			root => _composition.Compose( root, request.UsePosture ) );

	/// <summary>Releases the matching retained tree so later overrides cannot update retired motion nodes.</summary>
	public void Release( int storage, IAnimationPlaybackTree tree ) => _motions.Release( storage, tree );

	/// <summary>Updates active and pending motions together without changing their clocks.</summary>
	public bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> attributes ) =>
		_motions.UpdateOverrides( attributes );
}
