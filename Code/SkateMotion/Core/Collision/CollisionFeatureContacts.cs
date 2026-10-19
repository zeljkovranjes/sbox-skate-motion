namespace SkateMotion.Core.Collision;

internal static class CollisionFeatureContacts
{
	internal static void Assemble( Span<uint> first, Span<uint> second,
		ReadOnlySpan<uint> direction, Span<uint> contacts )
	{
		if ( first.Length < 144 || second.Length < 144 || direction.Length < 4 || contacts.Length < 136 )
			throw new ArgumentException( "Contact assembly requires complete feature, direction and contact buffers." );
		var firstKind = first[ 140 ];
		var secondKind = second[ 140 ];
		if ( !ValidKind( firstKind ) || !ValidKind( secondKind ) )
			throw new ArgumentException( "Unsupported contact feature kind." );
		if ( firstKind == 0 || secondKind == 0 )
			CollisionPointContacts.Assemble( first, second, direction, contacts );
		else if ( firstKind == 1 && secondKind == 1 )
			CollisionSegmentContacts.Assemble( first, second, direction, contacts );
		else if ( firstKind == 1 || secondKind == 1 )
			CollisionPolygonSegmentContacts.Assemble( first, second, direction, contacts );
		else
		{
			var specialized = (firstKind == 4 && secondKind is 3 or 4) || (firstKind == 3 && secondKind == 4);
			if ( !specialized || !CollisionFaceClipContacts.TryAssemble( first, second, direction, contacts ) )
				CollisionPolygonContacts.Assemble( first, second, contacts );
		}
	}

	private static bool ValidKind( uint kind ) => kind is 0 or 1 or 3 or 4 or 8;
}
