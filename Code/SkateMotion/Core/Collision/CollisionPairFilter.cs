namespace SkateMotion.Core.Collision;

internal static class CollisionPairFilter
{
	internal static bool Rejects( ReadOnlySpan<uint> table, uint first, uint second )
	{
		if ( table.IsEmpty )
			return false;
		if ( table.Length < 3 )
			throw new ArgumentException( "A pair filter requires its complete header.", nameof( table ) );
		var index = unchecked( first * table[ 1 ] + second );
		var word = 3u + (index >> 5);
		if ( word >= (uint)table.Length )
			throw new ArgumentException( "Pair category exceeds the supplied filter storage.", nameof( table ) );
		return (table[ (int)word ] & (1u << (int)(index & 31u))) != 0;
	}
}
