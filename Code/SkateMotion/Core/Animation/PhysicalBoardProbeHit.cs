namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Completed query fields consumed by the board's wheel and retained probe observations.</summary>
internal readonly record struct PhysicalBoardProbeHit(
	Vector3 Position, Vector3 Normal, float Fraction, uint PackedMaterial );
