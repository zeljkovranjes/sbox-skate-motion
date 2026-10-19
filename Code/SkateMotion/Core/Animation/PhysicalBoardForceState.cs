namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Board response timers and relative rotation retained by the non-launch motor.</summary>
internal sealed class PhysicalBoardForceState
{
	internal float SmoothedResponse { get; set; }
	internal float FirstTimer { get; set; }
	internal float SecondTimer { get; set; }
	internal float FirstResponse { get; set; }
	internal float SecondResponse { get; set; }
	internal Vector4 RelativeRotation { get; set; }
	internal bool HasRelativeRotation { get; set; }
}
