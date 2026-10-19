namespace SkateMotion.Core.Animation;

using System.Buffers.Binary;
using Vector3 = System.Numerics.Vector3;

/// <summary>Reads the semantic fields of a completed big-endian query result record.</summary>
internal static class PhysicalBoardProbeResultDecoder
{
	internal static bool TryDecode( ReadOnlySpan<byte> record, out PhysicalBoardProbeHit hit )
	{
		if ( record.Length < 0x80 )
			throw new ArgumentException( "A complete query result record is required.", nameof( record ) );
		hit = default;
		if ( BinaryPrimitives.ReadUInt32BigEndian( record.Slice( 0x70, 4 ) ) == 0 )
			return false;
		hit = new( ReadVector( record ), ReadVector( record.Slice( 0x10 ) ),
			ReadFloat( record.Slice( 0x60 ) ), BinaryPrimitives.ReadUInt32BigEndian( record.Slice( 0x64, 4 ) ) );
		return true;
	}

	private static Vector3 ReadVector( ReadOnlySpan<byte> record ) => new(
		ReadFloat( record ), ReadFloat( record.Slice( 4 ) ), ReadFloat( record.Slice( 8 ) ) );

	private static float ReadFloat( ReadOnlySpan<byte> record ) =>
		BitConverter.Int32BitsToSingle( BinaryPrimitives.ReadInt32BigEndian( record.Slice( 0, 4 ) ) );
}
