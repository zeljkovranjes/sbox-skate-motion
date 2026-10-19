namespace SkateMotion;

/// <summary>Last-substep contact geometry in metres, with the surface friction coefficient used by the solve.</summary>
public readonly record struct SkateContactState(
	SkateBodyPart Part, Vector3 BoardPoint, Vector3 SurfacePoint, Vector3 Normal, float StaticFriction );
