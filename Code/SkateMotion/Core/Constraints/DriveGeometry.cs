namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

internal readonly record struct DriveGeometry(
	int BodyA, int BodyB, Vector3 LocalAnchorA, Vector3 LocalAnchorB,
	Quaternion LocalFrameA, Quaternion LocalFrameB );
