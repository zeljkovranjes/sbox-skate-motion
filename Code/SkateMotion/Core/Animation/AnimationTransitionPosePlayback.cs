namespace SkateMotion.Core.Animation;

/// <summary>Shares a transition clock between body blending and auxiliary command selection.</summary>
internal sealed class AnimationTransitionPosePlayback : AnimationPoseTreePlayback, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly AnimationTransitionPoseControl _control;

	internal AnimationTransitionPosePlayback( IAnimationPosePlaybackNode previous, IAnimationPosePlaybackNode next,
		float blendDuration, AnimationTransitionMode mode, int blendMode )
		: base( new AnimationTransitionPlayback( previous, next, blendDuration, mode ), new[] { previous, next } )
	{
		Playback = (AnimationTransitionPlayback)Clock;
		_control = new AnimationTransitionPoseControl( Playback, Operations, blendMode );
	}

	internal AnimationTransitionPlayback Playback { get; }

	/// <summary>Emits records from the retained attribute child without advancing its clock.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags ) =>
		EvaluatedAttributeChild( 0 ).SampleEvaluatedAttributes( context, output, flags );

	/// <summary>Queries the outgoing child independently of the body transition weight.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		AttributeChild( 0 ).TryGetAuthoredAttribute( name, flags, ref result );

	internal void Reset( IAnimationPosePlaybackNode previous, IAnimationPosePlaybackNode next,
		float blendDuration, AnimationTransitionMode mode )
	{
		ReplacePair( previous, next );
		Playback.Reset( previous, next, blendDuration, mode );
	}

	protected override bool PreparePose( bool enabled ) => _control.Prepare( enabled );

	internal void Reset( IAnimationPosePlaybackNode previous, IAnimationPosePlaybackNode next,
		float blendDuration, AnimationTransitionMode mode, int blendMode )
	{
		Reset( previous, next, blendDuration, mode );
		_control.SetBlendMode( blendMode );
	}

	protected override void SampleStreams() => _control.Sample();
}
