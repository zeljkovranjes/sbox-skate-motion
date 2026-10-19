namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Current contact geometry, activation metric and producer-owned request flags.</summary>
internal readonly record struct PhysicalHandContactInput(
	LimbRigidTransform ModelToWorld, Vector3 Point, Vector4 Metric, ulong ObjectFlags, ulong RequestFlags );
