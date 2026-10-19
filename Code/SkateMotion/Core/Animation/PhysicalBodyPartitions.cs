namespace SkateMotion.Core.Animation;

/// <summary>World-owned physical mode partitions; numerical modes retain their storage meaning.</summary>
internal sealed class PhysicalBodyPartitions
{
	internal PhysicalBodyQueueLink ModeTwoHead { get; } = new();
	internal PhysicalBodyQueueLink ModeFourHead { get; } = new();
	internal int ModeTwoCount { get; set; }
	internal int ModeFourCount { get; set; }
	internal int Epoch { get; set; }
}
