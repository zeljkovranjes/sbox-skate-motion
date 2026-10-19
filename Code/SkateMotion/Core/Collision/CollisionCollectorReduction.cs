using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionCollectorReduction
{
	internal static void Reduce( Span<uint> records, ref uint count )
	{
		if ( count > 50 || count > records.Length / 64 )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		Span<byte> assigned = stackalloc byte[ 50 ];
		Span<int> group = stackalloc int[ 50 ];
		Span<int> retained = stackalloc int[ 50 ];
		Span<int> selection = stackalloc int[ 4 ];
		assigned.Clear();
		var retainedCount = 0;
		for ( var first = 0; first < count; ++first )
		{
			if ( assigned[ first ] != 0 )
				continue;
			assigned[ first ] = 1;
			group[ 0 ] = first;
			var groupCount = 1;
			for ( var other = first + 1; other < count; ++other )
			{
				if ( assigned[ other ] != 0 || !SamePatch( records, first, other ) )
					continue;
				assigned[ other ] = 1;
				group[ groupCount++ ] = other;
			}
			if ( groupCount <= 4 )
			{
				group[ ..groupCount ].CopyTo( retained[ retainedCount.. ] );
				retainedCount += groupCount;
				continue;
			}
			var selectedCount = SelectGeometry( records, group[ ..groupCount ], selection );
			for ( var i = 0; i < selectedCount; ++i )
				retained[ retainedCount++ ] = group[ selection[ i ] ];
		}
		if ( retainedCount >= count )
			return;
		// Preserve original record order after selecting each geometric patch.
		for ( var i = 1; i < retainedCount; ++i )
		{
			var value = retained[ i ];
			var position = i;
			while ( position > 0 && retained[ position - 1 ] > value )
			{
				retained[ position ] = retained[ position - 1 ];
				--position;
			}
			retained[ position ] = value;
		}
		count = (uint)retainedCount;
		for ( var i = 0; i < retainedCount; ++i )
		{
			if ( retained[ i ] != i )
				records.Slice( retained[ i ] * 64, 64 ).CopyTo( records.Slice( i * 64, 64 ) );
		}
	}

	private static bool SamePatch( ReadOnlySpan<uint> records, int firstIndex, int secondIndex )
	{
		var first = records.Slice( firstIndex * 64, 64 );
		var second = records.Slice( secondIndex * 64, 64 );
		if ( first[ 3 ] != second[ 3 ] || first[ 7 ] != second[ 7 ] )
			return false;
		var normal = Read( first, 8 );
		if ( Greater( 0x3f7fbe76u, Dot( normal, Read( second, 8 ) ) ) )
			return false;
		var firstDistance = Dot( normal, Read( first, 0 ).Subtract( Read( second, 0 ) ) ) & 0x7fffffffu;
		var secondDistance = Dot( normal, Read( first, 4 ).Subtract( Read( second, 4 ) ) ) & 0x7fffffffu;
		var difference = Subtract( firstDistance, secondDistance );
		var minimum = (difference & 0x80000000u) != 0 ? firstDistance : secondDistance;
		return Greater( 0x3c23d70au, minimum );
	}

	private static CollisionWordVector Read( ReadOnlySpan<uint> words, int offset ) =>
		new( words[ offset ], words[ offset + 1 ], words[ offset + 2 ], 0 );

	private static CollisionWordVector Point( ReadOnlySpan<uint> records, ReadOnlySpan<int> group,
		int index, int offset = 0 ) => Read( records, group[ index ] * 64 + offset );

	private static uint Dot( CollisionWordVector first, CollisionWordVector second ) =>
		Add( Add( Multiply( first.X, second.X ), Multiply( first.Y, second.Y ) ), Multiply( first.Z, second.Z ) );
}
