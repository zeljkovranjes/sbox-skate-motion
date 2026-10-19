namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Preserves a physical frame and its paired motion observations for contact history.</summary>
internal readonly record struct AnimationContactFrame( Vector3 Forward, Vector3 Position,
	Vector3 Auxiliary, Vector3 CharacterFirst, Vector3 CharacterSecond,
	Vector3 MotionFirst, Vector3 MotionSecond )
{
	internal static AnimationContactFrame Initial => new( Vector3.UnitX, Vector3.Zero,
		Vector3.Zero, Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitX, Vector3.UnitX );
}
