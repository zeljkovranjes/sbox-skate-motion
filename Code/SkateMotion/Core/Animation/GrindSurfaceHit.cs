namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>A collision result for one ordered surface probe, with packed material data.</summary>
internal readonly record struct GrindSurfaceHit(
	bool HasHit, Vector3 Position, Vector3 Normal, float Fraction, uint Surface );
