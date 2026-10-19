namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Owner motion, axes and activation flags before selecting a hand request side.</summary>
internal readonly record struct PhysicalHandOwnerInput(
	ulong ActionFlags, ulong RigFlags, Vector3 Origin, Vector3 Velocity,
	Vector3 Normal, Vector3 Side, Vector3 Tangent );
