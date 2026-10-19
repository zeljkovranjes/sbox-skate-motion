namespace SkateMotion.Core.Animation;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

internal sealed class PhysicalLocomotionOwnerState
{
	internal Matrix4x4 Frame { get; set; }
	internal Matrix4x4 SecondaryFrame { get; set; }
	internal PhysicalLocomotionMotionState Solved { get; set; }
	internal PhysicalLocomotionContactReferenceState Contact { get; set; }
	internal Vector4 AngularHistory { get; set; }
	internal Vector3 Feedback { get; set; }
	internal Vector3 Movement { get; set; }
	internal Vector3 AdditionalMovement { get; set; }
	internal Vector3 AccelerationMovement { get; set; }
	internal Vector3 SmoothedUp { get; set; }
	internal Vector3 SecondaryCorrection { get; set; }
	internal Matrix4x4 TargetFrame { get; set; }
	internal Vector4 Heading { get; set; }
	internal Vector4 Timers { get; set; }
	internal float MovementLength { get; set; }
	internal bool ReferenceActive { get; set; }
	internal bool SecondaryCorrectionActive { get; set; }
	internal bool RejectionActive { get; set; }
}
