using Sandbox;
using SkateMotion.Core.Camera;

namespace SkateMotion.Engine;

/// <summary>
/// Third-person skate camera: reads the board and rider as shown this frame, lets the Core director decide the aim,
/// then fits it to the world (looking down into drops ahead, craning over and pulling in for anything in the way)
/// and places the camera. The mouse (or a free right stick) orbits it; while riding it eases back behind the board
/// once the mouse rests, and on foot it stays where the player turned it, so walking directions stay put relative
/// to the view.
/// </summary>
internal sealed class SkateCameraRig
{
	// The focus sits at this share of the rider's height above the board or feet (inches when there is no rider).
	private const float FocusShare = 0.64f;
	private const float LoneFocus = 40f;

	// Head-height sweep start, as a share of the focus height above it; the feet start this far up (inches).
	private const float HeadShare = 0.45f;
	private const float FeetHeight = 6f;

	// Least clearance (inches) the camera keeps from the world, the margin kept beyond the near plane's corners, and
	// how much wider the early pull-in probe is.
	private const float LeastClearance = 5f;
	private const float NearMargin = 0.5f;
	private const float SoftScale = 1.75f;

	// Highest the camera cranes over something in the way (radians).
	private const float HighestCrane = 75f * CameraMath.Degrees;

	private readonly CameraDirector _director = new();
	private readonly CameraCollision _world = new();
	private readonly CameraDropProbe _drop = new();

	/// <param name="distance">Follow distance (inches).</param>
	/// <param name="height">Camera height above the focus (inches).</param>
	/// <param name="focusHeight">Focus height above the board or feet (inches); zero takes it from the rider.</param>
	/// <param name="look">This frame's orbit input, in degrees.</param>
	internal void Update( CameraComponent camera, SkateCameraTarget target, float distance, float height, float focusHeight,
		float recentreDelay, Angles look, float delta )
	{
		_world.Ignore( camera.Scene, target.Ignored );
		var subject = Read( target, focusHeight );
		_director.Distance = distance / SkateUnits.InchesPerMetre;
		_director.Height = height / SkateUnits.InchesPerMetre;
		_director.Orbit.RecentreDelay = recentreDelay;
		var onFoot = subject.Mode != CameraSubjectMode.Riding;
		var aim = _director.Update( subject, LookForDrops( subject, target.State.WallRiding ), look.yaw * CameraMath.Degrees,
			look.pitch * CameraMath.Degrees, delta );
		var radius = Clearance( camera );
		var focus = SkateUnits.Inches( aim.Focus );
		var head = focus + Vector3.Up * (subject.FocusHeight * SkateUnits.InchesPerMetre * HeadShare);
		var feet = SkateUnits.Inches( aim.Base ) + Vector3.Up * FeetHeight;
		var wanted = aim.Distance * SkateUnits.InchesPerMetre;

		// Craning answers how blocked the usual place is; the camera then pulls in for whatever is still in the way.
		var usual = SkateUnits.Inches( aim.At( aim.Elevation, aim.Distance ) );
		var available = MathF.Min( _world.Clear( focus, head, feet, usual, radius ), wanted ) / SkateUnits.InchesPerMetre;
		var crane = _director.Boom.UpdateCrane( aim.Distance, available, onFoot, delta );
		var elevation = CameraMath.SoftClamp( aim.Elevation + crane, CameraDirector.LowestElevation, HighestCrane,
			CameraDirector.ElevationKnee );
		var place = SkateUnits.Inches( aim.At( elevation, aim.Distance ) );
		var soft = _world.Clear( focus, head, feet, place, radius * SoftScale ) / SkateUnits.InchesPerMetre;
		var hard = _world.Clear( focus, head, feet, place, radius ) / SkateUnits.InchesPerMetre;
		var settled = _director.Boom.UpdateDistance( aim.Distance, soft, hard, delta );

		var position = SkateUnits.Inches( aim.At( elevation, settled ) );
		var view = SkateUnits.Inches( aim.LookTarget ) - position;
		// Pressed right up to the focus, the view keeps the heading and elevation it had.
		if ( view.LengthSquared < 1f )
			view = -SkateUnits.ToEngine( CameraMath.Boom( aim.Yaw, elevation ) );
		camera.WorldPosition = position;
		camera.WorldRotation = Rotation.LookAt( view, Vector3.Up );
	}

	private static CameraSubject Read( SkateCameraTarget target, float focusHeight )
	{
		var mode = target.Mode switch
		{
			SkateRiderMode.Riding => CameraSubjectMode.Riding,
			SkateRiderMode.Bailed => CameraSubjectMode.Bailed,
			_ => CameraSubjectMode.OnFoot
		};
		var velocity = mode == CameraSubjectMode.Bailed ? Vector3.Zero : target.Velocity;
		var airborne = mode == CameraSubjectMode.OnFoot ? target.RiderAirborne : target.State.IsAirborne;
		var focus = focusHeight > 0f ? focusHeight : target.RiderHeight > 0f ? target.RiderHeight * FocusShare : LoneFocus;
		return new CameraSubject( mode, SkateUnits.Metres( target.Board.Position ), SkateUnits.Metres( target.Rider.Position ),
			SkateUnits.Metres( velocity ), target.Board.Rotation.Yaw() * CameraMath.Degrees, airborne,
			target.State.Grind != SkateGrind.None, target.State.Balance != SkateBalanceMode.None,
			focus / SkateUnits.InchesPerMetre );
	}

	// Drops are looked for ahead of the board, or of the feet on foot; a tumbling ragdoll, or a board riding along a
	// wall (the ground below it is no drop), looks for none.
	private CameraGround LookForDrops( CameraSubject subject, bool wallRiding )
	{
		if ( subject.Mode == CameraSubjectMode.Bailed || wallRiding )
			return CameraGround.None;
		var origin = subject.Mode == CameraSubjectMode.Riding ? subject.Board : subject.Rider;
		_drop.Run( _world, SkateUnits.Inches( origin ), SkateUnits.Inches( subject.Velocity ) );
		return new CameraGround( _drop.Distances, _drop.Grounds, _drop.Count, _drop.Below );
	}

	// The camera keeps the near plane's corners out of the world: their distance from the camera, from its field of
	// view and near clip distance.
	private static float Clearance( CameraComponent camera )
	{
		var half = MathF.Tan( camera.FieldOfView * 0.5f * CameraMath.Degrees );
		var aspect = Screen.Aspect > 0f ? Screen.Aspect : 16f / 9f;
		var (across, up) = camera.FovAxis == CameraComponent.Axis.Vertical ? (half * aspect, half) : (half, half / aspect);
		var corner = camera.ZNear * MathF.Sqrt( 1f + across * across + up * up );
		return MathF.Max( LeastClearance, corner + NearMargin );
	}
}
