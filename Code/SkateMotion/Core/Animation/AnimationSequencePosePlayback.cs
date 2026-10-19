namespace SkateMotion.Core.Animation;

/// <summary>Uses one sequence state for clock handoff, body selection and auxiliary streams.</summary>
internal sealed class AnimationSequencePosePlayback : AnimationPoseTreePlayback, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly AnimationSequencePoseControl _control;

	internal AnimationSequencePosePlayback( IAnimationPosePlaybackNode previous, IAnimationPosePlaybackNode next )
		: base( new AnimationSequencePlayback( previous, next ), new[] { previous, next } )
	{
		Playback = (AnimationSequencePlayback)Clock;
		_control = new AnimationSequencePoseControl( Playback, Operations );
	}

	internal AnimationSequencePlayback Playback { get; }

	/// <summary>Emits records from the retained attribute child without advancing its clock.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags ) =>
		EvaluatedAttributeChild( Playback.ActiveNext ? 1 : 0 ).SampleEvaluatedAttributes( context, output, flags );

	/// <summary>Queries only the child selected by the retained sequence handoff state.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		AttributeChild( Playback.ActiveNext ? 1 : 0 ).TryGetAuthoredAttribute( name, flags, ref result );

	internal void Reset( IAnimationPosePlaybackNode previous, IAnimationPosePlaybackNode next )
	{
		ReplacePair( previous, next );
		Playback.Reset( previous, next );
	}

	protected override bool PreparePose( bool enabled ) => _control.Prepare( enabled );
	protected override void SampleStreams() => _control.Sample();
}
