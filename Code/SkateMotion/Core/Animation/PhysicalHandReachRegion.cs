namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Local motion and four apex points defining the hand's edge-selection region.</summary>
internal readonly record struct PhysicalHandReachRegion(
	bool IsValid, Vector3 Motion, Vector3 FirstLower, Vector3 SecondLower,
	Vector3 FirstUpper, Vector3 SecondUpper );
