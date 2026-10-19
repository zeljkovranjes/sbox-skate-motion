namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Physical pose, feedback velocity and world-space inverse mass properties.</summary>
internal sealed class PhysicalBodyState
{
	internal PhysicalBodyQueueLink QueueLink { get; } = new();
	internal PhysicalBodyPartitions Partitions { get; set; }
	internal LimbRigidTransform Pose { get; set; }
	internal Vector4 Orientation { get; set; }
	internal Vector3 Velocity { get; set; }
	internal Vector3 AngularVelocity { get; set; }
	internal Vector3 LinearAccumulator { get; set; }
	internal Vector3 AngularAccumulator { get; set; }
	internal Vector3 DefaultLinearAccumulator { get; set; }
	internal int AccumulatorState { get; set; }
	internal int MotionMode { get; set; }
	internal float MotionMetric { get; set; }
	internal PhysicalBodyMass? Mass { get; set; }
	internal Vector3 InverseInertiaDiagonal { get; set; }
	internal Vector3 InverseInertiaOffDiagonal { get; set; }
	internal float InverseMass { get; set; }
}
