namespace SkateMotion.Core.Animation;

/// <summary>Samples a bound raw clip into reusable pose storage without reference composition.</summary>
internal sealed class AnimationClipPoseBackend : IAnimationClipPoseBackend
{
	private readonly RawAnimationClip _clip;
	private readonly ushort _id;
	private readonly AnimationPoseSamplingContext _context;

	internal AnimationClipPoseBackend( RawAnimationClip clip, ushort id,
		AnimationPoseSamplingContext context, SkeletonPose pose )
	{
		ArgumentNullException.ThrowIfNull( clip );
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( pose );
		if ( clip.BoneCount != pose.Positions.Length )
			throw new ArgumentException( "The clip and pose need matching bone counts.", nameof( pose ) );
		if ( context.TrajectoryEnabled && !clip.Trajectory.HasValue )
			throw new ArgumentException( "Trajectory sampling requires explicit clip metadata.", nameof( clip ) );
		_clip = clip;
		_id = id;
		_context = context;
		Pose = pose;
	}

	internal SkeletonPose Pose { get; }

	/// <summary>Observes the shared suppression state at each preparation boundary.</summary>
	public bool Suppressed => _context.Suppressed;

	/// <summary>Returns the identifier assigned when this leaf was bound.</summary>
	public ushort ResolveClip() => _id;

	/// <summary>Samples authored times; wrap history remains owned by the playback clock.</summary>
	public void Sample( ushort clip, float start, float current, uint wraps, uint flags )
	{
		if ( clip != _id )
			throw new ArgumentException( "The requested clip is not bound to this leaf.", nameof( clip ) );
		Pose.Sample( _clip, start, current, wraps, _context.Interpolate( flags ), _context.TrajectoryEnabled );
	}
}
