namespace SkateMotion.Core.Input;

/// <summary>Turns owned match notifications into queued actions and retained hold requests.</summary>
internal sealed class GestureCommandReceiver
{
	private readonly int _player;
	private readonly GestureCommandIdentity[] _holdPatterns;
	internal GestureCommandIdentity TrackedPattern { get; private set; }
	internal bool HoldPending { get; private set; }

	internal GestureCommandReceiver( int player, GestureCommandIdentity[] holdPatterns )
	{
		ArgumentNullException.ThrowIfNull( holdPatterns );
		_player = player;
		_holdPatterns = holdPatterns.AsSpan().ToArray();
		for ( var index = 0; index < _holdPatterns.Length; index++ )
			ArgumentNullException.ThrowIfNull( _holdPatterns[ index ] );
	}

	internal bool ReceiveMatch( int player, GestureCommandIdentity identity, float quality,
		out QueuedInputCommand command )
	{
		command = default;
		if ( player != _player )
			return false;
		ArgumentNullException.ThrowIfNull( identity );
		command = new( 3, identity.Key, quality );
		for ( var index = 0; index < _holdPatterns.Length; index++ )
		{
			if ( _holdPatterns[ index ].Matches( identity ) )
			{
				TrackedPattern = identity;
				break;
			}
		}
		return true;
	}

	internal void ReceiveHold( int player, GestureCommandIdentity identity )
	{
		if ( player == _player && TrackedPattern is not null && TrackedPattern.Matches( identity ) )
			HoldPending = true;
	}

	internal bool ConsumeHold()
	{
		var result = HoldPending;
		HoldPending = false;
		return result;
	}
}
