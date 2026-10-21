using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class KickTurnTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float Step = 1f / 240f;

	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void SteeringOverWhileStoppedPivotsTheBoardOnItsBackWheels( float steer )
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, 0f );
		var tail = board.Body( 2 ).Position;
		for ( var i = 0; i < 144; i++ )
		{
			Simulate( board, pilot, settings, new ControlInput( Steer: steer ) );
			if ( i % 12 == 0 )
				output.WriteLine( $"{i} spin {board.Deck.AngularVelocity.Z:F2} tail v {board.Body( 2 ).Velocity} com v {board.Deck.Velocity} wheels {board.Body( 5 ).Velocity}/{board.Body( 6 ).Velocity} turning {pilot.KickTurning}" );
		}
		var heading = MathF.Atan2( board.Deck.Forward.Y, board.Deck.Forward.X );
		var moved = Vector3.Distance( board.Body( 2 ).Position, tail );
		output.WriteLine( $"steer {steer}: heading {heading}, back truck moved {moved}, up {board.Deck.Up}" );
		// Steering left (positive) turns the nose left, about the back wheels, and the board stays upright.
		Assert.True( heading * steer > 1f );
		Assert.InRange( moved, 0f, 0.08f );
		Assert.True( board.Deck.Up.Z > 0.95f );
	}

	[Theory]
	[InlineData( 0.2f, 0f )]
	[InlineData( 1f, 3f )]
	public void ALightTouchOrRidingAlongIsNoKickTurn( float steer, float speed )
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, speed );
		var turning = false;
		for ( var i = 0; i < 60; i++ )
		{
			Simulate( board, pilot, settings, new ControlInput( Steer: steer ) );
			turning |= pilot.KickTurning;
		}
		Assert.False( turning );
	}

	[Fact]
	public void AKickTurnStartsSlowAndCarriesOnALittleFaster()
	{
		var turn = new KickTurn();
		Assert.False( turn.Step( 1.2f, 1f, true ) );
		Assert.True( turn.Step( 0.8f, 1f, true ) );
		Assert.True( turn.Step( 1.2f, 1f, true ) );
		Assert.False( turn.Step( 1.2f, 0.1f, true ) );
		Assert.False( turn.Step( 0.5f, 1f, false ) );
	}

	private static (ArticulatedBoard Board, BoardPilot Pilot) Create( SkateSettings settings, float speed )
	{
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		board.Reset( new Vector3( 0f, 0f, settings.RideHeight ), Quaternion.Identity, Vector3.UnitX * speed, Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		return (board, new BoardPilot( settings ));
	}

	// Flat ground under every wheel, at the wheel's own grip; while the board kick turns the wheels keep only the share
	// of it they keep in a powerslide (the board's contacts do the same).
	private static void Simulate( ArticulatedBoard board, BoardPilot pilot, SkateSettings settings, ControlInput input )
	{
		board.BeginStep();
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var grip = settings.Grip * (pilot.KickTurning ? 0.35f : 1f);
			var wheel = board.Body( i + 3 ).Position;
			var bottom = new Vector3( wheel.X, wheel.Y, wheel.Z - Radius );
			var ground = new Vector3( wheel.X, wheel.Y, 0f );
			contacts[ i ] = new CollisionContact( i + 3, 7, bottom, ground, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY,
				grip, grip * 0.8f, 0f );
		}
		pilot.Apply( board, contacts, input, Step );
		board.Step( contacts, 0f, 0f, Step );
	}
}
