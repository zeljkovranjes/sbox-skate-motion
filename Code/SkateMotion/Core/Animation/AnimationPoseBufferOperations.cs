namespace SkateMotion.Core.Animation;

/// <summary>Combines two prepared poses in preparation order, with separate attribute callbacks.</summary>
internal sealed class AnimationPoseBufferOperations : IAnimationLayerPoseOperations, IAnimationCompositionPoseOperations
{
	private readonly IAnimationPreparedPoseNode[] _children;
	private readonly AnimationPoseStack _stack;
	private readonly Action<int> _sampleChild;
	private readonly Action<float> _queueBlend;
	private bool _consumeHistory;
	private uint _flags;

	internal AnimationPoseBufferOperations( IAnimationPreparedPoseNode[] children,
		Action<int> sampleChild, Action<float> queueBlend )
	{
		ArgumentNullException.ThrowIfNull( children );
		ArgumentNullException.ThrowIfNull( sampleChild );
		ArgumentNullException.ThrowIfNull( queueBlend );
		if ( children.Length == 0 )
			throw new ArgumentException( "Pose operations need at least one child.", nameof( children ) );
		ArgumentNullException.ThrowIfNull( children[ 0 ] );
		var bones = children[ 0 ].Pose.Positions.Length;
		_children = new IAnimationPreparedPoseNode[ children.Length ];
		for ( var child = 0; child < children.Length; child++ )
		{
			ArgumentNullException.ThrowIfNull( children[ child ] );
			if ( children[ child ].Pose.Positions.Length != bones )
				throw new ArgumentException( "Every child needs the same bone count.", nameof( children ) );
			_children[ child ] = children[ child ];
		}
		_stack = new AnimationPoseStack( bones, 2 );
		_sampleChild = sampleChild;
		_queueBlend = queueBlend;
	}

	internal SkeletonPose Pose => _stack.Pose;
	internal int PreparedCount => _stack.Count;

	internal void ReplacePair( IAnimationPreparedPoseNode first, IAnimationPreparedPoseNode second )
	{
		ArgumentNullException.ThrowIfNull( first );
		ArgumentNullException.ThrowIfNull( second );
		var bones = Pose.Positions.Length;
		if ( _children.Length != 2 || first.Pose.Positions.Length != bones || second.Pose.Positions.Length != bones )
			throw new ArgumentException( "Replacement children must retain the pair's bone count." );
		_children[ 0 ] = first;
		_children[ 1 ] = second;
	}

	internal void Begin( bool consumeHistory, uint flags )
	{
		_consumeHistory = consumeHistory;
		_flags = flags;
		_stack.Clear();
	}

	/// <summary>Prepares a child and snapshots successful output before another child can change it.</summary>
	public bool Prepare( int child, bool enabled )
	{
		var node = _children[ child ];
		if ( !node.Prepare( enabled, _consumeHistory, _flags ) )
			return false;
		if ( PreparedCount == 2 )
			throw new InvalidOperationException( "Combine the prepared pair before preparing another pose." );
		_stack.Push( node.Pose );
		return true;
	}

	/// <summary>Blends the first prepared pose toward the second.</summary>
	public void Blend( float weight )
	{
		RequirePair();
		_stack.Blend( weight );
	}

	/// <summary>Uses the selected prepared pose's bone masks without interpreting scale as a mask.</summary>
	public void BlendWeighted( float weight, bool maskFromFirst )
	{
		RequirePair();
		_stack.BlendWeighted( weight, maskFromFirst );
	}

	/// <summary>Composes prepared operands in the requested order and retains the selected translation lane.</summary>
	public void Compose( bool reverse, bool retainFirstTranslationW )
	{
		RequirePair();
		_stack.Compose( reverse, retainFirstTranslationW );
	}

	/// <summary>Forwards attribute sampling without changing the prepared skeletal pose.</summary>
	public void Sample( int child ) => _sampleChild( child );

	/// <summary>Forwards the attribute blend weight separately from immediate skeletal blending.</summary>
	public void QueueBlend( float weight ) => _queueBlend( weight );

	private void RequirePair()
	{
		if ( PreparedCount != 2 )
			throw new InvalidOperationException( "This operation needs two successfully prepared poses." );
	}
}
