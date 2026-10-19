namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Direction-phase inputs after character publication.</summary>
internal readonly record struct PhysicalDirectionInput(
	Vector3 Direction, Vector3 SecondaryDirection, float Blend,
	bool Mirrored, bool UseControl, float Control );
