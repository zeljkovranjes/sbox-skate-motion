namespace SkateMotion.Core.Animation;

/// <summary>Retained optional turn request and inverse of the adopted offboard physical frame.</summary>
internal sealed class PhysicalLocomotionFrameState
{
	internal bool TurnEnabled { get; set; }
	internal float TurnAngle { get; set; }
	internal float TurnRate { get; set; }
	internal float TurnRemaining { get; set; }
	internal LimbFrame QueryAxes { get; set; }
	internal LimbRigidTransform WorldToModel { get; set; }
	internal bool FrameDirty { get; set; }
}
