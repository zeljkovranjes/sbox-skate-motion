namespace SkateMotion.Core.Animation;

/// <summary>A shared frame owner whose writes can also supply the board's source frame.</summary>
internal sealed class PhysicalFrameOwner
{
	internal LimbRigidTransform Frame { get; set; }
	internal ulong UpdateFlags { get; set; }
}
