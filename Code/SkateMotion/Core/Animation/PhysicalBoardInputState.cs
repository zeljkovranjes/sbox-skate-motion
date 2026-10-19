namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Published physical board frames and a heading retained by the board owner.</summary>
internal sealed class PhysicalBoardInputState
{
	internal LimbRigidTransform PhysicalFrame { get; set; }
	internal LimbRigidTransform StanceFrame { get; set; }
	internal LimbRigidTransform InverseStanceFrame { get; set; }
	internal LimbFrame StanceAxes { get; set; }
	internal Vector3 HorizontalDirection { get; set; }
	internal Vector3 PerpendicularDirection { get; set; }
	internal Vector3 SignedVelocityDirection { get; set; }
	internal Vector3 TravelDirection { get; set; }
	internal Vector3 Heading { get; set; }
	internal float Speed { get; set; }
	internal float StanceSign { get; set; }
	internal float Mass { get; set; }
}
