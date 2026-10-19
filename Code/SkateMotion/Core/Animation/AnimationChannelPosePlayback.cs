namespace SkateMotion.Core.Animation;

/// <summary>Shares a channel envelope between clock advancement, masked body poses and auxiliary streams.</summary>
internal sealed class AnimationChannelPosePlayback : AnimationPoseTreePlayback, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly AnimationChannelPoseControl _control;
	private readonly AnimationChannelBlend _blend;
	private readonly bool _evaluatedAttributes;

	internal AnimationChannelPosePlayback( IAnimationPosePlaybackNode baseNode, IAnimationPosePlaybackNode overlay,
		AnimationChannelBlend blend )
		: this( baseNode, overlay, blend, false )
	{
	}

	internal AnimationChannelPosePlayback( IAnimationPosePlaybackNode baseNode, IAnimationPosePlaybackNode overlay,
		AnimationChannelBlend blend, bool evaluatedAttributes )
		: base( new AnimationChannelPlayback( baseNode, overlay, blend ), new[] { baseNode, overlay } )
	{
		ArgumentNullException.ThrowIfNull( blend );
		_blend = blend;
		_evaluatedAttributes = evaluatedAttributes;
		_control = new AnimationChannelPoseControl( blend, Operations );
	}

	/// <summary>Publishes the enabled channel's ordered overlay-priority attribute union.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags )
	{
		if ( !_evaluatedAttributes )
			throw new NotSupportedException( "Bulk channel attributes require an explicitly bound evaluated owner." );
		if ( (_blend.State.Flags & 0x02000000) == 0 )
		{
			EvaluatedAttributeChild( 0 ).SampleEvaluatedAttributes( context, output, flags );
			return;
		}
		AnimationChannelAttributeProducer.Sample( EvaluatedAttributeChild( 0 ), EvaluatedAttributeChild( 1 ),
			context, output, flags );
	}

	protected override bool PreparePose( bool enabled ) => _control.Prepare( enabled );

	/// <summary>Queries the overlay first, falling back to the base only when it reports a miss.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		AttributeChild( 1 ).TryGetAuthoredAttribute( name, flags, ref result )
		|| AttributeChild( 0 ).TryGetAuthoredAttribute( name, flags, ref result );

	protected override void SampleStreams() => _control.Sample();
}
