namespace SkateMotion.Core.Animation;

/// <summary>Shares the ordered physical scalar queue and animation parameters across graph behaviors.</summary>
internal sealed class AnimationScalarOutput
{
	private readonly PhysicalAnimationAttribute[] _queued;
	internal AnimationAttributeOverrideBuffer Attributes { get; }
	internal int QueuedCount { get; private set; }
	internal ReadOnlySpan<PhysicalAnimationAttribute> Queued => _queued.AsSpan( 0, QueuedCount );

	internal AnimationScalarOutput( AnimationAttributeOverrideBuffer attributes, int queueCapacity )
	{
		ArgumentNullException.ThrowIfNull( attributes );
		if ( queueCapacity < 0 )
			throw new ArgumentOutOfRangeException( nameof( queueCapacity ) );
		Attributes = attributes;
		_queued = new PhysicalAnimationAttribute[ queueCapacity ];
	}

	internal void Enqueue( AnimationAttributeKey name, float value )
	{
		if ( QueuedCount == _queued.Length )
			throw new InvalidOperationException( "The caller must provide capacity for all queued scalar outputs." );
		_queued[ QueuedCount++ ] = new( name, value );
	}

	internal void ResetQueued() => QueuedCount = 0;
}
