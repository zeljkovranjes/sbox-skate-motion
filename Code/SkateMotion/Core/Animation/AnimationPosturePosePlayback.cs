namespace SkateMotion.Core.Animation;

/// <summary>Wraps a retained motion with authored body posture, attribute mapping and paired streams.</summary>
internal sealed class AnimationPosturePosePlayback : IAnimationPosePlaybackNode, IAnimationRatePlaybackNode,
	IAnimationAttributePlaybackNode, IAnimationEvaluatedAttributeNode
{
	private readonly IAnimationPosePlaybackNode _child;
	private readonly IAnimationRatePlaybackNode _clock;
	private readonly IAnimationAttributePlaybackNode _query;
	private readonly IAnimationEvaluatedAttributeNode _attributes;
	private readonly AnimationPostureBoneMap _bones;
	private readonly AnimationPostureAttributeMap _mapping;
	private readonly int _mode;

	internal AnimationPosturePosePlayback( IAnimationPosePlaybackNode child, AnimationPostureBoneMap bones,
		AnimationPostureAttributeMap mapping, int mode )
	{
		ArgumentNullException.ThrowIfNull( child );
		ArgumentNullException.ThrowIfNull( bones );
		ArgumentNullException.ThrowIfNull( mapping );
		if ( bones.BoneCount != child.Pose.Positions.Length )
			throw new ArgumentException( "Posture mapping must match the child's skeleton.", nameof( bones ) );
		_child = child;
		_clock = child as IAnimationRatePlaybackNode
			?? throw new ArgumentException( "Posture playback requires a rated child.", nameof( child ) );
		_query = child as IAnimationAttributePlaybackNode
			?? throw new ArgumentException( "Posture playback requires authored attribute queries.", nameof( child ) );
		_attributes = AnimationEvaluatedAttributeContext.Require( child );
		_bones = bones;
		_mapping = mapping;
		_mode = mode;
	}

	public SkeletonPose Pose => _child.Pose;
	public float Duration => _clock.Duration;
	public float Elapsed => _clock.Elapsed;

	public bool Prepare( bool enabled, bool consumeHistory, uint flags )
	{
		var ready = _child.Prepare( enabled, consumeHistory, flags );
		if ( enabled && ready )
			AnimationRuntimePosturePoseTransform.Apply( Pose, _bones, _mode );
		return ready;
	}

	public void SamplePoseStreams( AnimationPoseStreamContext context )
	{
		_child.SamplePoseStreams( context );
		context.SwapPairs = true;
	}

	public bool TryGetAuthoredAttribute( AnimationAttributeKey name, byte flags,
		ref AnimationAttributeQueryResult result )
	{
		if ( !_query.TryGetAuthoredAttribute( name, flags, ref result ) ) return false;
		result = _mapping.Transform( result );
		return true;
	}

	public void SampleEvaluatedAttributes( AnimationEvaluatedAttributeContext context,
		AnimationEvaluatedAttributeBuffer output, byte flags )
	{
		_attributes.SampleEvaluatedAttributes( context, output, flags );
		_mapping.Transform( output );
	}

	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) =>
		_clock.Advance( delta, phase, ref completion );
	public void Seek( float time ) => _clock.Seek( time );
	public void SetRate( float rate ) => _clock.SetRate( rate );
}
