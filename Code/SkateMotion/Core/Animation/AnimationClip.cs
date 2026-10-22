namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Local bone keys sampled at a fixed rate. A track holds no key (rest), one key or one key per frame.</summary>
internal sealed class AnimationClip
{
	private readonly int[] _rotationStart;
	private readonly int[] _rotationCount;
	private readonly int[] _positionStart;
	private readonly int[] _positionCount;
	private readonly Quaternion[] _rotations;
	private readonly Vector3[] _positions;
	private float[] _boneWeights;

	internal AnimationClip( string name, float framesPerSecond, int frameCount, int[] rotationStart, int[] rotationCount,
		Quaternion[] rotations, int[] positionStart, int[] positionCount, Vector3[] positions )
	{
		if ( !(framesPerSecond > 0f) || frameCount < 1 )
			throw new ArgumentException( "A clip needs a positive frame rate and at least one frame." );
		Name = name;
		FramesPerSecond = framesPerSecond;
		FrameCount = frameCount;
		_rotationStart = rotationStart;
		_rotationCount = rotationCount;
		_rotations = rotations;
		_positionStart = positionStart;
		_positionCount = positionCount;
		_positions = positions;
	}

	/// <summary>A clip that stands for a blend: it has no keys of its own and is sampled through the blend.</summary>
	internal AnimationClip( string name, BlendNode blend ) : this( name, 30f, 1, Array.Empty<int>(), Array.Empty<int>(),
		Array.Empty<Quaternion>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<Vector3>() ) => Blend = blend;

	/// <summary>The blend this clip stands for, or null for a keyed clip.</summary>
	internal BlendNode Blend { get; }

	/// <summary>Full precision raw sampler and its selected reference, when supplied by the asset loader.</summary>
	internal ReferencedAnimationClip RawPose { get; private set; }

	internal void BindRawPose( ReferencedAnimationClip pose )
	{
		ArgumentNullException.ThrowIfNull( pose );
		if ( Blend is not null || pose.Clip.BoneCount != _positionCount.Length
			|| !string.Equals( Name, pose.Clip.Name, StringComparison.OrdinalIgnoreCase )
			|| FrameCount != pose.Clip.FrameCount || FramesPerSecond != pose.Clip.FramesPerSecond )
			throw new ArgumentException( "Raw poses must match the keyed clip's name, timing and skeleton.", nameof( pose ) );
		RawPose = pose;
		// A clip goes round only if its source plays it round (a loop, or a cycle whose phase the controller drives): a
		// one-shot shown past its end, in a state that outlasts it, holds its last pose. Wrapped, a hippy jump's crouch held
		// longer than its anticipation sprang back up to standing and down again three times a second. The graph's loop
		// entries, read after, still override it.
		Looping = pose.Clip.Looping || pose.Clip.PhaseControlled;
	}

	/// <summary>Maps the graph's requested time to the raw sampler without quantizing it to a frame.</summary>
	internal float RawSampleTime( float time, bool looping )
	{
		var end = ( FrameCount - 1f ) / FramesPerSecond;
		if ( looping && Looping && end > 0f )
		{
			time %= end;
			if ( time < 0f )
				time += end;
		}
		return Math.Clamp( time, 0f, end );
	}

	/// <summary>Applies an explicit graph mask without discarding raw masks when no override exists.</summary>
	internal void ApplyBoneWeightOverride( Span<float> output )
	{
		if ( _boneWeights is not null )
			_boneWeights.AsSpan().CopyTo( output );
	}

	/// <summary>Length in seconds; a blend's depends on the parameters.</summary>
	internal float Length( AnimationParameters parameters ) => Blend?.Duration( parameters ) ?? Duration;

	internal string Name { get; }
	internal float FramesPerSecond { get; }
	internal int FrameCount { get; }

	/// <summary>
	/// Whether a caller requesting looping may wrap this clip; false holds its last frame. Bound to raw poses it follows the
	/// source's playback, which the graph's loop entries override.
	/// </summary>
	internal bool Looping { get; set; } = true;

	/// <summary>Copies authored layer weights in skeleton order; ordinary clips use full influence.</summary>
	internal void SetBoneWeights( ReadOnlySpan<float> weights )
	{
		if ( Blend is not null || weights.Length != _positionCount.Length )
			throw new ArgumentException( "Bone weights must match the keyed clip's skeleton." );
		for ( var i = 0; i < weights.Length; i++ )
		{
			if ( !float.IsFinite( weights[ i ] ) )
				throw new ArgumentException( "Bone weights must be finite." );
		}
		var copy = new float[ weights.Length ];
		weights.CopyTo( copy );
		_boneWeights = copy;
	}

	internal void SampleBoneWeights( Span<float> output )
	{
		if ( output.Length != _positionCount.Length )
			throw new ArgumentException( "The weight buffer must match the keyed clip's skeleton." );
		if ( _boneWeights is null )
			output.Fill( 1f );
		else
			_boneWeights.AsSpan().CopyTo( output );
	}

	/// <summary>Length in seconds; a looping clip's last frame meets its first.</summary>
	internal float Duration => Math.Max( FrameCount - 1, 1 ) / FramesPerSecond;

	/// <summary>Writes this clip's local pose at <paramref name="time"/> seconds into the pose buffers.</summary>
	internal void Sample( ClipSkeleton skeleton, float time, bool looping, Span<Vector3> positions, Span<Quaternion> rotations )
	{
		var frame = FrameAt( time, looping && Looping, out var next, out var blend );
		for ( var bone = 0; bone < skeleton.Count; bone++ )
		{
			rotations[ bone ] = Rotation( skeleton, bone, frame, next, blend );
			positions[ bone ] = Position( skeleton, bone, frame, next, blend );
		}
	}

	private int FrameAt( float time, bool looping, out int next, out float blend )
	{
		var last = FrameCount - 1;
		var position = time * FramesPerSecond;
		if ( looping && last > 0 )
			position %= last;
		position = Math.Clamp( position < 0f ? position + last : position, 0f, last );
		var frame = Math.Min( (int)position, last );
		next = Math.Min( frame + 1, last );
		blend = position - frame;
		return frame;
	}

	private Quaternion Rotation( ClipSkeleton skeleton, int bone, int frame, int next, float blend )
	{
		var count = _rotationCount[ bone ];
		if ( count == 0 )
			return skeleton.RestRotations[ bone ];
		var start = _rotationStart[ bone ];
		if ( count == 1 )
			return _rotations[ start ];
		return Quaternion.Slerp( _rotations[ start + frame ], _rotations[ start + next ], blend );
	}

	private Vector3 Position( ClipSkeleton skeleton, int bone, int frame, int next, float blend )
	{
		var count = _positionCount[ bone ];
		if ( count == 0 )
			return skeleton.RestPositions[ bone ];
		var start = _positionStart[ bone ];
		if ( count == 1 )
			return _positions[ start ];
		return Vector3.Lerp( _positions[ start + frame ], _positions[ start + next ], blend );
	}
}
