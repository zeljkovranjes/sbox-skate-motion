using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Raw local clips and references, with versioned trajectory and pose-stream metadata.</summary>
internal sealed class RawAnimationClipSet
{
	private readonly Dictionary<string, RawAnimationClip> _clips = new( StringComparer.OrdinalIgnoreCase );
	private readonly AnimationBoneTransform[][] _references;

	private RawAnimationClipSet( string[] names, int[] parents, AnimationBoneTransform[][] references )
	{
		Names = names;
		Parents = parents;
		_references = references;
	}

	internal string[] Names { get; }
	internal int[] Parents { get; }
	internal int Count => _clips.Count;
	internal int BoneCount => Names.Length;
	internal int ReferenceCount => _references.Length;
	internal RawAnimationClip Find( string name ) => _clips.TryGetValue( name, out var clip ) ? clip : null;
	internal ReadOnlySpan<AnimationBoneTransform> Reference( int index ) => _references[ index ];
	internal AnimationBoneTransform ReferenceBone( int index, int bone ) => _references[ index ][ bone ];

	/// <summary>Creates a reusable sampler bound to the clip's selected reference pose.</summary>
	internal ReferencedAnimationClip CreateReferencedClip( string name )
	{
		var clip = Find( name );
		return clip is null ? null : new ReferencedAnimationClip( clip, _references[ clip.ReferenceIndex ] );
	}

	internal static RawAnimationClipSet Read( ReadOnlySpan<byte> data ) => ReadCore( data, null );

	internal static RawAnimationClipSet ReadWithAttributes( ReadOnlySpan<byte> data, AnimationClipAttributeBank attributes )
	{
		ArgumentNullException.ThrowIfNull( attributes );
		return ReadCore( data, attributes );
	}

	private static RawAnimationClipSet ReadCore( ReadOnlySpan<byte> data, AnimationClipAttributeBank attributes )
	{
		var reader = new AnimationBinaryReader( data );
		var magic = reader.UInt32();
		var version = reader.UInt32();
		if ( magic != 0x4e414d53 || ( version != 2 && version != 3 && version != 4 && version != 5 ) )
			throw new FormatException( "Expected a version 2, 3, 4 or 5 raw pose container." );
		var bones = (int)reader.UInt16();
		if ( bones == 0 || bones > reader.Remaining / 3 )
			throw new FormatException( "Invalid bone count." );
		var names = new string[ bones ];
		var parents = new int[ bones ];
		for ( var bone = 0; bone < bones; bone++ )
		{
			names[ bone ] = reader.Text();
			parents[ bone ] = (short)reader.UInt16();
			if ( parents[ bone ] < -1 || parents[ bone ] >= bone )
				throw new FormatException( "Each parent must precede its child." );
		}
		var count = (int)reader.UInt16();
		if ( count == 0 || count > reader.Remaining / ( bones * 48 ) )
			throw new FormatException( "Invalid reference count." );
		var references = new AnimationBoneTransform[ count ][];
		for ( var reference = 0; reference < count; reference++ )
		{
			references[ reference ] = new AnimationBoneTransform[ bones ];
			for ( var bone = 0; bone < bones; bone++ )
				references[ reference ][ bone ] = ReadTransform( ref reader );
		}
		var result = new RawAnimationClipSet( names, parents, references );
		var clips = reader.UInt32();
		if ( clips > reader.Remaining / ( 15 + bones * 3 ) )
			throw new FormatException( "Invalid clip count." );
		for ( var index = 0u; index < clips; index++ )
		{
			var clip = ReadClip( ref reader, bones, count, version, attributes );
			if ( !result._clips.TryAdd( clip.Name, clip ) )
				throw new FormatException( "Duplicate clip name." );
		}
		if ( reader.Remaining != 0 )
			throw new FormatException( "Unexpected trailing pose data." );
		return result;
	}

	private static AnimationBoneTransform ReadTransform( ref AnimationBinaryReader reader )
	{
		var position = reader.Vector();
		var rotation = reader.Vector();
		return new AnimationBoneTransform( position,
			new Quaternion( rotation.X, rotation.Y, rotation.Z, rotation.W ), reader.Vector() );
	}

	private static RawAnimationClip ReadClip( ref AnimationBinaryReader reader, int bones, int references, uint version,
		AnimationClipAttributeBank attributes )
	{
		var name = reader.Text();
		var clipAttributes = attributes?.Find( name );
		if ( attributes is not null && clipAttributes is null )
			throw new FormatException( "The attribute bank is missing a pose clip." );
		var fps = reader.Single();
		var rate = reader.Single();
		var frames = (int)reader.UInt16();
		var reference = (int)reader.UInt16();
		var sampler = reader.Byte();
		var flags = reader.Byte();
		var validSampler = sampler == 1 || ( version >= 5 && sampler == 2 );
		if ( fps <= 0f || rate <= 0f || frames == 0 || reference >= references || !validSampler || ( flags & ~3 ) != 0 )
			throw new FormatException( "Invalid raw clip metadata." );
		AnimationClipTrajectory? trajectory = null;
		if ( version >= 3 )
		{
			var bone = unchecked( (int)reader.UInt32() );
			if ( bone < -1 || bone >= bones )
				throw new FormatException( "Invalid trajectory bone." );
			var translation = new Vector3( reader.Single(), reader.Single(), reader.Single() );
			var rotation = reader.Vector();
			trajectory = new AnimationClipTrajectory( bone, translation,
				new Quaternion( rotation.X, rotation.Y, rotation.Z, rotation.W ) );
		}
		var streams = version >= 4 ? AnimationPoseStreamReader.Read( ref reader ) : null;
		var kinds = reader.Take( bones * 3 );
		var starts = new int[ kinds.Length ];
		var counts = new int[ kinds.Length ];
		var keyCount = 0;
		for ( var track = 0; track < kinds.Length; track++ )
		{
			var count = kinds[ track ] switch
			{
				0 => 0,
				1 => 1,
				2 => frames,
				_ => throw new FormatException( "Unknown pose track kind." )
			};
			if ( count > reader.Remaining / 16 - keyCount )
				throw new FormatException( "Truncated pose keys." );
			starts[ track ] = keyCount;
			counts[ track ] = count;
			keyCount += count;
		}
		var keys = new Vector4[ keyCount ];
		for ( var key = 0; key < keys.Length; key++ )
			keys[ key ] = reader.Vector();
		return new RawAnimationClip( name, fps, rate, frames, reference, flags, starts, counts, keys, trajectory, streams,
			(AnimationClipSamplingMode)sampler, clipAttributes );
	}
}
