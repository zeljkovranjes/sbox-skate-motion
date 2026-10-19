using Sandbox;
using SkateMotion.Core;

namespace SkateMotion.Engine;

internal sealed class MapContacts
{
	private readonly GameObject _board;
	private readonly SkateSettings _settings;
	private readonly float _radius;
	private readonly BoardRig _rig;
	private readonly PhysicsBody[] _surfaces = new PhysicsBody[ 4 ];

	internal MapContacts( GameObject board, SkateSettings settings, float radius, BoardRig rig )
	{
		_board = board;
		_settings = settings;
		_radius = radius;
		_rig = rig;
	}

	internal void Sample( Span<WheelContact> contacts )
	{
		var up = _board.WorldRotation.Up;
		var startLift = 0.03f * SkateUnits.InchesPerMetre;
		var travel = (_settings.RideHeight + 0.04f - _radius) * SkateUnits.InchesPerMetre;
		for ( var i = 0; i < 4; i++ )
		{
			var offset = _rig.Origins[ i ];
			var origin = _board.WorldPosition + _board.WorldRotation * offset;
			var trace = _board.Scene.Trace
				.Sphere( _radius * SkateUnits.InchesPerMetre, origin + up * startLift, origin - up * travel )
				.IgnoreGameObjectHierarchy( _board )
				.Run();

			_surfaces[ i ] = null;
			contacts[ i ] = default;
			if ( !trace.Hit || trace.StartedSolid )
				continue;

			var point = trace.HitPosition;
			var distance = Vector3.Dot( origin - point, up ) / SkateUnits.InchesPerMetre;
			var surface = trace.Body;
			var velocity = surface.IsValid() ? surface.GetVelocityAtPoint( point ) : Vector3.Zero;
			var friction = trace.Surface is null ? 1f : Math.Clamp( trace.Surface.Friction, 0f, 4f );
			contacts[ i ] = new( true, SkateUnits.Metres( point ), SkateUnits.ToCore( trace.Normal ),
				SkateUnits.Metres( velocity ), distance, friction );
			_surfaces[ i ] = surface;
		}
	}

	internal void ApplyReaction( int index, WheelForce wheel )
	{
		var surface = _surfaces[ index ];
		if ( wheel.Load > 0f && surface.IsValid() && surface.MotionEnabled )
			surface.ApplyForceAt( SkateUnits.Inches( wheel.Point ), -SkateUnits.Inches( wheel.Force ) );
	}
}
