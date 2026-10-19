namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained local pin and the current and historical proximity flags.</summary>
internal sealed class PhysicalContactPin
{
	internal int Bone { get; set; }
	internal Vector3 LocalPosition { get; set; }
	internal Vector3 WorldPosition { get; set; }
	internal float RadiusSquared { get; set; }
	internal bool Near { get; set; }
	internal bool RetainedNear { get; set; }
}
