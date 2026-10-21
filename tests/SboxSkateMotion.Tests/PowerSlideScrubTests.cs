using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class PowerSlideScrubTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float Step = 1f / 240f;

	[Fact]
	public void AHeldPowerslideScrubsTheSpeedOffRatherThanGlidingSideways()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, 8.5f );
		var slide = new ControlInput( Steer: 0.3f, Slide: true );
		// Into the slide: the board turns across its travel.
		for ( var i = 0; i < 60; i++ )
			Simulate( board, pilot, settings, slide );
		var start = Speed( board );
		for ( var i = 0; i < 240; i++ )
			Simulate( board, pilot, settings, slide );
		var second = Speed( board );
		output.WriteLine( $"across {MathF.Abs( Vector3.Dot( board.Deck.Forward, Vector3.UnitY ) ):F2}; {start:F2} -> {second:F2} m/s over a second" );
		// Sliding urethane slows the board at about half a g: well over the 1.7 m/s^2 it glided at on the slide's grip.
		Assert.InRange( start - second, 3.5f, 7f );
		// Held on, the slide is over within about two seconds of a fast push.
		for ( var i = 0; i < 240 && pilot.PowerSliding; i++ )
			Simulate( board, pilot, settings, slide );
		Assert.True( Speed( board ) < 1.5f );
	}

	[Fact]
	public void RollingWithoutASlideIsNotScrubbed()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, 8.5f );
		for ( var i = 0; i < 240; i++ )
			Simulate( board, pilot, settings, default );
		Assert.True( Speed( board ) > 7.5f );
	}

	private static float Speed( ArticulatedBoard board ) => new Vector2( board.Deck.Velocity.X, board.Deck.Velocity.Y ).Length();

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

	// Flat ground under every wheel at the wheel's own grip; while the board powerslides the wheels keep only the share of
	// it the board's contacts keep then.
	private static void Simulate( ArticulatedBoard board, BoardPilot pilot, SkateSettings settings, ControlInput input )
	{
		board.BeginStep();
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var grip = settings.Grip * (pilot.PowerSliding ? 0.35f : 1f);
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
