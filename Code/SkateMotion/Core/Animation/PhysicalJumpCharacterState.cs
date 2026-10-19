namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retains the current character launch result separately from its launch context.</summary>
internal sealed class PhysicalJumpCharacterState
{
	internal bool Launched { get; set; }
	internal Vector3 Velocity { get; set; }
}
