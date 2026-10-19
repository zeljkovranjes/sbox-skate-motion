namespace SkateMotion.Core.Animation;

internal static class PhysicalGroundQueryCandidateSort
{
	private const int Stride = 20;
	private const int SemanticWords = 18;
	private const int InsertionLimit = 28;

	/// <summary>Orders candidate projections while retaining each destination record's padding.</summary>
	internal static void Sort( Span<uint> owner )
	{
		if ( owner.Length < PhysicalGroundQuerySelection.OwnerWordCount || owner[ 0x76f0 / 4 ] > 64 )
			throw new ArgumentException( "A complete query owner with at most 64 admitted candidates is required.", nameof( owner ) );
		var count = (int)owner[ 0x76f0 / 4 ];
		if ( count == 0 )
			return;
		var records = owner.Slice( 0x62a0 / 4, count * Stride );
		var depth = 0;
		for ( var remaining = count; remaining > 1; remaining >>= 1 )
			depth += 2;
		Partition( records, 0, count, depth );
		// Partitioning provides the sentinel for the unguarded tail insertion.
		Insert( records, 1, Math.Min( InsertionLimit, count ) );
		if ( count > InsertionLimit )
			Insert( records, InsertionLimit, count );
	}

	private static void Partition( Span<uint> records, int begin, int end, int depth )
	{
		Span<uint> temporary = stackalloc uint[ SemanticWords ];
		while ( end - begin > InsertionLimit )
		{
			if ( depth == 0 )
			{
				HeapSort( records, begin, end );
				return;
			}
			depth--;
			var middle = begin + (end - begin) / 2;
			var last = end - 1;
			var pivot = Less( records, begin, middle )
				? (Less( records, middle, last ) ? middle : Less( records, begin, last ) ? last : begin)
				: (Less( records, begin, last ) ? begin : Less( records, middle, last ) ? last : middle);
			var key = Key( Record( records, pivot ) );
			var left = begin;
			var right = end;
			while ( true )
			{
				while ( Key( Record( records, left ) ) < key )
					left++;
				do
				{
					right--;
				} while ( key < Key( Record( records, right ) ) );
				if ( left >= right )
					break;
				Record( records, left ).CopyTo( temporary );
				Record( records, right ).CopyTo( Record( records, left ) );
				temporary.CopyTo( Record( records, right ) );
				left++;
			}
			Partition( records, left, end, depth );
			end = left;
		}
		if ( depth == 0 )
			HeapSort( records, begin, end );
	}

	private static void Insert( Span<uint> records, int first, int end )
	{
		Span<uint> temporary = stackalloc uint[ SemanticWords ];
		for ( var current = first; current < end; current++ )
		{
			Record( records, current ).CopyTo( temporary );
			var hole = current;
			while ( hole > 0 && Key( temporary ) < Key( Record( records, hole - 1 ) ) )
			{
				Record( records, hole - 1 ).CopyTo( Record( records, hole ) );
				hole--;
			}
			temporary.CopyTo( Record( records, hole ) );
		}
	}

	private static void HeapSort( Span<uint> records, int begin, int end )
	{
		var heap = records.Slice( begin * Stride, (end - begin) * Stride );
		var count = end - begin;
		Span<uint> temporary = stackalloc uint[ SemanticWords ];
		for ( var parent = (count - 2) / 2; count > 1 && parent >= 0; parent-- )
		{
			Record( heap, parent ).CopyTo( temporary );
			AdjustHeap( heap, parent, count, temporary );
		}
		for ( var last = count - 1; last > 0; last-- )
		{
			Record( heap, last ).CopyTo( temporary );
			Record( heap, 0 ).CopyTo( Record( heap, last ) );
			AdjustHeap( heap, 0, last, temporary );
		}
	}

	private static void AdjustHeap( Span<uint> records, int top, int count, ReadOnlySpan<uint> value )
	{
		var hole = top;
		var right = 2 * hole + 2;
		while ( right < count )
		{
			if ( Less( records, right, right - 1 ) )
				right--;
			Record( records, right ).CopyTo( Record( records, hole ) );
			hole = right;
			right = 2 * hole + 2;
		}
		if ( right == count )
		{
			Record( records, right - 1 ).CopyTo( Record( records, hole ) );
			hole = right - 1;
		}
		while ( hole > top )
		{
			var parent = (hole - 1) / 2;
			if ( !(Key( Record( records, parent ) ) < Key( value )) )
				break;
			Record( records, parent ).CopyTo( Record( records, hole ) );
			hole = parent;
		}
		value.CopyTo( Record( records, hole ) );
	}

	private static Span<uint> Record( Span<uint> records, int index ) => records.Slice( index * Stride, SemanticWords );
	private static float Key( ReadOnlySpan<uint> record ) => BitConverter.UInt32BitsToSingle( record[ 15 ] );
	private static bool Less( Span<uint> records, int left, int right ) => Key( Record( records, left ) ) < Key( Record( records, right ) );
}
