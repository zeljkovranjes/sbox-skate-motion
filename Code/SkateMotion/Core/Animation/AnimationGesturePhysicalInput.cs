namespace SkateMotion.Core.Animation;

/// <summary>Completed physical fields consumed independently during channel selection and scalar publication.</summary>
internal readonly record struct AnimationGesturePhysicalInput(
	uint Mode, bool Gate, bool HoldingBoard, bool ForceUnitDistance, float Distance );
