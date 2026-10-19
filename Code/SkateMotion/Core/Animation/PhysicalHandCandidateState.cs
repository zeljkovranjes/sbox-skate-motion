namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Retained candidate output and independent trajectory/frame caches.</summary>
internal sealed class PhysicalHandCandidateState
{
	internal byte Pending { get; set; }
	internal Vector3 Point { get; set; }
	internal ulong Flags { get; set; }
	internal GrindTrajectoryState Trajectory { get; set; }
	internal GrindTrajectoryState FirstTrajectory { get; set; }
	internal GrindTrajectoryState SecondTrajectory { get; set; }
	internal GrindEdgeRecord Edge { get; set; }
	internal Vector3 Normal { get; set; }
	internal Vector3 Down { get; set; }
	internal Vector3 ReferenceNormal { get; set; }
	internal LimbRigidTransform StartFrame { get; set; }
	internal LimbRigidTransform ApexFrame { get; set; }
	internal LimbRigidTransform ContactFrame { get; set; }
	internal Vector3 OppositeSide { get; set; }
	internal float StartTime { get; set; }
	internal float ApexTime { get; set; }
	internal Vector4 Metric { get; set; }
	internal float SideSign { get; set; }
}
