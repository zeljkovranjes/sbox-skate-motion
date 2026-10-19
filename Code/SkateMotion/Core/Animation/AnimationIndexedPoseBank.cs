namespace SkateMotion.Core.Animation;

/// <summary>Caches discrete local poses through authored frame timing for indexed stream selection.</summary>
internal sealed class AnimationIndexedPoseBank
{
	private readonly AnimationBoneTransform[] _frames;
	private readonly SkeletonPose _selected;

	internal AnimationIndexedPoseBank( RawAnimationClip clip )
	{
		ArgumentNullException.ThrowIfNull( clip );
		var playback = new AnimationClipPlayback( clip, 0, new AnimationPoseSamplingContext( 0, false ) );
		Count = clip.FrameCount;
		BoneCount = clip.BoneCount;
		_frames = new AnimationBoneTransform[ checked( Count * BoneCount ) ];
		_selected = new SkeletonPose( BoneCount );
		for ( var index = 0; index < Count; index++ )
		{
			// Clock rounding is observable: a selected index can sample the preceding key.
			playback.PrepareFrame( index );
			for ( var bone = 0; bone < BoneCount; bone++ )
				_frames[ index * BoneCount + bone ] = playback.Pose.Transform( bone );
		}
	}

	internal int Count { get; }
	internal int BoneCount { get; }

	/// <summary>Copies a cached local pose into reusable selection storage; reference composition follows blending.</summary>
	internal SkeletonPose Sample( int index )
	{
		if ( index < 0 || index >= Count )
			throw new ArgumentOutOfRangeException( nameof( index ) );
		_selected.SetTransforms( _frames.AsSpan( index * BoneCount, BoneCount ) );
		return _selected;
	}
}
