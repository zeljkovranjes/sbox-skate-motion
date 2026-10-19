namespace SkateMotion.Core.Animation;

/// <summary>Connects nested parameter updates and authored sample queries to a retained pose node.</summary>
internal sealed class AnimationMotionTreeNode : IAnimationDirectionalParameters
{
	private readonly AnimationMotionTreeNode[] _children;
	private readonly AnimationDirectionalPosePlayback _directional;
	private readonly AnimationAttributeKey _sample;
	private readonly AnimationOwnerPoseContext _ownerContext;
	private ReadOnlyMemory<AnimationAttributeOverride> _parameters;

	internal AnimationMotionTreeNode( IAnimationPosePlaybackNode playback,
		AnimationMotionTreeNode[] children = null, AnimationAttributeKey sample = default,
		AnimationOwnerPoseContext ownerContext = null )
	{
		Playback = playback;
		_children = children ?? Array.Empty<AnimationMotionTreeNode>();
		_directional = playback as AnimationDirectionalPosePlayback;
		_sample = sample;
		_ownerContext = ownerContext;
	}

	internal IAnimationPosePlaybackNode Playback { get; }

	internal bool Update( ReadOnlyMemory<AnimationAttributeOverride> parameters )
	{
		if ( _directional is null ) return false;
		if ( _ownerContext is not null )
			_directional.PoseThreshold = _ownerContext.PoseThreshold;
		_parameters = parameters;
		foreach ( var entry in parameters.Span )
			if ( entry.Attribute.Name == _sample )
				return _directional.UpdateParameters( this, entry.Attribute.Payload.Scalar, entry.Normalized );
		return _directional.UpdateParameters( this, 0f, false, false );
	}

	/// <summary>Updates children in the retained directional node's current storage order.</summary>
	public bool UpdateChild( int child ) => _children[ child ].Update( _parameters );

	/// <summary>Resolves a child's current sample through its authored attribute query.</summary>
	public float QuerySample( int child )
	{
		var result = default(AnimationAttributeQueryResult);
		var query = (IAnimationAttributePlaybackNode)_children[ child ].Playback;
		return query.TryGetAuthoredAttribute( _sample, 31, ref result ) ? result.Payload.Scalar : 0f;
	}
}
