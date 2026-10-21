namespace SkateMotion.Core.Camera;

using Vector3 = System.Numerics.Vector3;

/// <summary>Where the camera wants to be this frame, before the world is taken into account (metres, radians).</summary>
/// <param name="Base">The followed board or feet position.</param>
/// <param name="Focus">The point the camera orbits and is pulled in toward.</param>
/// <param name="LookTarget">The point the camera looks at: the focus, a little ahead of the travel.</param>
/// <param name="Yaw">Heading the camera looks along.</param>
/// <param name="Elevation">Angle of the camera above the focus, before craning over anything in the way.</param>
/// <param name="Distance">Wanted distance from the focus.</param>
/// <param name="Teleported">The subject was placed somewhere else: the camera starts again there.</param>
internal readonly record struct CameraAim( Vector3 Base, Vector3 Focus, Vector3 LookTarget, float Yaw, float Elevation,
	float Distance, bool Teleported )
{
	/// <summary>The camera position at an elevation and distance along this aim's heading.</summary>
	internal Vector3 At( float elevation, float distance ) => Focus + CameraMath.Boom( Yaw, elevation ) * distance;
}
