namespace SkateMotion.Core.Animation;

/// <summary>Snapshots prepared poses and reduces the most recent operands without allocating per frame.</summary>
internal sealed class AnimationPoseStack
{
	private readonly SkeletonPose[] _poses;

	internal AnimationPoseStack( int boneCount, int capacity )
	{
		if ( boneCount < 0 || capacity < 1 )
			throw new ArgumentException( "Pose storage needs a nonnegative bone count and positive capacity." );
		_poses = new SkeletonPose[ capacity ];
		for ( var index = 0; index < capacity; index++ )
			_poses[ index ] = new SkeletonPose( boneCount );
	}

	/// <summary>Stable bottom buffer, containing the combined result when one pose remains.</summary>
	internal SkeletonPose Pose => _poses[ 0 ];
	internal int Count { get; private set; }

	/// <summary>Most recent result, including streams that leave earlier operands unreduced.</summary>
	internal SkeletonPose Top => Count > 0
		? _poses[ Count - 1 ]
		: throw new InvalidOperationException( "An empty pose stack has no top result." );

	internal void Clear() => Count = 0;

	internal void Push( SkeletonPose pose )
	{
		ArgumentNullException.ThrowIfNull( pose );
		if ( pose.Positions.Length != Pose.Positions.Length )
			throw new ArgumentException( "Stacked poses need matching bone counts.", nameof( pose ) );
		if ( Count == _poses.Length )
			throw new InvalidOperationException( "Combine poses before exceeding the allocated stack capacity." );
		_poses[ Count ].CopyFrom( pose );
		Count++;
	}

	internal void Blend( float weight )
	{
		RequireOperands( 2 );
		_poses[ Count - 2 ].BlendToward( _poses[ Count - 1 ], weight );
		Count--;
	}

	internal void BlendWeighted( float weight, bool maskFromFirst )
	{
		RequireOperands( 2 );
		_poses[ Count - 2 ].BlendWeightedToward( _poses[ Count - 1 ], weight, maskFromFirst );
		Count--;
	}

	internal void BlendMany( ReadOnlySpan<float> weights )
	{
		RequireOperands( weights.Length );
		var first = Count - weights.Length;
		_poses[ first ].BlendMany( _poses.AsSpan( first, weights.Length ), weights );
		Count = first + 1;
	}

	internal void Compose( bool reverse, bool retainFirstTranslationW )
	{
		RequireOperands( 2 );
		_poses[ Count - 2 ].ComposeWith( _poses[ Count - 1 ], reverse, retainFirstTranslationW );
		Count--;
	}

	private void RequireOperands( int count )
	{
		if ( count < 1 || count > Count )
			throw new InvalidOperationException( "The operation needs enough successfully prepared poses." );
	}
}
