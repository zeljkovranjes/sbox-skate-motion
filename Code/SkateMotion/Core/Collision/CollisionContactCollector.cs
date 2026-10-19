namespace SkateMotion.Core.Collision;

internal sealed class CollisionContactCollector : ICollisionContactCollector
{
	private const int RecordStart = 32;
	private const int RecordWords = 64;
	private const int InlineCapacity = 50;
	private const int FlagsWord = 0x328c / 4;
	private readonly uint[] _state;
	private readonly ICollisionContactOutput _output;

	internal CollisionContactCollector( uint[] state, ICollisionContactOutput output )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( state.Length < 0x3290 / 4 || state[ 0 ] > InlineCapacity )
			throw new ArgumentException( "Collector storage must hold its complete header and fifty contact records.", nameof( state ) );
		_state = state;
		_output = output;
	}

	/// <summary>Allocates the next contact, reducing or flushing the current batch when required.</summary>
	public Span<uint> AllocateContact()
	{
		if ( _state[ 2 ] <= unchecked( _state[ 0 ] + _state[ 1 ] ) )
		{
			if ( !Exhausted )
			{
				if ( DeferredReduction )
					Reduce();
				if ( _state[ 2 ] <= unchecked( _state[ 0 ] + _state[ 1 ] ) )
					Exhausted = true;
			}
			if ( Exhausted )
			{
				_state[ 3 ] = unchecked( _state[ 3 ] + 1 );
				return Span<uint>.Empty;
			}
		}
		if ( _state[ 0 ] >= InlineCapacity )
		{
			if ( (_state[ FlagsWord ] & 0xff000000u) == 0 )
			{
				Exhausted = true;
				return Span<uint>.Empty;
			}
			Flush();
		}
		var index = (int)_state[ 0 ];
		_state[ 0 ]++;
		return _state.AsSpan( RecordStart + index * RecordWords, RecordWords );
	}

	/// <summary>Checks the newest contact for duplication while preserving its backing record.</summary>
	public void CompleteContact()
	{
		if ( CollisionContactReduction.RejectNewest( Records, (int)_state[ 0 ], _state[ 4 ],
			DeferredReduction, ref _state[ 3 ] ) )
			_state[ 0 ]--;
	}

	internal void Flush()
	{
		if ( _state[ 0 ] == 0 )
			return;
		if ( _output is null )
			throw new InvalidOperationException( "A streaming collector requires a contact output target." );
		if ( DeferredReduction )
			Reduce();
		_output.Write( Records[ ..((int)_state[ 0 ] * RecordWords) ] );
		_state[ 1 ] = unchecked( _state[ 1 ] + _state[ 0 ] );
		_state[ 0 ] = 0;
	}

	private void Reduce() => CollisionCollectorReduction.Reduce( Records, ref _state[ 0 ] );
	private Span<uint> Records => _state.AsSpan( RecordStart, InlineCapacity * RecordWords );
	private bool DeferredReduction => (_state[ FlagsWord ] & 0x00ff0000u) != 0;
	private bool Exhausted
	{
		get => (_state[ FlagsWord ] & 0x0000ff00u) != 0;
		set => _state[ FlagsWord ] = (_state[ FlagsWord ] & 0xffff00ffu) | (value ? 0x100u : 0u);
	}
}
