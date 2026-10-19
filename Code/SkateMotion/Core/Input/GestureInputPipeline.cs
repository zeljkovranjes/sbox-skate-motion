namespace SkateMotion.Core.Input;

using Vector2 = System.Numerics.Vector2;

/// <summary>Processes one input notification, delivering lane holds before new matches.</summary>
internal sealed class GestureInputPipeline
{
	private readonly GestureRecognitionSet[] _sets;
	private readonly GesturePatternMatch[] _matches;
	private readonly bool[] _matched;
	private readonly GestureCommandReceiver _receiver;
	private readonly float _componentThreshold;

	internal int SetCount => _sets.Length;
	internal GestureCommandReceiver Receiver => _receiver;

	internal GestureInputPipeline( GestureRecognitionSet[] sets, GestureCommandReceiver receiver,
		float componentThreshold )
	{
		ArgumentNullException.ThrowIfNull( sets );
		ArgumentNullException.ThrowIfNull( receiver );
		if ( !float.IsFinite( componentThreshold ) || componentThreshold < 0f )
			throw new ArgumentOutOfRangeException( nameof( componentThreshold ) );
		_sets = sets.AsSpan().ToArray();
		for ( var index = 0; index < _sets.Length; index++ )
			ArgumentNullException.ThrowIfNull( _sets[ index ] );
		_matches = new GesturePatternMatch[ sets.Length ];
		_matched = new bool[ sets.Length ];
		_receiver = receiver;
		_componentThreshold = componentThreshold;
	}

	internal void Notify( ControllerReading reading, int player, int mode,
		QueuedInputCommand[] commands, ref int commandCount )
	{
		ArgumentNullException.ThrowIfNull( commands );
		if ( commandCount < 0 || commandCount > commands.Length )
			throw new ArgumentOutOfRangeException( nameof( commandCount ) );
		// Check the largest possible append before changing any recognizer state.
		if ( commands.Length - commandCount < _sets.Length )
			throw new InvalidOperationException( "Gesture command storage has insufficient capacity." );
		ProcessLane( new( reading.RightX, -reading.RightY ), 1, player, mode, commands, ref commandCount );
		ProcessLane( new( reading.LeftX, -reading.LeftY ), 0, player, mode, commands, ref commandCount );
	}

	private void ProcessLane( Vector2 sample, int lane, int player, int mode,
		QueuedInputCommand[] commands, ref int commandCount )
	{
		sample.X = MathF.Abs( sample.X ) < _componentThreshold ? 0f : sample.X;
		sample.Y = MathF.Abs( sample.Y ) < _componentThreshold ? 0f : sample.Y;
		for ( var index = 0; index < _sets.Length; index++ )
		{
			var set = _sets[ index ];
			if ( set.Lane != lane )
				continue;
			_matched[ index ] = set.Matcher.Step( sample, mode, out _matches[ index ] );
			if ( set.Matcher.HeldPattern >= 0 )
				_receiver.ReceiveHold( player, set.Identity( set.Matcher.HeldPattern ) );
		}
		for ( var index = 0; index < _sets.Length; index++ )
		{
			var set = _sets[ index ];
			if ( set.Lane != lane || !_matched[ index ] )
				continue;
			var match = _matches[ index ];
			if ( _receiver.ReceiveMatch( player, set.Identity( match.PatternIndex ), match.Quality, out var command ) )
				commands[ commandCount++ ] = command;
		}
	}
}
