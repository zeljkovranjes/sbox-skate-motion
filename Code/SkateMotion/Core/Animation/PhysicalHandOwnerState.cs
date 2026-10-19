namespace SkateMotion.Core.Animation;

/// <summary>Retains side selection and consecutive direction mismatches across owner requests.</summary>
internal sealed class PhysicalHandOwnerState
{
	internal int RetryCount { get; set; }
	internal int RequestedSide { get; set; }
}
