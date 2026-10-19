namespace SkateMotion.Core.Animation;

/// <summary>A reusable direct-leaf motion tree with authored directional samples and one reference composition.</summary>
internal sealed class BoardHandlingAnimationTree : IAnimationPlaybackTree, IAnimationDirectionalParameters
{
	private readonly AnimationClipPlayback[] _leaves;
	private readonly AnimationDirectionalPosePlayback _directional;
	private readonly AnimationAttributeKey _sampleKey;
	private readonly AnimationOwnerPoseContext _context;

	internal BoardHandlingAnimationTree( RawAnimationClip[] clips, AnimationBoneTransform[] reference,
		AnimationAttributeKey sampleKey, AnimationOwnerPoseContext context )
	{
		if ( clips.Length == 0 )
			throw new ArgumentException( "A motion needs at least one clip.", nameof( clips ) );
		_sampleKey = sampleKey;
		_context = context;
		_leaves = new AnimationClipPlayback[ clips.Length ];
		var children = new IAnimationPosePlaybackNode[ clips.Length ];
		for ( var i = 0; i < clips.Length; i++ )
		{
			_leaves[ i ] = new AnimationClipPlayback( clips[ i ], (ushort)i, context.Sampling );
			children[ i ] = _leaves[ i ];
		}
		IAnimationPosePlaybackNode active = children[ 0 ];
		if ( clips.Length > 1 )
		{
			_directional = new AnimationDirectionalPosePlayback( children, new float[ clips.Length ] );
			active = _directional;
		}
		PoseRoot = new AnimationCompositionPosePlayback( active, new AnimationReferencePosePlayback( reference ), true );
		UpdateSamplingContext();
	}

	internal IAnimationPosePlaybackNode PoseRoot { get; }
	internal bool InUse { get; set; }

	/// <summary>The composed motion sharing the authored clip or directional clock.</summary>
	public IAnimationPlaybackNode Root => PoseRoot;

	internal void Restart( float rate )
	{
		foreach ( var leaf in _leaves )
			leaf.Seek( 0f );
		((IAnimationRatePlaybackNode)PoseRoot).SetRate( rate );
		PoseRoot.Seek( 0f );
	}

	internal void UpdateYaw( float yaw ) => _directional?.UpdateParameters( this, yaw, false );

	internal void UpdateSamplingContext()
	{
		if ( _directional is not null )
			_directional.PoseThreshold = _context.PoseThreshold;
	}

	/// <summary>Direct clip leaves have no child parameter bindings.</summary>
	public bool UpdateChild( int child ) => false;

	/// <summary>Reads sample coordinates through the retained clip clock and authored record query.</summary>
	public float QuerySample( int child )
	{
		var result = default(AnimationAttributeQueryResult);
		return _leaves[ child ].TryGetAuthoredAttribute( _sampleKey, 31, ref result ) ? result.Payload.Scalar : 0f;
	}
}
