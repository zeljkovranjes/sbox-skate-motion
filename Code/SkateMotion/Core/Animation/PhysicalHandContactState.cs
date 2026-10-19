namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Retained hand-contact weight, contraction radius and projection modes.</summary>
internal sealed class PhysicalHandContactState
{
	internal float Weight { get; set; }
	internal Vector4 Radius { get; set; }
	internal ulong Flags { get; set; }
}
