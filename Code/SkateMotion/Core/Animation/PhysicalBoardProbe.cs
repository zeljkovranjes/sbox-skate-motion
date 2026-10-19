namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>A query in physical world coordinates, before conversion to an engine's trace ABI.</summary>
internal readonly record struct PhysicalBoardProbe( Vector3 Start, Vector3 End, float Radius,
	int ExcludedActor, uint Filter, uint Flags, uint Auxiliary );
