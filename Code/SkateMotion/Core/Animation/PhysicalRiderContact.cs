namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>One ordered body contact with canonical bone and surface classification.</summary>
internal readonly record struct PhysicalRiderContact(
	int Bone, Vector3 Normal, Vector3 Point, int SurfaceFlags, int OtherKind,
	bool SelectSecondBody, PhysicalContactBody FirstBody, PhysicalContactBody SecondBody,
	Vector3 Impulse, int? OtherIdentifier = null );
