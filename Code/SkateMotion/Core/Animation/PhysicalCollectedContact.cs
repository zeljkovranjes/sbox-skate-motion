namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Owner-oriented contact and its source index in the current solved stream.</summary>
internal readonly record struct PhysicalCollectedContact(
	int SourceIndex, Vector4 Normal, Vector4 Point, Vector3 RelativeVelocity,
	PhysicalContactOwner OtherOwner, PhysicalRiderContact RiderContact );
