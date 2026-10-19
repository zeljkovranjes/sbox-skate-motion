namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Actor state and retained reference frames used by physical contact input.</summary>
internal readonly record struct PhysicalContactActorState(
	uint State, uint ParentState, ulong ActionBits, ulong MotionBits,
	Vector3 Position, Vector3 AlternatePosition, Vector3 SpecialPosition, Vector3 SpecialNormal );
