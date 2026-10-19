namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Probe-frame geometry and fallback endpoints for the two far cross-section probes.</summary>
internal readonly record struct GrindSurfaceSection(
	Vector3 Center, Vector3 Up, Vector3 Direction, Vector3 FirstFarEnd, Vector3 SecondFarEnd );
