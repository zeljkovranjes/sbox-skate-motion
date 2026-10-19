namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Explicit physical observations in the common source coordinate basis.</summary>
internal readonly record struct AnimationContactHistoryInput(
	int ParentController, bool Active, bool ResetFrames, AnimationContactFrame Frame,
	Vector3 BodySide, Vector3 ReferenceAxis, Vector3 ReferencePosition, Vector3 Motion,
	Vector3 ContactDirection, Vector3 ContactPosition, Vector3 ContactNormal, Vector3 ContactSide,
	AnimationMovementContactData Contact );
