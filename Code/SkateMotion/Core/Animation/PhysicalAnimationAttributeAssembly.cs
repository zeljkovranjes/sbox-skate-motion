namespace SkateMotion.Core.Animation;

/// <summary>Combines queued scalars and sampled records in physical-consumer order.</summary>
internal sealed class PhysicalAnimationAttributeAssembly
{
	private readonly PhysicalAnimationAttribute[] _storage;
	internal int Count { get; private set; }
	internal ReadOnlySpan<PhysicalAnimationAttribute> Values => _storage.AsSpan( 0, Count );

	internal PhysicalAnimationAttributeAssembly( int capacity )
	{
		if ( capacity < 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ) );
		_storage = new PhysicalAnimationAttribute[ capacity ];
	}

	internal void Gather( ReadOnlySpan<PhysicalAnimationAttribute> queued,
		ReadOnlySpan<AnimationAttributeQueryResult> sampled )
	{
		if ( sampled.Length > _storage.Length || queued.Length > _storage.Length - sampled.Length )
			throw new InvalidOperationException( "The caller must provide capacity for queued and sampled attributes." );
		var count = 0;
		foreach ( var attribute in queued )
			_storage[ count++ ] = new( attribute.Name, attribute.Value, 0, 6 );
		foreach ( var attribute in sampled )
		{
			var payload = attribute.Payload;
			var value = attribute.Kind == 0 ? payload.Scalar
				: attribute.Kind == 3 ? BitConverter.UInt32BitsToSingle( payload.F ) : 0f;
			var bone = attribute.Kind == 3
				? new AnimationAttributeKey( payload.A, payload.B, payload.C, payload.D, payload.E ) : default;
			_storage[ count++ ] = new( attribute.Name, value, attribute.Kind, attribute.Flags, bone );
		}
		Count = count;
	}

	internal void Update( PhysicalAnimationAttributeState state, PhysicalAnimationAttributeInput input,
		ReadOnlySpan<PhysicalAnimationAttribute> queued, ReadOnlySpan<AnimationAttributeQueryResult> sampled )
	{
		Gather( queued, sampled );
		PhysicalAnimationAttributeProcessor.UpdateAttributes( state, input, Values );
	}
}
