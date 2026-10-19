namespace SkateMotion.Core.Animation;

/// <summary>Orientation, timing and correction mode for a deck-driven character update.</summary>
internal readonly record struct PhysicalRidingCharacterInput(
	LimbRigidTransform OrientationSource, PhysicalRigInput RigInput,
	float PlacementDeltaTime, byte BoardCorrectionMode );
