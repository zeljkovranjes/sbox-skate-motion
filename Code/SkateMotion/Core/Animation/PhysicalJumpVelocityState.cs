namespace SkateMotion.Core.Animation;

/// <summary>Launch state retained independently of the per-frame physical attribute list.</summary>
internal sealed class PhysicalJumpVelocityState
{
	internal bool Triggered { get; set; }
	internal int HoldFrames { get; set; }
	internal float Cooldown { get; set; }
}
