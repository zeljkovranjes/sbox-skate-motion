namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Displacement publication and elapsed time retained by the owner state phase.</summary>
internal sealed class PhysicalBoardOwnerPhaseState
{
	internal Vector3 PredictedDisplacement { get; set; }
	internal float Elapsed { get; set; }
}
