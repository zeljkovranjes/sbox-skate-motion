namespace SkateMotion.Core.Animation;

/// <summary>Connects composite clocks and pose controls to reusable child buffers and stream traversal.</summary>
internal abstract class AnimationPoseTreePlayback : IAnimationPosePlaybackNode
{
	private readonly IAnimationPosePlaybackNode[] _children;
	private AnimationPoseStreamContext _streams;

	protected AnimationPoseTreePlayback( IAnimationPlaybackNode playback, IAnimationPosePlaybackNode[] children )
	{
		ArgumentNullException.ThrowIfNull( playback );
		ArgumentNullException.ThrowIfNull( children );
		Clock = playback;
		_children = new IAnimationPosePlaybackNode[ children.Length ];
		Array.Copy( children, _children, children.Length );
		Operations = new AnimationPoseBufferOperations( _children, SampleChild, QueueBlend );
	}

	protected IAnimationPlaybackNode Clock { get; }
	protected AnimationPoseBufferOperations Operations { get; }

	/// <summary>The composite's reusable prepared body pose.</summary>
	public SkeletonPose Pose => Operations.Pose;
	/// <summary>Duration reported by the same clock used by the pose control.</summary>
	public float Duration => Clock.Duration;
	/// <summary>Elapsed time reported by the retained composite clock.</summary>
	public float Elapsed => Clock.Elapsed;

	/// <summary>Prepares the body without sampling or clearing auxiliary commands.</summary>
	public bool Prepare( bool enabled, bool consumeHistory, uint flags )
	{
		Operations.Begin( consumeHistory, flags );
		return PreparePose( enabled );
	}

	/// <summary>Traverses selected children without advancing clocks or resetting caller-owned streams.</summary>
	public void SamplePoseStreams( AnimationPoseStreamContext context )
	{
		ArgumentNullException.ThrowIfNull( context );
		var previous = _streams;
		_streams = context;
		try
		{
			SampleStreams();
		}
		finally
		{
			_streams = previous;
		}
	}

	/// <summary>Advances the retained clock and preserves its completion semantics.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) =>
		Clock.Advance( delta, phase, ref completion );

	/// <summary>Seeks through the retained composite's own child-selection rules.</summary>
	public void Seek( float time ) => Clock.Seek( time );

	protected abstract bool PreparePose( bool enabled );
	protected abstract void SampleStreams();

	protected IAnimationAttributePlaybackNode AttributeChild( int child ) =>
		_children[ child ] as IAnimationAttributePlaybackNode
			?? throw new InvalidOperationException( "Attribute queries require a child with authored query support." );

	protected IAnimationEvaluatedAttributeNode EvaluatedAttributeChild( int child ) =>
		AnimationEvaluatedAttributeContext.Require( _children[ child ] );

	protected void ReplacePair( IAnimationPosePlaybackNode first, IAnimationPosePlaybackNode second )
	{
		Operations.ReplacePair( first, second );
		_children[ 0 ] = first;
		_children[ 1 ] = second;
	}

	private void SampleChild( int child ) => _children[ child ].SamplePoseStreams( _streams );
	private void QueueBlend( float weight ) => _streams.QueueBlend( weight );
}
