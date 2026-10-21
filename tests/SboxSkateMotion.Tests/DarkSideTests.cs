using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DarkSideTests
{
	private const float Frame = 1f / 60f;
	private const float Step = 1f / 240f;
	private const float Radius = 0.0285f;

	[Fact]
	public void TheBoardIsDarkWhileTheGraphHoldsItDark()
	{
		// A dark catch makes the board dark at once, though the deck shown has not yet settled upside down, and it stays
		// dark until the graph lets go of it (a flip back to light).
		var facts = new StateFacts();
		var source = new RiderStateFacts { GraphDark = true };
		source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = 0.2f }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-dark" ] );
		source.Fill( facts, new RiderAnimationInput { DeckUp = -1f }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-dark" ] );
		source.GraphDark = false;
		source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = 1f }, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "is-dark" ] );
	}

	[Theory]
	[InlineData( true, "boardslide", "darkslide" )]
	[InlineData( false, "boardslide", "board" )]
	public void ASlideStartedWhileTheGraphHoldsTheBoardDarkIsADarkslide( bool dark, string grind, string named )
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts { GraphDark = dark };
		source.Fill( facts, new RiderAnimationInput { Grind = grind, DeckUp = 1f }, false, Frame );
		Assert.EndsWith( named, facts.Grind );
		Assert.Equal( dark, source.Darksliding );
		// The name is the grind's from its start: the board going dark or light partway along does not rename it.
		source.GraphDark = !dark;
		source.Fill( facts, new RiderAnimationInput { Grind = grind, DeckUp = 1f }, false, Frame );
		Assert.EndsWith( named, facts.Grind );
		source.Fill( facts, new RiderAnimationInput { Airborne = true }, false, Frame );
		Assert.False( source.Darksliding );
	}

	[Fact]
	public void ADarkBoardSkidsOnItsGripAndIsNotPushed()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var rolling = Ride( settings, new ControlInput(), 0.5f );
		var skidding = Ride( settings, new ControlInput() { DarkSideUp = true }, 0.5f );
		var pushed = Ride( settings, new ControlInput( Push: 1f ) { DarkSideUp = true }, 0.5f );
		// Three metres a second, half a second: rolling loses little, a skid about 1.5 m/s, and pushing adds nothing.
		Assert.True( rolling - skidding > 1.2f );
		Assert.InRange( pushed, skidding - 0.05f, skidding + 0.05f );
	}

	[Fact]
	public void ABoardLetGoDarkSideUpGoesOnUpsideDown()
	{
		// Let go of while held dark side up (a bail), the board is rolled over where it is and keeps its motion.
		var settings = SkateSettings.RecoveredDefaults;
		var (board, _) = Create( settings, 3f );
		var position = board.Deck.Position;
		var forward = board.Deck.Forward;
		BoardPilot.FlipOver( board );
		Assert.True( board.Deck.Up.Z < -0.99f );
		Assert.True( Vector3.Dot( board.Deck.Forward, forward ) > 0.99f );
		Assert.True( Vector3.Distance( board.Deck.Position, position ) < 0.001f );
		Assert.InRange( board.Deck.Velocity.X, 2.99f, 3.01f );
	}

	[Fact]
	public void ABoardLyingUpsideDownIsTurnedBackOverOntoItsWheels()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, _) = Create( settings, 0f );
		BoardPilot.FlipOver( board );
		var position = board.Deck.Position;
		var forward = board.Deck.Forward;
		BoardPilot.TurnUpright( board, settings.RideHeight );
		Assert.True( board.Deck.Up.Z > 0.99f );
		Assert.True( Vector3.Dot( board.Deck.Forward, forward ) > 0.99f );
		Assert.InRange( board.Deck.Position.Z - position.Z, settings.RideHeight - 0.0001f, settings.RideHeight + 0.0001f );
		// Already upright, it is left alone.
		var upright = board.Deck.Position;
		BoardPilot.TurnUpright( board, settings.RideHeight );
		Assert.True( Vector3.Distance( board.Deck.Position, upright ) < 0.0001f );
	}

	[Theory]
	[InlineData( 0f, "normal" )]
	[InlineData( -1f, "left" )]
	[InlineData( 1f, "right" )]
	public void APopOffADarkslideIsTakenFromTheRailWithItsOutBySteering( float steer, string type )
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts { GraphDark = true };
		source.Fill( facts, new RiderAnimationInput { Grind = "boardslide", DeckUp = 1f, TimeSincePop = 2f }, false, Frame );
		Assert.True( source.Darksliding );
		source.Fill( facts, new RiderAnimationInput
		{
			Airborne = true, Trick = "kickflip", TimeSincePop = 0.01f, Steer = steer, DeckUp = 1f
		}, false, Frame );
		Assert.Equal( "grind", facts.Physics );
		Assert.Equal( 1f, facts.Facts[ "is-dark" ] );
		var test = new StateTestDefinition { Test = "fact", Fact = "grind-trick-out-type-allowed" };
		test.Arguments[ "type" ] = type;
		Assert.True( facts.MatchesArguments( test ) );
		// Past the pop, and for a pop off a light slide, the rider is in the air.
		source.Fill( facts, new RiderAnimationInput { Airborne = true, Trick = "kickflip", TimeSincePop = 0.2f, DeckUp = 1f }, false, Frame );
		Assert.Equal( "air", facts.Physics );
		var light = new RiderStateFacts();
		light.Fill( facts, new RiderAnimationInput { Grind = "boardslide", DeckUp = 1f, TimeSincePop = 2f }, false, Frame );
		light.Fill( facts, new RiderAnimationInput { Airborne = true, Trick = "ollie", TimeSincePop = 0.01f, DeckUp = 1f }, false, Frame );
		Assert.Equal( "ground", facts.Physics );
	}

	// Speed after riding this long on flat ground with these controls, from 3 m/s.
	private static float Ride( SkateSettings settings, ControlInput input, float seconds )
	{
		var (board, pilot) = Create( settings, 3f );
		for ( var t = 0f; t < seconds; t += Step )
		{
			board.BeginStep();
			var contacts = Ground( board, settings );
			pilot.Apply( board, contacts, input, Step );
			board.Step( contacts, 0f, 0f, Step );
		}
		return board.Deck.Velocity.X;
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

	// Flat ground under every wheel.
	private static CollisionContact[] Ground( ArticulatedBoard board, SkateSettings settings )
	{
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var wheel = board.Body( i + 3 ).Position;
			var bottom = new Vector3( wheel.X, wheel.Y, wheel.Z - Radius );
			var ground = new Vector3( wheel.X, wheel.Y, 0f );
			contacts[ i ] = new CollisionContact( i + 3, 7, bottom, ground, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY,
				settings.Grip, settings.Grip * 0.8f, 0f );
		}
		return contacts;
	}
}
