namespace SkateMotion.Core.Animation;

/// <summary>Retained intrusive-list links shared by physical bodies and partition sentinels.</summary>
internal sealed class PhysicalBodyQueueLink
{
	internal PhysicalBodyQueueLink Previous { get; set; }
	internal PhysicalBodyQueueLink Next { get; set; }

	internal PhysicalBodyQueueLink()
	{
		Previous = this;
		Next = this;
	}
}
