namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct WheelContact(
	bool Hit, Vector3 Point, Vector3 Normal, Vector3 SurfaceVelocity, float Distance,
	float Grip = 1f, float Resistance = 1f );
