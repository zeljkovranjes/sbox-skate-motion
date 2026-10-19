namespace SkateMotion.Core.Animation;

/// <summary>A concrete raw-pose leaf connecting authored timing, preparation and immediate sampling.</summary>
internal sealed class AnimationClipPlayback : IAnimationRatePlaybackNode, IAnimationPosePlaybackNode, IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly RawAnimationClip _clip;
	private readonly AnimationClipPoseControl _control;
	private readonly AnimationPoseStreamProducer _streams;

	internal AnimationClipPlayback( RawAnimationClip clip, ushort id, AnimationPoseSamplingContext context )
	{
		ArgumentNullException.ThrowIfNull( clip );
		_clip = clip;
		ValidateRate( 1f );
		Pose = new SkeletonPose( clip.BoneCount );
		var backend = new AnimationClipPoseBackend( clip, id, context, Pose );
		Clock = new AnimationClipClock( clip.FrameCount, clip.FramesPerSecond,
			clip.RateScale, clip.Looping, clip.PhaseControlled );
		_control = new AnimationClipPoseControl( Clock, backend );
		_streams = clip.HasPoseStreams ? clip.CreatePoseStreamProducer( Clock ) : null;
	}

	internal SkeletonPose Pose { get; }
	internal AnimationClipClock Clock { get; }
	internal bool HasPose => _control.HasPose;
	internal bool HasAttributes => _clip.Attributes is not null;

	/// <summary>Queries the loaded attribute table without consuming or changing clock history.</summary>
	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result )
	{
		if ( _clip.Attributes is null )
			throw new InvalidOperationException( "Attribute queries require explicit clip metadata." );
		return TryGetAttribute( _clip.Attributes.Records, name, flags, ref result );
	}

	/// <summary>Appends all eligible records from the loaded clip at its retained time.</summary>
	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags )
	{
		if ( _clip.Attributes is null )
			throw new InvalidOperationException( "Attribute sampling requires explicit clip metadata." );
		AnimationClipAttributeProducer.Sample( _clip.Attributes.Records, Clock, _clip.FramesPerSecond, output, flags );
	}

	internal bool TryGetAttribute( ReadOnlySpan<AnimationClipAttribute> attributes,
		AnimationAttributeKey name, byte flags, ref AnimationAttributeQueryResult result ) =>
		AnimationClipAttributeQuery.TryGet( attributes, Clock, _clip.FramesPerSecond, name, flags, ref result );

	/// <summary>Emits authored stream commands at the same retained time as skeletal preparation.</summary>
	internal void SamplePoseStreams( AnimationPoseStreamContext context )
	{
		if ( _streams is null )
			throw new InvalidOperationException( "Pose stream sampling requires explicit track metadata." );
		_streams.Sample( context );
	}

	SkeletonPose IAnimationPreparedPoseNode.Pose => Pose;
	void IAnimationPosePlaybackNode.SamplePoseStreams( AnimationPoseStreamContext context ) => SamplePoseStreams( context );
	bool IAnimationPreparedPoseNode.Prepare( bool enabled, bool consumeHistory, uint flags ) =>
		Prepare( enabled, consumeHistory, flags );

	/// <summary>Duration at the current playback rate.</summary>
	public float Duration => Clock.Duration;

	/// <summary>Elapsed playback time capped at the authored endpoint.</summary>
	public float Elapsed => Clock.Elapsed;

	internal bool Prepare( bool enabled, bool consumeHistory, uint flags ) =>
		_control.Prepare( enabled, consumeHistory, flags );

	/// <summary>Prepares a discrete frame selection without consuming history or enabling interpolation.</summary>
	internal bool PrepareFrame( int frame )
	{
		Clock.SelectFrame( frame );
		return Prepare( true, false, 0 );
	}

	/// <summary>Advances the clock; preparation subsequently samples and optionally consumes history.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
	{
		if ( !float.IsFinite( delta ) || !float.IsFinite( Clock.Position + delta ) )
			throw new ArgumentOutOfRangeException( nameof( delta ) );
		if ( !float.IsFinite( phase ) )
			throw new ArgumentOutOfRangeException( nameof( phase ) );
		Clock.Advance( delta, phase, ref completion );
	}

	/// <summary>Seeks both samples and clears accumulated wrap history.</summary>
	public void Seek( float time )
	{
		if ( !float.IsFinite( time ) )
			throw new ArgumentOutOfRangeException( nameof( time ) );
		Clock.Seek( time );
	}

	/// <summary>Changes rate while retaining authored frame positions.</summary>
	public void SetRate( float rate )
	{
		ValidateRate( rate );
		Clock.SetRate( rate );
	}

	private void ValidateRate( float rate )
	{
		var scaledFrames = ( _clip.FramesPerSecond * _clip.RateScale ) * rate;
		var duration = ( _clip.FrameCount - 1f ) / scaledFrames;
		if ( _clip.FrameCount < 1 || !float.IsFinite( rate ) || rate <= 0f
			|| !float.IsFinite( scaledFrames ) || scaledFrames <= 0f || !float.IsFinite( duration )
			|| ( _clip.Looping && !_clip.PhaseControlled && duration <= 0f ) )
			throw new ArgumentOutOfRangeException( nameof( rate ), "Playback needs finite positive timing; timed loops need a positive duration." );
	}
}
