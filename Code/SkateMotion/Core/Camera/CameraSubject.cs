namespace SkateMotion.Core.Camera;

using Vector3 = System.Numerics.Vector3;

/// <summary>One frame of what the camera follows, in metres, seconds and radians (+X forward, +Z up).</summary>
/// <param name="Mode">Riding, on foot or bailed.</param>
/// <param name="Board">The board as shown this frame.</param>
/// <param name="Rider">The rider as shown this frame: the feet on foot, the body in a bail.</param>
/// <param name="Velocity">The board's velocity while riding, the rider's otherwise.</param>
/// <param name="Facing">The board's yaw.</param>
/// <param name="Airborne">The board (on foot, the rider) is in the air.</param>
/// <param name="Grinding">The board is on a rail.</param>
/// <param name="Manual">The board is balanced on one axle.</param>
/// <param name="FocusHeight">Height of the point the camera looks at above the board or the feet.</param>
internal readonly record struct CameraSubject( CameraSubjectMode Mode, Vector3 Board, Vector3 Rider, Vector3 Velocity,
	float Facing, bool Airborne, bool Grinding, bool Manual, float FocusHeight );
