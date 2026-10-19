using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// A skeleton and its clips, read from the clip-set format: little-endian "SMAN", version 1, metres, Z up, X forward.
/// Bones: name, parent, rest position and rotation. Clips: name, rate, frame count, two kinds per bone
/// (0 rest, 1 one key, 2 one key per frame) for rotation then position, followed by their keys.
/// Rotations are four signed 16-bit components scaled by 32767; positions are three floats.
/// </summary>
internal sealed class ClipSet
{
	private readonly Dictionary<string, AnimationClip> _clips = new( StringComparer.OrdinalIgnoreCase );

	internal ClipSet( ClipSkeleton skeleton, IEnumerable<AnimationClip> clips )
	{
		Skeleton = skeleton;
		foreach ( var clip in clips )
			_clips[ clip.Name ] = clip;
	}

	internal ClipSkeleton Skeleton { get; }
	internal int Count => _clips.Count;
	internal IEnumerable<AnimationClip> Clips => _clips.Values;

	/// <summary>Raw clips and metadata shared with runtime playback-tree construction.</summary>
	internal RawAnimationClipSet RawPoses { get; private set; }

	/// <summary>Binds full precision poses before blend construction, retaining the display skeleton's calibration.</summary>
	internal void BindRawPoses( RawAnimationClipSet poses )
	{
		ArgumentNullException.ThrowIfNull( poses );
		if ( poses.BoneCount != Skeleton.Count || poses.Count != _clips.Count )
			throw new ArgumentException( "Raw poses must cover the complete keyed clip set and skeleton.", nameof( poses ) );
		for ( var bone = 0; bone < Skeleton.Count; bone++ )
		{
			if ( poses.Names[ bone ] != Skeleton.Names[ bone ] || poses.Parents[ bone ] != Skeleton.Parents[ bone ] )
				throw new ArgumentException( "Raw pose skeleton names and parents must match.", nameof( poses ) );
		}
		foreach ( var clip in _clips.Values )
		{
			var raw = poses.Find( clip.Name );
			if ( clip.Blend is not null || raw is null || raw.FrameCount != clip.FrameCount
				|| raw.FramesPerSecond != clip.FramesPerSecond )
				throw new ArgumentException( "Raw poses must match every keyed clip's name and timing.", nameof( poses ) );
		}
		foreach ( var clip in _clips.Values )
			clip.BindRawPose( poses.CreateReferencedClip( clip.Name ) );
		RawPoses = poses;
	}

	internal AnimationClip Find( string name ) => _clips.TryGetValue( name, out var clip ) ? clip : null;

	/// <summary>Adds a clip for each named blend so motions can name blends like clips. Existing names are kept.</summary>
	internal void AddBlends( IReadOnlyDictionary<string, BlendDefinition> blends )
	{
		if ( blends is null || blends.Count == 0 )
			return;
		foreach ( var (name, node) in BlendNode.Build( blends, this ) )
			_clips.TryAdd( name, new AnimationClip( name, node ) );
	}

	internal static ClipSet Read( ReadOnlySpan<byte> data )
	{
		var reader = new Reader( data );
		if ( reader.Byte() != 'S' || reader.Byte() != 'M' || reader.Byte() != 'A' || reader.Byte() != 'N' )
			throw new FormatException( "Not a clip set." );
		if ( reader.UInt32() != 1 )
			throw new FormatException( "Unsupported clip set version." );
		var skeleton = ReadSkeleton( ref reader );
		var count = (int)reader.UInt32();
		var clips = new List<AnimationClip>( count );
		for ( var i = 0; i < count; i++ )
			clips.Add( ReadClip( ref reader, skeleton.Count ) );
		return new ClipSet( skeleton, clips );
	}

	private static ClipSkeleton ReadSkeleton( ref Reader reader )
	{
		var count = reader.UInt16();
		var names = new string[ count ];
		var parents = new int[ count ];
		var positions = new Vector3[ count ];
		var rotations = new Quaternion[ count ];
		for ( var i = 0; i < count; i++ )
		{
			names[ i ] = reader.Text();
			parents[ i ] = reader.Int16();
			positions[ i ] = reader.Vector();
			rotations[ i ] = Quaternion.Normalize( new Quaternion( reader.Single(), reader.Single(), reader.Single(), reader.Single() ) );
		}
		return new ClipSkeleton( names, parents, positions, rotations );
	}

	private static AnimationClip ReadClip( ref Reader reader, int bones )
	{
		var name = reader.Text();
		var rate = reader.Single();
		var frames = (int)reader.UInt16();
		var kinds = new byte[ bones * 2 ];
		for ( var i = 0; i < kinds.Length; i++ )
			kinds[ i ] = reader.Byte();
		var rotationStart = new int[ bones ];
		var rotationCount = new int[ bones ];
		var positionStart = new int[ bones ];
		var positionCount = new int[ bones ];
		var rotations = new List<Quaternion>();
		var positions = new List<Vector3>();
		for ( var bone = 0; bone < bones; bone++ )
		{
			rotationStart[ bone ] = rotations.Count;
			rotationCount[ bone ] = KeyCount( kinds[ bone * 2 ], frames );
			for ( var k = 0; k < rotationCount[ bone ]; k++ )
				rotations.Add( reader.PackedRotation() );
			positionStart[ bone ] = positions.Count;
			positionCount[ bone ] = KeyCount( kinds[ bone * 2 + 1 ], frames );
			for ( var k = 0; k < positionCount[ bone ]; k++ )
				positions.Add( reader.Vector() );
		}
		return new AnimationClip( name, rate, frames, rotationStart, rotationCount, rotations.ToArray(),
			positionStart, positionCount, positions.ToArray() );
	}

	private static int KeyCount( byte kind, int frames ) => kind switch
	{
		0 => 0,
		1 => 1,
		2 => frames,
		_ => throw new FormatException( "Unknown track kind." )
	};

	private ref struct Reader
	{
		private readonly ReadOnlySpan<byte> _data;
		private int _at;

		internal Reader( ReadOnlySpan<byte> data )
		{
			_data = data;
			_at = 0;
		}

		internal byte Byte() => _data[ _at++ ];

		internal ushort UInt16()
		{
			var value = (ushort)(_data[ _at ] | _data[ _at + 1 ] << 8);
			_at += 2;
			return value;
		}

		internal short Int16() => (short)UInt16();

		internal uint UInt32()
		{
			var value = (uint)(_data[ _at ] | _data[ _at + 1 ] << 8 | _data[ _at + 2 ] << 16 | _data[ _at + 3 ] << 24);
			_at += 4;
			return value;
		}

		internal float Single() => BitConverter.Int32BitsToSingle( (int)UInt32() );

		internal Vector3 Vector() => new( Single(), Single(), Single() );

		internal Quaternion PackedRotation()
		{
			var q = new Quaternion( Int16() / 32767f, Int16() / 32767f, Int16() / 32767f, Int16() / 32767f );
			return Quaternion.Normalize( q );
		}

		internal string Text()
		{
			var length = Byte();
			var text = System.Text.Encoding.UTF8.GetString( _data.Slice( _at, length ) );
			_at += length;
			return text;
		}
	}
}
