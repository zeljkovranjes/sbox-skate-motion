namespace SkateMotion.Core.Animation;

/// <summary>Reusable registration, pairing and command storage for three authored pose streams.</summary>
internal sealed class AnimationPoseStreamContext
{
	internal const int SlotCount = 3;
	internal const float WeightMaximum = 1048575f;
	private readonly AnimationPoseStreamKey[] _names = new AnimationPoseStreamKey[ SlotCount ];
	private readonly int[] _pairedSlots = new int[ SlotCount ];
	private readonly uint[][] _commands = new uint[ SlotCount ][];
	private readonly int[] _counts = new int[ SlotCount ];

	internal AnimationPoseStreamContext( int commandCapacity )
	{
		if ( commandCapacity < 1 )
			throw new ArgumentOutOfRangeException( nameof( commandCapacity ) );
		Array.Fill( _pairedSlots, -1 );
		for ( var slot = 0; slot < SlotCount; slot++ )
			_commands[ slot ] = new uint[ commandCapacity ];
	}

	internal int NameCount { get; private set; }
	internal bool SwapPairs { get; set; }
	internal AnimationPoseStreamKey GetName( int slot ) => _names[ slot ];
	internal int GetPairedSlot( int slot ) => _pairedSlots[ slot ];
	internal ReadOnlySpan<uint> Commands( int slot ) => _commands[ slot ].AsSpan( 0, _counts[ slot ] );

	internal void Reset()
	{
		NameCount = 0;
		SwapPairs = false;
		Array.Clear( _counts );
	}

	internal int Find( AnimationPoseStreamKey name )
	{
		var slot = FindDirect( name );
		return slot < 0 || !SwapPairs ? slot : _pairedSlots[ slot ];
	}

	internal int Register( AnimationPoseStreamKey name, AnimationPoseStreamKey pairedName )
	{
		var existing = FindDirect( name );
		if ( existing >= 0 )
			return existing;
		var paired = name != pairedName;
		if ( NameCount + ( paired ? 2 : 1 ) > SlotCount )
			throw new InvalidOperationException( "Pose stream registration exceeds three names." );
		var slot = NameCount++;
		_names[ slot ] = name;
		_pairedSlots[ slot ] = slot;
		if ( paired )
		{
			_names[ NameCount ] = pairedName;
			_pairedSlots[ slot ] = NameCount;
			_pairedSlots[ NameCount ] = slot;
			NameCount++;
		}
		return slot;
	}

	internal void QueueBlend( float weight )
	{
		if ( !float.IsFinite( weight ) )
			throw new ArgumentOutOfRangeException( nameof( weight ) );
		var encoded = unchecked( (uint)(long)( weight * WeightMaximum ) );
		for ( var slot = 0; slot < NameCount; slot++ )
		{
			if ( _counts[ slot ] == 0 )
				continue;
			Append( slot, 0x40000000u | encoded );
			Combine( slot );
		}
	}

	internal void Append( int slot, uint word )
	{
		if ( _counts[ slot ] == _commands[ slot ].Length )
			throw new InvalidOperationException( "Pose commands exceed the allocated stream capacity." );
		_commands[ slot ][ _counts[ slot ]++ ] = word;
	}

	/// <summary>Queues one weight per sampled input and reduces the group in all three streams.</summary>
	internal void QueueBlendMany( ReadOnlySpan<float> weights )
	{
		if ( weights.Length < 1 || weights.Length > AnimationPoseMultiBlender.MaximumInputs )
			throw new ArgumentOutOfRangeException( nameof( weights ) );
		for ( var input = 0; input < weights.Length; input++ )
			if ( !float.IsFinite( weights[ input ] ) )
				throw new ArgumentOutOfRangeException( nameof( weights ) );

		for ( var input = 0; input < weights.Length; input++ )
		{
			var encoded = unchecked( (uint)(long)( weights[ input ] * WeightMaximum ) );
			for ( var slot = 0; slot < SlotCount; slot++ )
				Append( slot, 0x40000000u | encoded );
		}
		for ( var slot = 0; slot < SlotCount; slot++ )
			CombineMany( slot, weights.Length );
	}

	private void CombineMany( int slot, int inputs )
	{
		var words = _commands[ slot ];
		var lastValue = _counts[ slot ] - 1 - inputs;
		// Reduction requires a preceding word beyond the input group, even for equal values.
		var identical = lastValue >= inputs && words[ lastValue ] >> 30 == 0;
		for ( var input = 1; identical && input < inputs; input++ )
			identical = words[ lastValue - input ] == words[ lastValue ];
		if ( identical )
			_counts[ slot ] -= inputs == 2 ? 2 : inputs * 2 - 1;
		else
			Append( slot, 0x80000000u | ( (uint)inputs << 20 ) );
	}

	internal void Combine( int slot )
	{
		var words = _commands[ slot ];
		var head = _counts[ slot ] - 1;
		// This reduction examines the preceding window, not simply the last two words.
		if ( head - 2 > 1 && words[ head - 2 ] >> 30 == 0 && words[ head - 3 ] == words[ head - 2 ] )
			_counts[ slot ] -= 2;
		else
			Append( slot, 0x80200000u );
	}

	private int FindDirect( AnimationPoseStreamKey name )
	{
		for ( var slot = 0; slot < NameCount; slot++ )
			if ( _names[ slot ] == name )
				return slot;
		return -1;
	}
}
