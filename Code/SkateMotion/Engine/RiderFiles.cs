using Sandbox;

namespace SkateMotion.Engine;

/// <summary>
/// The rider's animation files. Each is found among the mounted files (this package's own, which ship the default rider
/// under data/skater, or a game's assets) or else in the game's data folder, and read back to its original bytes from
/// how it may ship packed (dev/pack_poses.py): a raw pose container arranged and packed ("SMZ1", the original length, the
/// header length, then an LZ4 frame), or any file as one plain LZ4 frame (the graph, the attribute bank). A file that is
/// neither is read as it is.
/// </summary>
internal static class RiderFiles
{
	/// <summary>Whether the file is among the mounted files or in the data folder.</summary>
	internal static bool Exists( string path ) => !string.IsNullOrWhiteSpace( path ) && Holding( path ) is not null;

	/// <summary>The file's original bytes; throws when it is in neither place.</summary>
	internal static byte[] Read( string path )
	{
		var files = Holding( path ) ?? throw new FormatException( $"Rider animation file not found: {path}" );
		var data = files.ReadAllBytes( path ).ToArray();
		if ( data.Length >= 12 && data[ 0 ] == 'S' && data[ 1 ] == 'M' && data[ 2 ] == 'Z' && data[ 3 ] == '1' )
			return Unpack( data );
		// The LZ4 frame format's own magic number, 0x184D2204, little-endian.
		if ( data.Length >= 4 && data[ 0 ] == 0x04 && data[ 1 ] == 0x22 && data[ 2 ] == 0x4D && data[ 3 ] == 0x18 )
			return Sandbox.Compression.LZ4.DecompressFrame( data );
		return data;
	}

	/// <summary>The file's original text (UTF-8).</summary>
	internal static string ReadText( string path ) => System.Text.Encoding.UTF8.GetString( Read( path ) );

	// The mounted files first, then the data folder (files a game writes or supplies at run time).
	private static BaseFileSystem Holding( string path ) =>
		FileSystem.Mounted.FileExists( path ) ? FileSystem.Mounted : FileSystem.Data.FileExists( path ) ? FileSystem.Data : null;

	private static byte[] Unpack( byte[] data )
	{
		var length = data[ 4 ] | data[ 5 ] << 8 | data[ 6 ] << 16 | data[ 7 ] << 24;
		var headers = data[ 8 ] | data[ 9 ] << 8 | data[ 10 ] << 16 | data[ 11 ] << 24;
		var payload = Sandbox.Compression.LZ4.DecompressFrame( new System.ReadOnlySpan<byte>( data, 12, data.Length - 12 ) );
		return PackedPoseLayout.Restore( payload, headers, length );
	}
}
