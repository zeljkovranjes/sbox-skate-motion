namespace SkateMotion.Core.Animation;

/// <summary>Orientation and timing for the grounded physical character update.</summary>
internal readonly record struct PhysicalGroundCharacterInput(
	LimbRigidTransform OrientationSource, PhysicalRigInput RigInput, float PlacementDeltaTime );
