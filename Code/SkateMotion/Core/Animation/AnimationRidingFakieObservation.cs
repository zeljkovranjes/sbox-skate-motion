namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Physical owner observations in their shared coordinate frame, before graph evaluation.</summary>
internal readonly record struct AnimationRidingFakieObservation(
	int MovementState,
	int ControllerCode,
	Vector3 ReferenceDirection,
	Vector3 BoardDirection,
	Vector3 AlternateDirection,
	float Speed );
