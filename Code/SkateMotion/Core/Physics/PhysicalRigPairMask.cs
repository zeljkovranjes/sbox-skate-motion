namespace SkateMotion.Core.Physics;

/// <summary>Canonical body-pair exclusions for either authored rig collision table.</summary>
internal readonly record struct PhysicalRigPairMask( bool Secondary, bool SuppressAll )
{
	internal const int BodyCount = 26;
	internal const int WordCount = 22;

	internal bool Excludes( int first, int second )
	{
		if ( (uint)first >= BodyCount )
			throw new ArgumentOutOfRangeException( nameof( first ), "Pair indices must name canonical rig bodies." );
		if ( (uint)second >= BodyCount )
			throw new ArgumentOutOfRangeException( nameof( second ), "Pair indices must name canonical rig bodies." );
		if ( SuppressAll )
			return true;
		var allowed = Secondary ? SecondaryRow( first ) : PrimaryRow( first );
		return (allowed & (1u << second)) == 0;
	}

	internal void Write( Span<uint> words )
	{
		if ( words.Length != WordCount )
			throw new ArgumentException( "A canonical pair table requires twenty-two words.", nameof( words ) );
		// Only the 676 body-pair bits belong to this table; preserve trailing padding.
		for ( var first = 0; first < BodyCount; ++first )
		{
			for ( var second = 0; second < BodyCount; ++second )
			{
				var bit = first * BodyCount + second;
				var mask = 1u << (bit & 31);
				if ( Excludes( first, second ) )
					words[ bit >> 5 ] |= mask;
				else
					words[ bit >> 5 ] &= ~mask;
			}
		}
	}

	private static uint PrimaryRow( int body ) => body switch
	{
		4 => (1u << 14) | (1u << 18) | (1u << 23),
		8 => (1u << 14) | (1u << 22) | (1u << 23),
		14 or 23 => (1u << 4) | (1u << 8),
		18 => 1u << 4,
		22 => 1u << 8,
		_ => 0u
	};

	private static uint SecondaryRow( int body ) => body switch
	{
		1 => (1u << 3) | (1u << 4) | (1u << 5) | (1u << 7) | (1u << 8) | (1u << 9) | (1u << 11),
		2 => (1u << 3) | (1u << 4) | (1u << 5) | (1u << 7) | (1u << 8) | (1u << 9),
		3 => (1u << 1) | (1u << 2) | (1u << 7) | (1u << 8) | (1u << 9) | (1u << 11) | (1u << 12)
			| (1u << 13) | (1u << 14) | (1u << 18) | (1u << 21) | (1u << 22),
		4 => (1u << 1) | (1u << 2) | (1u << 7) | (1u << 8) | (1u << 9) | (1u << 11) | (1u << 12)
			| (1u << 13) | (1u << 14) | (1u << 18) | (1u << 22),
		5 => (1u << 1) | (1u << 2) | (1u << 7) | (1u << 8) | (1u << 9) | (1u << 11) | (1u << 12)
			| (1u << 13) | (1u << 14) | (1u << 21) | (1u << 22),
		7 => (1u << 1) | (1u << 2) | (1u << 3) | (1u << 4) | (1u << 5) | (1u << 11) | (1u << 12)
			| (1u << 13) | (1u << 14) | (1u << 17) | (1u << 18) | (1u << 22),
		8 => (1u << 1) | (1u << 2) | (1u << 3) | (1u << 4) | (1u << 5) | (1u << 11) | (1u << 12)
			| (1u << 13) | (1u << 14) | (1u << 18) | (1u << 22),
		9 => (1u << 1) | (1u << 2) | (1u << 3) | (1u << 4) | (1u << 5) | (1u << 11) | (1u << 12)
			| (1u << 13) | (1u << 14) | (1u << 17) | (1u << 18),
		11 => (1u << 1) | (1u << 3) | (1u << 4) | (1u << 5) | (1u << 7) | (1u << 8) | (1u << 9),
		12 or 13 or 14 => (1u << 3) | (1u << 4) | (1u << 5) | (1u << 7) | (1u << 8) | (1u << 9),
		16 => (1u << 20) | (1u << 21) | (1u << 22),
		17 => (1u << 7) | (1u << 9) | (1u << 20) | (1u << 21) | (1u << 22),
		18 => (1u << 3) | (1u << 4) | (1u << 7) | (1u << 8) | (1u << 9) | (1u << 20) | (1u << 21),
		20 => (1u << 16) | (1u << 17) | (1u << 18),
		21 => (1u << 3) | (1u << 5) | (1u << 16) | (1u << 17) | (1u << 18),
		22 => (1u << 3) | (1u << 4) | (1u << 5) | (1u << 7) | (1u << 8) | (1u << 16) | (1u << 17),
		_ => 0u
	};
}
