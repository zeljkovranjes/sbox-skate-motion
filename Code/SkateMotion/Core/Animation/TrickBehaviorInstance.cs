namespace SkateMotion.Core.Animation;

internal sealed class TrickBehaviorInstance
{
	internal int References { get; set; } = 1;
	internal int ExternalHandle { get; init; } = -1;
	internal TrickContinuationMonitor Monitor { get; init; }
}
