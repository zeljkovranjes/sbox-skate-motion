namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Uncomposed float32 pose tracks retaining rotation magnitudes, masks and scale.</summary>
internal sealed class RawAnimationClip
{
	private readonly int[] _starts;
	private readonly int[] _counts;
	private readonly Vector4[] _keys;
	private readonly AnimationPoseStreamTrack[] _poseStreams;

	internal RawAnimationClip( string name, float framesPerSecond, float rateScale, int frameCount,
		int referenceIndex, byte flags, int[] starts, int[] counts, Vector4[] keys )
		: this( name, framesPerSecond, rateScale, frameCount, referenceIndex, flags, starts, counts, keys, null )
	{
	}

	internal RawAnimationClip( string name, float framesPerSecond, float rateScale, int frameCount,
		int referenceIndex, byte flags, int[] starts, int[] counts, Vector4[] keys, AnimationClipTrajectory? trajectory )
		: this( name, framesPerSecond, rateScale, frameCount, referenceIndex, flags, starts, counts, keys, trajectory, null )
	{
	}

	internal RawAnimationClip( string name, float framesPerSecond, float rateScale, int frameCount,
		int referenceIndex, byte flags, int[] starts, int[] counts, Vector4[] keys, AnimationClipTrajectory? trajectory,
		AnimationPoseStreamTrack[] poseStreams )
		: this( name, framesPerSecond, rateScale, frameCount, referenceIndex, flags, starts, counts, keys,
			trajectory, poseStreams, AnimationClipSamplingMode.Block )
	{
	}

	internal RawAnimationClip( string name, float framesPerSecond, float rateScale, int frameCount,
		int referenceIndex, byte flags, int[] starts, int[] counts, Vector4[] keys, AnimationClipTrajectory? trajectory,
		AnimationPoseStreamTrack[] poseStreams, AnimationClipSamplingMode samplingMode )
		: this( name, framesPerSecond, rateScale, frameCount, referenceIndex, flags, starts, counts, keys,
			trajectory, poseStreams, samplingMode, null )
	{
	}

	internal RawAnimationClip( string name, float framesPerSecond, float rateScale, int frameCount,
		int referenceIndex, byte flags, int[] starts, int[] counts, Vector4[] keys, AnimationClipTrajectory? trajectory,
		AnimationPoseStreamTrack[] poseStreams, AnimationClipSamplingMode samplingMode, AnimationClipAttributeSet attributes )
	{
		if ( samplingMode != AnimationClipSamplingMode.Block && samplingMode != AnimationClipSamplingMode.Continuous )
			throw new ArgumentOutOfRangeException( nameof( samplingMode ) );
		if ( trajectory.HasValue && trajectory.Value.BoneIndex >= counts.Length / 3 )
			throw new ArgumentException( "The trajectory bone must belong to the clip.", nameof( trajectory ) );
		Name = name;
		FramesPerSecond = framesPerSecond;
		RateScale = rateScale;
		FrameCount = frameCount;
		ReferenceIndex = referenceIndex;
		Looping = ( flags & 1 ) != 0;
		PhaseControlled = ( flags & 2 ) != 0;
		_starts = starts;
		_counts = counts;
		_keys = keys;
		Trajectory = trajectory;
		SamplingMode = samplingMode;
		if ( attributes is not null && !string.Equals( attributes.Name, name, StringComparison.OrdinalIgnoreCase ) )
			throw new ArgumentException( "Attribute metadata must belong to this clip.", nameof( attributes ) );
		Attributes = attributes;
		if ( poseStreams is not null )
		{
			_poseStreams = new AnimationPoseStreamTrack[ poseStreams.Length ];
			for ( var index = 0; index < poseStreams.Length; index++ )
			{
				ArgumentNullException.ThrowIfNull( poseStreams[ index ] );
				_poseStreams[ index ] = poseStreams[ index ];
			}
		}
	}

	internal string Name { get; }
	internal AnimationClipAttributeSet Attributes { get; }
	internal float FramesPerSecond { get; }
	internal float RateScale { get; }
	internal int FrameCount { get; }
	internal int ReferenceIndex { get; }
	internal bool Looping { get; }
	internal bool PhaseControlled { get; }
	internal int BoneCount => _counts.Length / 3;
	internal AnimationClipTrajectory? Trajectory { get; }
	internal AnimationClipSamplingMode SamplingMode { get; }
	internal bool HasPoseStreams => _poseStreams is not null;
	internal ReadOnlySpan<AnimationPoseStreamTrack> PoseStreams => _poseStreams;

	internal AnimationPoseStreamProducer CreatePoseStreamProducer( AnimationClipClock clock )
	{
		if ( !HasPoseStreams )
			throw new InvalidOperationException( "Pose stream sampling requires explicit track metadata." );
		return new AnimationPoseStreamProducer( clock, _poseStreams );
	}

	/// <summary>Samples local keys; the caller owns rate, looping and reference composition.</summary>
	internal void Sample( float time, bool interpolate, Span<AnimationBoneTransform> output )
	{
		if ( output.Length < BoneCount )
			throw new ArgumentException( "The output must hold every bone." );
		var first = FramesAt( time, interpolate, out var second, out var weight );
		for ( var bone = 0; bone < BoneCount; bone++ )
			output[ bone ] = SampleBone( bone, first, second, weight );
	}

	internal AnimationBoneTransform SampleBone( float time, int bone, bool interpolate )
	{
		if ( bone < 0 || bone >= BoneCount )
			throw new ArgumentOutOfRangeException( nameof( bone ) );
		var first = FramesAt( time, interpolate, out var second, out var weight );
		return SampleBone( bone, first, second, weight );
	}

	/// <summary>Samples current locals and replaces the trajectory track with its interval delta.</summary>
	internal void SampleInterval( float previousTime, float currentTime, int wraps, bool interpolate,
		Span<AnimationBoneTransform> output )
	{
		if ( !Trajectory.HasValue )
			throw new InvalidOperationException( "Interval sampling requires explicit trajectory metadata." );
		if ( output.Length < BoneCount )
			throw new ArgumentException( "The output must hold every bone." );
		var previousFirst = FramesAt( previousTime, interpolate, out var previousSecond, out var previousWeight );
		var currentFirst = FramesAt( currentTime, interpolate, out var currentSecond, out var currentWeight );
		for ( var bone = 0; bone < BoneCount; bone++ )
			output[ bone ] = SampleBone( bone, currentFirst, currentSecond, currentWeight );
		var trajectory = Trajectory.Value;
		if ( trajectory.BoneIndex < 0 )
			return;
		var previous = SampleBone( trajectory.BoneIndex, previousFirst, previousSecond, previousWeight );
		output[ trajectory.BoneIndex ] = AnimationPoseTrajectory.Delta( previous,
			output[ trajectory.BoneIndex ], trajectory, wraps != 0 );
	}

	private int FramesAt( float time, bool interpolate, out int second, out float weight )
	{
		if ( !float.IsFinite( time ) || time < 0f )
			throw new ArgumentOutOfRangeException( nameof( time ) );
		var position = MathF.Min( time * FramesPerSecond, FrameCount - 1 );
		var first = (int)position;
		second = interpolate ? Math.Min( first + 1, FrameCount - 1 ) : first;
		weight = SamplingMode == AnimationClipSamplingMode.Continuous
			? ContinuousPoseSampler.Weight( position - first )
			: (int)( ( position - first ) * 65535f ) * ( 1f / 65535f );
		return first;
	}

	private AnimationBoneTransform SampleBone( int bone, int first, int second, float weight )
	{
		if ( first == second )
			return Key( bone, first );
		if ( SamplingMode == AnimationClipSamplingMode.Continuous )
			return ContinuousPoseSampler.Blend( Key( bone, first ), Key( bone, second ), weight );
		if ( first / 8 != second / 8 && first % 8 == 7 )
			first = second;
		var start = Key( bone, first );
		var end = Key( bone, second );
		var blended = AnimationPoseBlender.Blend( start, end, weight );
		var position = blended.Position;
		position.W = start.Position.W;
		var scale = blended.Scale;
		// The block sampler carries the second axis into the auxiliary scale lane.
		scale.W = scale.Y;
		return new AnimationBoneTransform( position, blended.Rotation, scale );
	}

	private AnimationBoneTransform Key( int bone, int frame )
	{
		var rotation = Track( bone * 3 + 1, frame );
		return new AnimationBoneTransform( Track( bone * 3, frame ),
			new Quaternion( rotation.X, rotation.Y, rotation.Z, rotation.W ), Track( bone * 3 + 2, frame ) );
	}

	private Vector4 Track( int track, int frame )
	{
		var count = _counts[ track ];
		if ( count == 0 )
			return track % 3 == 2 ? Vector4.One : new Vector4( 0f, 0f, 0f, 1f );
		return _keys[ _starts[ track ] + ( count == 1 ? 0 : frame ) ];
	}
}
