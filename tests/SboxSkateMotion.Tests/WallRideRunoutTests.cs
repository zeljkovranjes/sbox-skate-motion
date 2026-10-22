using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WallRideRunoutTests
{
	private const float Step = 1f / 240f;

	// A wall 0.3 m ahead along +x, facing back along -x, a metre up where the board meets it.
	private static readonly WallSight Wall = new( new Vector3( 0.3f, 0f, 1f ), -Vector3.UnitX, 1f );

	[Fact]
	public void TheWallBeingRiddenMeetingTheBoardSideOnIsNotACrash()
	{
		// Into the wall at 1.5 m/s along its face at 5: side-on, it is a crash (a runout upright) unless it is the wall the
		// board is turning to for a wall ride.
		var board = Board( new Vector3( 1.5f, 5f, 0f ) );
		var contact = new[] { Contact( -Vector3.UnitX ) };
		Assert.True( LandingSafety.RequiresBail( board, contact, 8f, Step, out _, out _ ) );
		Assert.False( LandingSafety.RequiresBail( board, contact, 8f, Step, out _, out _, wall: -Vector3.UnitX ) );
		// Another surface is still a crash.
		Assert.True( LandingSafety.RequiresBail( board, contact, 8f, Step, out _, out _, wall: Vector3.UnitY ) );
	}

	[Fact]
	public void SlammingIntoTheWallBeingRiddenStillBails()
	{
		var board = Board( new Vector3( 9f, 5f, 0f ) );
		Assert.True( LandingSafety.RequiresBail( board, new[] { Contact( -Vector3.UnitX ) }, 8f, Step, out _, out _,
			wall: -Vector3.UnitX ) );
	}

	[Fact]
	public void TheWallIsKnownWhileTurningToItAndRidingIt()
	{
		var ride = new WallRide();
		Assert.Null( ride.Wall );
		ride.Air( new BoardBody( Vector3.Zero, Quaternion.Identity, new Vector3( 1.5f, 5f, 0f ), Vector3.Zero, Vector3.One ),
			Wall, Step );
		Assert.Equal( -Vector3.UnitX, ride.Wall );
		// A surface that is not steep ends it.
		ride.OnSurface( new BoardBody( Vector3.Zero, Quaternion.Identity, Vector3.UnitY, Vector3.Zero, Vector3.One ),
			Vector3.UnitZ, Step );
		Assert.Null( ride.Wall );
	}

	[Fact]
	public void ABoardTurningOntoAWallIsNotRunOutByIt()
	{
		// In the air heading for the wall at a glancing angle (the wall ride turns to it), then its side meets the wall:
		// before, the rider stepped off and ran it out.
		var pilot = new BoardPilot( SkateSettings.RecoveredDefaults );
		var board = Board( new Vector3( 1.5f, 5f, 0f ) );
		pilot.Apply( board, [], default, Step, wall: Wall );
		var result = pilot.Apply( board, new[] { Contact( -Vector3.UnitX ) }, default, Step, wall: Wall );
		Assert.False( result.Runout );
		Assert.NotEqual( MotionMode.Bailed, result.Mode );
	}

	[Fact]
	public void ABoardMeetingAWallItIsNotTurningToStillRunsOut()
	{
		// With no wall ride under way (the wall not seen ahead) the same side-on hit is a runout, as before.
		var pilot = new BoardPilot( SkateSettings.RecoveredDefaults );
		var board = Board( new Vector3( 1.5f, 5f, 0f ) );
		pilot.Apply( board, [], default, Step );
		Assert.True( pilot.Apply( board, new[] { Contact( -Vector3.UnitX ) }, default, Step ).Runout );
	}

	[Fact]
	public void AMomentOffTheWallIsStillTheWallRide()
	{
		// Riding the wall, a bounce off it for a few steps keeps the ride (and pulls the board back toward the wall);
		// off it for longer, the board leaves it.
		var ride = new WallRide();
		var body = new BoardBody( Vector3.Zero, Quaternion.Identity, new Vector3( 1.5f, 5f, 0f ), Vector3.Zero, Vector3.One );
		ride.Air( body, Wall, Step );
		ride.OnSurface( body, -Vector3.UnitX, Step );
		Assert.True( ride.Riding );
		var pulled = Vector3.Zero;
		for ( var t = 0f; t < WallRide.LooseHold - 0.02f; t += Step )
			pulled += ride.Air( body, Wall, Step ).Lift;
		Assert.True( ride.Riding );
		Assert.True( pulled.X > 0f );
		ride.OnSurface( body, -Vector3.UnitX, Step );
		Assert.True( ride.Riding );
		for ( var t = 0f; t < WallRide.LooseHold + 0.02f; t += Step )
			ride.Air( body, Wall, Step );
		Assert.False( ride.Riding );
	}

	[Theory]
	// Off a kicker with an ollie at its lip: 2.7 m/s along the wall, rising; a slow 2 m/s still is not ridden.
	[InlineData( 0.7f, 2.7f, 0.5f, true )]
	[InlineData( 0.7f, 2f, 0.5f, false )]
	public void AWallMetOffAKickerOllieIsRidden( float into, float along, float up, bool rides )
	{
		var ride = new WallRide();
		ride.Air( new BoardBody( Vector3.Zero, Quaternion.Identity, new Vector3( into, along, up ), Vector3.Zero, Vector3.One ),
			Wall with { Point = new Vector3( 0.25f, 0f, 1f ) }, Step );
		Assert.Equal( rides, ride.Active );
	}

	private static ArticulatedBoard Board( Vector3 velocity )
	{
		var settings = new SkateSettings();
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - 0.0285f);
		var board = new ArticulatedBoard( settings, origins, 0.0285f );
		board.Reset( new Vector3( 0f, 0f, 1f ), Quaternion.Identity, velocity, Vector3.Zero );
		return board;
	}

	private static CollisionContact Contact( Vector3 normal ) => new(
		0, 7, new Vector3( 0f, 0f, 1f ), new Vector3( 0f, 0f, 1f ), normal,
		Vector3.UnitY, Vector3.Cross( normal, Vector3.UnitY ), 0.6f, 0.5f, 0f );
}
