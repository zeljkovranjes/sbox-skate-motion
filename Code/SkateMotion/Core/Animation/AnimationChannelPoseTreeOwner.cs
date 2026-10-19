namespace SkateMotion.Core.Animation;

/// <summary>Uses the existing channel ownership lifecycle for complete body and auxiliary pose trees.</summary>
internal sealed class AnimationChannelPoseTreeOwner : IAnimationPosePlaybackNode
{
	private readonly AnimationChannelTreeOwner _owner;

	internal AnimationChannelPoseTreeOwner( IAnimationPlaybackTree currentTree,
		IAnimationChannelTreeFactory factory, AnimationChannelBlend blend )
	{
		_owner = new AnimationChannelTreeOwner( currentTree, factory, blend, true );
	}

	internal IAnimationPosePlaybackNode Root => (IAnimationPosePlaybackNode)_owner.Root;
	internal bool HasPending => _owner.HasPending;
	internal AnimationChannelReplaceResult Replace( AnimationChannelReplacement request ) => _owner.Replace( request );
	internal bool CleanupCompleted() => _owner.CleanupCompleted();
	internal void Restart( IAnimationPlaybackTree tree ) => _owner.Restart( tree );

	/// <summary>The promoted or pending composite's prepared pose.</summary>
	public SkeletonPose Pose => Root.Pose;
	/// <summary>The selected tree's duration.</summary>
	public float Duration => _owner.Duration;
	/// <summary>The selected tree's elapsed time.</summary>
	public float Elapsed => _owner.Elapsed;

	/// <summary>Prepares the currently published tree without promoting or releasing it.</summary>
	public bool Prepare( bool enabled, bool consumeHistory, uint flags ) => Root.Prepare( enabled, consumeHistory, flags );

	/// <summary>Samples the same published tree used for body preparation.</summary>
	public void SamplePoseStreams( AnimationPoseStreamContext context ) => Root.SamplePoseStreams( context );

	/// <summary>Advances playback independently from pending-tree cleanup.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) =>
		_owner.Advance( delta, phase, ref completion );

	/// <summary>Seeks the selected tree using its composite rules.</summary>
	public void Seek( float time ) => _owner.Seek( time );
}
