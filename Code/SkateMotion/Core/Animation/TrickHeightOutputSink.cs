namespace SkateMotion.Core.Animation;

/// <summary>Queues every jump override before publishing a normalized height parameter.</summary>
internal sealed class TrickHeightOutputSink
{
	private static readonly AnimationAttributeKey JumpHeightOverride = AnimationAttributeKey.Encode( "JUMPHEIGHTOVERRIDE" );
	private readonly AnimationScalarOutput _output;
	private readonly Func<int, AnimationAttributeKey> _outputName;
	internal AnimationAttributeOverrideBuffer Attributes => _output.Attributes;
	internal int QueuedCount => _output.QueuedCount;
	internal ReadOnlySpan<PhysicalAnimationAttribute> Queued => _output.Queued;

	internal TrickHeightOutputSink( AnimationAttributeOverrideBuffer attributes, int queueCapacity,
		Func<int, AnimationAttributeKey> outputName )
		: this( new AnimationScalarOutput( attributes, queueCapacity ), outputName )
	{
	}

	internal TrickHeightOutputSink( AnimationScalarOutput output, Func<int, AnimationAttributeKey> outputName )
	{
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( outputName );
		_output = output;
		_outputName = outputName;
	}

	internal void Publish( int definition, TrickHeightResult result )
	{
		_output.Enqueue( JumpHeightOverride, result.JumpHeightOverride );
		Attributes.WriteScalar( _outputName( definition ), result.Height, true, -1 );
	}

	internal void ResetQueued() => _output.ResetQueued();
}
