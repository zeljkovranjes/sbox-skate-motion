namespace SkateMotion.Core.Animation;

/// <summary>Reads signed-time tracks from the version 4 raw-pose container.</summary>
internal static class AnimationPoseStreamReader
{
	internal static AnimationPoseStreamTrack[] Read( ref AnimationBinaryReader reader )
	{
		var count = reader.Byte();
		if ( count > 31 || count > reader.Remaining / 52 )
			throw new FormatException( "Invalid pose stream count." );
		var tracks = new AnimationPoseStreamTrack[ count ];
		for ( var track = 0; track < tracks.Length; track++ )
		{
			var name = ReadName( ref reader );
			var pairedName = ReadName( ref reader );
			var keyCount = reader.UInt32();
			if ( keyCount > reader.Remaining / 4 )
				throw new FormatException( "Truncated pose stream keys." );
			var keys = new AnimationPoseStreamKeyframe[ (int)keyCount ];
			for ( var key = 0; key < keys.Length; key++ )
			{
				var time = unchecked( (short)reader.UInt16() );
				var value = reader.UInt16();
				if ( value > 127 || ( key > 0 && time < keys[ key - 1 ].Time ) )
					throw new FormatException( "Invalid pose stream key." );
				keys[ key ] = new AnimationPoseStreamKeyframe( time, value );
			}
			tracks[ track ] = new AnimationPoseStreamTrack( name, pairedName, keys );
		}
		return tracks;
	}

	private static AnimationPoseStreamKey ReadName( ref AnimationBinaryReader reader ) =>
		new( reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32(), reader.UInt32() );
}
