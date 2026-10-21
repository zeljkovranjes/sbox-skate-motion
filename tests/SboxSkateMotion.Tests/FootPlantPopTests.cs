using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FootPlantPopTests
{
	[Fact]
	public void PushingOffWhileHoldingAGrabOnTheGroundPopsAFootPlant()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var pilot = new BoardPilot( settings );
		var board = Board( settings );
		var contacts = Ground( board );
		var held = pilot.Apply( board, contacts, new ControlInput( Grab: true ), 1f / 240f );
		Assert.Equal( TrickKind.None, held.Trick );
		var pushed = pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), 1f / 240f );
		Assert.Equal( TrickKind.Plant, pushed.Trick );
		// A push already held is a push, not another plant.
		var still = pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), 1f / 240f );
		Assert.Equal( TrickKind.None, still.Trick );
	}

	[Fact]
	public void PushingWithoutAGrabIsJustAPush()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var pilot = new BoardPilot( settings );
		var board = Board( settings );
		var contacts = Ground( board );
		pilot.Apply( board, contacts, default, 1f / 240f );
		Assert.Equal( TrickKind.None, pilot.Apply( board, contacts, new ControlInput( Push: 1f ), 1f / 240f ).Trick );
	}

	private static ArticulatedBoard Board( SkateSettings settings )
	{
		const float radius = 0.0285f;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
		var board = new ArticulatedBoard( settings, origins, radius );
		board.Reset( new Vector3( 0f, 0f, settings.RideHeight ), Quaternion.Identity, Vector3.UnitX * 2f, Vector3.Zero );
		return board;
	}

	// One contact under every wheel, pressed into flat ground.
	private static CollisionContact[] Ground( ArticulatedBoard board )
	{
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var wheel = board.Body( i + 3 ).Position;
			var point = new Vector3( wheel.X, wheel.Y, 0f );
			contacts[ i ] = new CollisionContact( i + 3, 7, point, point, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY,
				0.5f, 0.5f, 0f );
		}
		return contacts;
	}
}
