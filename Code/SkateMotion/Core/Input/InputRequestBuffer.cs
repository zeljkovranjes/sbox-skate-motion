namespace SkateMotion.Core.Input;

/// <summary>Retains unique requests in insertion order; adding a duplicate preserves its value.</summary>
internal sealed class InputRequestBuffer
{
	private readonly InputRequest[] _requests;
	internal int Count { get; private set; }

	internal InputRequestBuffer( int capacity )
	{
		if ( capacity < 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ) );
		_requests = new InputRequest[ capacity ];
	}

	internal InputRequest this[ int index ]
	{
		get
		{
			if ( index < 0 || index >= Count )
				throw new ArgumentOutOfRangeException( nameof( index ) );
			return _requests[ index ];
		}
	}

	internal void Clear() => Count = 0;

	internal bool Add( InputCommandKey key, float value )
	{
		for ( var i = 0; i < Count; i++ )
		{
			if ( _requests[ i ].Key == key )
				return false;
		}
		if ( Count == _requests.Length )
			throw new InvalidOperationException( "The caller must provide capacity for all distinct input requests." );
		_requests[ Count++ ] = new( key, value );
		return true;
	}

	internal void Set( InputCommandKey key, float value )
	{
		for ( var i = 0; i < Count; i++ )
		{
			if ( _requests[ i ].Key == key )
			{
				_requests[ i ] = new( key, value );
				return;
			}
		}
		Add( key, value );
	}
}
