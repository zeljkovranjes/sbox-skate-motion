namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>A pinned or releasing effector's persistent target state.</summary>
internal readonly record struct EffectorTargetState(
	int Mode, Vector3 Pin, Vector3 Release, Vector3 Offset, Vector3 Normal,
	float Weight, float OrientationWeight, bool OrientationRequested );
