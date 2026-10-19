namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

internal static class PhysicalGroundQueryPipeline
{
	/// <summary>Builds segments from ordered physical query samples, then admits and selects the final support.</summary>
	internal static int Select( Span<uint> owner )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount || owner[ 0x3a50 / 4 ] > 64 )
			throw new ArgumentException( "The complete owner and ordered query samples are required.", nameof( owner ) );
		var count = PhysicalGroundQuerySegments.Build( owner.Slice( 0x2a50 / 4, 64 * 16 ),
			(int)owner[ 0x3a50 / 4 ], Read( owner, 16 ), Read( owner, 32 ), Read( owner, 48 ),
			owner.Slice( 0x4a90 / 4, 64 * 24 ), out var lastNeutralEnd );
		owner[ 0x6290 / 4 ] = (uint)lastNeutralEnd;
		owner[ 0x6294 / 4 ] = (uint)count;
		return SelectSegments( owner );
	}

	/// <summary>Admits, orders and selects support from an already prepared physical segment owner.</summary>
	internal static int SelectSegments( Span<uint> owner )
	{
		PhysicalGroundQueryAdmission.Build( owner );
		PhysicalGroundQueryCandidateSort.Sort( owner );
		return PhysicalGroundQuerySelection.SelectOrdered( owner );
	}

	private static Vector4 Read( ReadOnlySpan<uint> owner, int offset ) => new(
		BitConverter.UInt32BitsToSingle( owner[ offset / 4 ] ),
		BitConverter.UInt32BitsToSingle( owner[ offset / 4 + 1 ] ),
		BitConverter.UInt32BitsToSingle( owner[ offset / 4 + 2 ] ),
		BitConverter.UInt32BitsToSingle( owner[ offset / 4 + 3 ] ) );
}
