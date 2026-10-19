using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector2 = System.Numerics.Vector2;

/// <summary>Versioned attribute metadata, loaded separately from skeletal pose compression.</summary>
internal sealed class AnimationClipAttributeBank
{
	private readonly Dictionary<string, AnimationClipAttributeSet> _clips = new( StringComparer.OrdinalIgnoreCase );

	internal int Count => _clips.Count;
	internal AnimationClipAttributeSet Find( string name ) => _clips.TryGetValue( name, out var clip ) ? clip : null;

	internal static AnimationClipAttributeBank Read( ReadOnlySpan<byte> data )
	{
		var reader = new AnimationBinaryReader( data );
		if ( reader.UInt32() != 0x54414d53 || reader.UInt32() != 1 )
			throw new FormatException( "Expected a version 1 attribute bank." );
		var count = reader.UInt32();
		if ( count > reader.Remaining / 29 )
			throw new FormatException( "Invalid attribute clip count." );
		var result = new AnimationClipAttributeBank();
		for ( var index = 0u; index < count; index++ )
		{
			var name = reader.Text();
			var words = new uint[ 6 ];
			for ( var word = 0; word < words.Length; word++ )
				words[ word ] = reader.UInt32();
			var records = ReadRecords( ref reader );
			if ( !result._clips.TryAdd( name, new AnimationClipAttributeSet( name, words, records ) ) )
				throw new FormatException( "Duplicate attribute clip name." );
		}
		if ( reader.Remaining != 0 )
			throw new FormatException( "Unexpected trailing attribute data." );
		return result;
	}

	private static AnimationClipAttribute[] ReadRecords( ref AnimationBinaryReader reader )
	{
		var count = reader.UInt32();
		if ( count > reader.Remaining / 57 )
			throw new FormatException( "Invalid attribute count." );
		var records = new AnimationClipAttribute[ (int)count ];
		for ( var index = 0; index < records.Length; index++ )
		{
			var name = new AnimationAttributeKey( reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32() );
			var start = reader.Single();
			var end = reader.Single();
			var kind = unchecked( (sbyte)reader.Byte() );
			var payload = new AnimationAttributePayload( reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32() );
			var keyCount = reader.UInt32();
			if ( keyCount > reader.Remaining / 8 || (kind == 2 ? keyCount == 0 : keyCount != 0) )
				throw new FormatException( "Invalid attribute curve count." );
			AnimationScalarCurve curve = null;
			if ( kind == 2 )
			{
				var keys = new Vector2[ (int)keyCount ];
				for ( var key = 0; key < keys.Length; key++ )
				{
					keys[ key ] = new Vector2( reader.Single(), reader.Single() );
					if ( key > 0 && keys[ key ].X < keys[ key - 1 ].X )
						throw new FormatException( "Attribute curve times must be ordered." );
				}
				curve = new AnimationScalarCurve( keys );
			}
			records[ index ] = new AnimationClipAttribute( name, start, end, kind, payload, curve );
		}
		return records;
	}
}
