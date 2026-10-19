namespace SkateMotion.Core.Animation;

/// <summary>Composes fresh child poses while retaining the active child's clock and auxiliary streams.</summary>
internal sealed class AnimationCompositionPosePlayback : AnimationPoseTreePlayback, IAnimationRatePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly AnimationCompositionPlayback _playback;
	private readonly AnimationCompositionPoseControl _control;
	private readonly int _activeChild;

	internal AnimationCompositionPosePlayback( IAnimationPosePlaybackNode first,
		IAnimationPosePlaybackNode second, bool firstActive )
		: base( CreateClock( first, second, firstActive ), new[] { first, second } )
	{
		_playback = (AnimationCompositionPlayback)Clock;
		_control = new AnimationCompositionPoseControl( Operations, firstActive );
		_activeChild = firstActive ? 0 : 1;
	}

	/// <summary>Emits records from the retained attribute child without advancing its clock.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags ) =>
		EvaluatedAttributeChild( _activeChild ).SampleEvaluatedAttributes( context, output, flags );

	/// <summary>Queries the same active child that owns the composition clock.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		AttributeChild( _activeChild ).TryGetAuthoredAttribute( name, flags, ref result );

	/// <summary>Forwards rate changes in child storage order through the retained composition clock.</summary>
	public void SetRate( float rate ) => _playback.SetRate( rate );

	protected override bool PreparePose( bool enabled ) => _control.Prepare( enabled );
	protected override void SampleStreams() => _control.Sample();

	private static AnimationCompositionPlayback CreateClock( IAnimationPosePlaybackNode first,
		IAnimationPosePlaybackNode second, bool firstActive )
	{
		if ( first is not IAnimationRatePlaybackNode firstClock || second is not IAnimationRatePlaybackNode secondClock )
			throw new ArgumentException( "Composition children need rate control." );
		return new AnimationCompositionPlayback( firstClock, secondClock, firstActive );
	}
}
