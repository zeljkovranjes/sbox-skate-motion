namespace SkateMotion.Core.Animation;

/// <summary>Uses the selected directional pair for clock updates, body preparation and auxiliary streams.</summary>
internal sealed class AnimationDirectionalPosePlayback : AnimationPoseTreePlayback, IAnimationRatePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly AnimationDirectionalPoseControl _control;

	internal AnimationDirectionalPosePlayback( IAnimationPosePlaybackNode[] children, float[] samples )
		: base( CreateClock( children, samples ), children )
	{
		Playback = (AnimationDirectionalPlayback)Clock;
		_control = new AnimationDirectionalPoseControl( Operations );
	}

	internal AnimationDirectionalPlayback Playback { get; }
	internal float PoseThreshold { get; set; }

	/// <summary>Emits the selected pair's ordered attribute intersection.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags ) =>
		AnimationDirectionalAttributeProducer.Sample( EvaluatedAttributeChild( Playback.FirstIndex ),
			EvaluatedAttributeChild( Playback.SecondIndex ), Playback.Weight, PoseThreshold, context, output, flags );

	/// <summary>Queries the retained pair with attribute-specific endpoint and filter behavior.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		AnimationDirectionalAttributeQuery.TryGet( AttributeChild( Playback.FirstIndex ), AttributeChild( Playback.SecondIndex ),
			Playback.Weight, PoseThreshold, name, flags, ref result );

	internal bool UpdateParameters( IAnimationDirectionalParameters parameters,
		float value, bool normalized, bool present = true ) =>
		Playback.UpdateParameters( parameters, value, normalized, present );

	/// <summary>Updates every child's rate through the same directional clock used for sampling.</summary>
	public void SetRate( float rate ) => Playback.SetRate( rate );

	protected override bool PreparePose( bool enabled ) => _control.Prepare(
		Playback.FirstIndex, Playback.SecondIndex, Playback.Weight, PoseThreshold,
		Playback.FirstIsEndpoint, Playback.SecondIsEndpoint, enabled );

	protected override void SampleStreams() => _control.Sample(
		Playback.FirstIndex, Playback.SecondIndex, Playback.Weight,
		Playback.FirstIsEndpoint, Playback.SecondIsEndpoint );

	private static AnimationDirectionalPlayback CreateClock( IAnimationPosePlaybackNode[] children, float[] samples )
	{
		ArgumentNullException.ThrowIfNull( children );
		var clocks = new IAnimationRatePlaybackNode[ children.Length ];
		for ( var child = 0; child < children.Length; child++ )
			clocks[ child ] = children[ child ] as IAnimationRatePlaybackNode
				?? throw new ArgumentException( "Directional children need rate control.", nameof( children ) );
		return new AnimationDirectionalPlayback( clocks, samples );
	}
}
