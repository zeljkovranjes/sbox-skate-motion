namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained board probe preparation and skeleton query state.</summary>
internal sealed class PhysicalBoardProbeState
{
	internal ulong SkeletonFlags { get; set; }
	internal bool ForwardEnabled { get; set; }
	internal Vector3 ForwardStart { get; set; }
	internal Vector3 ForwardEnd { get; set; }
	internal PhysicalBoardProbeCache Ground { get; } = new();
	internal PhysicalBoardProbeCache Forward { get; } = new();
}
