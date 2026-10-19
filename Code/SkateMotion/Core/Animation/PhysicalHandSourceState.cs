namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained world contact and source edge, with independent activation and side.</summary>
internal sealed class PhysicalHandSourceState
{
	internal bool Enabled { get; set; }
	internal Vector3 Point { get; set; }
	internal int Side { get; set; }
	internal GrindEdgeRecord Edge { get; set; }
}
