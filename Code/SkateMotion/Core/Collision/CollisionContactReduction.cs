using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static class CollisionContactReduction
{
	internal static bool RejectNewest( ReadOnlySpan<uint> records, int count, uint squaredTolerance,
		bool disabled, ref uint rejectedCount )
	{
		if ( count < 1 || count > records.Length / 64 )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		if ( Greater( 0, squaredTolerance ) || disabled )
			return false;
		var newest = records.Slice( (count - 1) * 64, 64 );
		for ( var index = 0; index < count - 1; ++index )
		{
			var previous = records.Slice( index * 64, 64 );
			if ( previous[ 3 ] != newest[ 3 ] || previous[ 7 ] != newest[ 7 ] )
				continue;
			if ( !Greater( squaredTolerance, DistanceSquared( previous, newest, 0 ) ) ||
				!Greater( squaredTolerance, DistanceSquared( previous, newest, 4 ) ) )
				continue;
			var dot = Add( Add( Multiply( previous[ 8 ], newest[ 8 ] ),
				Multiply( previous[ 9 ], newest[ 9 ] ) ), Multiply( previous[ 10 ], newest[ 10 ] ) );
			// The source widens the rounded dot before subtracting this double threshold.
			var difference = BitConverter.DoubleToInt64Bits( (double)Value( dot ) - 0.94 );
			var high = (int)(difference >> 32);
			if ( high > 0 || (high == 0 && (uint)difference != 0) )
			{
				rejectedCount = unchecked( rejectedCount + 1 );
				return true;
			}
		}
		return false;
	}

	private static uint DistanceSquared( ReadOnlySpan<uint> previous, ReadOnlySpan<uint> newest, int offset )
	{
		var x = Subtract( previous[ offset ], newest[ offset ] );
		var y = Subtract( previous[ offset + 1 ], newest[ offset + 1 ] );
		var z = Subtract( previous[ offset + 2 ], newest[ offset + 2 ] );
		return Add( Add( Multiply( x, x ), Multiply( y, y ) ), Multiply( z, z ) );
	}
}
