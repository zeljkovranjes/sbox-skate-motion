namespace SkateMotion.Core.Input;

/// <summary>One independently evaluated pattern collection on a stick lane.</summary>
internal sealed class GestureRecognitionSet
{
	private readonly GestureCommandIdentity[] _identities;
	internal int Lane { get; }
	internal GesturePatternMatcher Matcher { get; }
	internal GestureCommandIdentity Identity( int index ) => _identities[ index ];

	internal GestureRecognitionSet( int lane, GesturePattern[] patterns,
		GestureCommandIdentity[] identities, byte maximumGap )
	{
		ArgumentNullException.ThrowIfNull( patterns );
		ArgumentNullException.ThrowIfNull( identities );
		if ( lane != 0 && lane != 1 )
			throw new ArgumentOutOfRangeException( nameof( lane ) );
		if ( identities.Length != patterns.Length )
			throw new ArgumentException( "Each gesture path requires an identity.", nameof( identities ) );
		Lane = lane;
		Matcher = new( patterns, maximumGap );
		_identities = identities.AsSpan().ToArray();
		for ( var index = 0; index < _identities.Length; index++ )
			ArgumentNullException.ThrowIfNull( _identities[ index ] );
	}
}
