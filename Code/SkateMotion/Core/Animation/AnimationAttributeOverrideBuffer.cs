namespace SkateMotion.Core.Animation;

/// <summary>Updates the first matching owner/key while retaining unrelated payload and metadata.</summary>
internal sealed class AnimationAttributeOverrideBuffer
{
	private readonly AnimationAttributeOverride[] _storage;
	internal int Count { get; private set; }
	internal ReadOnlySpan<AnimationAttributeOverride> Values => _storage.AsSpan( 0, Count );

	internal AnimationAttributeOverrideBuffer( int capacity, ReadOnlySpan<AnimationAttributeOverride> initial )
	{
		if ( capacity < initial.Length )
			throw new ArgumentOutOfRangeException( nameof( capacity ) );
		_storage = new AnimationAttributeOverride[ capacity ];
		initial.CopyTo( _storage );
		Count = initial.Length;
	}

	internal void Flush( IAnimationAttributeOverrideConsumer consumer )
	{
		if ( Count == 0 )
			return;
		ArgumentNullException.ThrowIfNull( consumer );
		consumer.UpdateOverrides( Values );
		Count = 0;
	}

	internal void WriteScalar( AnimationAttributeKey name, float value, bool normalized, int owner )
	{
		for ( var index = 0; index < Count; index++ )
		{
			var current = _storage[ index ];
			if ( current.Attribute.Name != name || current.Owner != owner )
				continue;
			var payload = current.Attribute.Payload with { A = BitConverter.SingleToUInt32Bits( value ) };
			_storage[ index ] = current with
			{
				Attribute = current.Attribute with { Payload = payload },
				Normalized = normalized
			};
			return;
		}
		if ( Count == _storage.Length )
			throw new InvalidOperationException( "The caller must provide capacity for distinct attribute owners and names." );
		_storage[ Count++ ] = new( new( new( BitConverter.SingleToUInt32Bits( value ), 0, 0, 0, 0, 0 ),
			name, -1f, -1f, 6, 0 ), owner, normalized );
	}
}
