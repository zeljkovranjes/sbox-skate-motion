namespace SkateMotion.Core.Animation;

/// <summary>A constant reference pose with inert timing and no auxiliary stream output.</summary>
internal sealed class AnimationReferencePosePlayback : IAnimationPosePlaybackNode, IAnimationRatePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly AnimationReferencePose _reference;

	internal AnimationReferencePosePlayback( AnimationBoneTransform[] reference ) =>
		_reference = new AnimationReferencePose( reference );

	/// <summary>The reusable reference transform buffer.</summary>
	public SkeletonPose Pose => _reference.Pose;
	/// <summary>Constant references have no playback duration.</summary>
	public float Duration => 0f;
	/// <summary>Constant references have no elapsed clock.</summary>
	public float Elapsed => 0f;

	/// <summary>A constant reference contributes no evaluated attributes.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags ) { }

	/// <summary>Reports no attribute and preserves the caller's result.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) => false;

	/// <summary>Restores reference transforms on each enabled preparation.</summary>
	public bool Prepare( bool enabled, bool consumeHistory, uint flags ) =>
		_reference.Prepare( enabled, consumeHistory, flags );

	/// <summary>Constant references do not emit or clear auxiliary commands.</summary>
	public void SamplePoseStreams( AnimationPoseStreamContext context ) { }

	/// <summary>Leaves caller completion fields unchanged because no reference clock advances.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) { }

	/// <summary>Seeking does not change a constant reference.</summary>
	public void Seek( float time ) { }

	/// <summary>Rate changes do not change a constant reference.</summary>
	public void SetRate( float rate ) { }
}
