using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class GrindDynamicsTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float RailHeight = 0.4f;
	private const float Step = 1f / 240f;
	private const int Surface = 7;

	[Theory]
	[InlineData( 30 )]
	[InlineData( 60 )]
	[InlineData( 120 )]
	public void FiftyFiftySlidesAlongRailWithFrictionAndLeavesAtItsEnd( int frequency )
	{
		var settings = new SkateSettings();
		var (board, pilot) = Create( settings, Quaternion.Identity, 4f );
		const float railEnd = 5f;
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( railEnd, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var grinding = 0;
		var maximumLateral = 0f;
		var maximumTilt = 0f;
		var maximumGap = 0f;
		var initialEnergy = board.KineticEnergy() + Potential( board, settings );
		var maximumEnergy = initialEnergy;
		var speedAtOneSecond = 0f;
		var left = false;
		for ( var tick = 0; tick < frequency * 2; tick++ )
		{
			for ( var substep = 0; substep < 240 / frequency; substep++ )
			{
				var result = Simulate( board, pilot, rails, contacts, default );
				if ( result.Grind == GrindKind.FiftyFifty )
				{
					grinding++;
					Assert.Equal( MotionMode.Grinding, result.Mode );
					maximumLateral = Math.Max( maximumLateral, MathF.Abs( board.Deck.Position.Y ) );
					maximumTilt = Math.Max( maximumTilt, MathF.Acos( Math.Clamp( board.Deck.Up.Z, -1f, 1f ) ) );
				}
				else if ( board.Deck.Position.X > railEnd )
				{
					left = true;
				}
				maximumGap = Math.Max( maximumGap, board.MaximumAnchorError() );
				maximumEnergy = Math.Max( maximumEnergy, board.KineticEnergy() + Potential( board, settings ) );
			}
			if ( tick == frequency - 1 )
				speedAtOneSecond = board.Deck.Velocity.X;
		}
		var expectedLoss = settings.GrindFriction * settings.Gravity;
		output.WriteLine( $"{frequency}Hz: grinding {grinding} substeps, speed at 1s {speedAtOneSecond}, lateral {maximumLateral}, tilt {maximumTilt}, gap {maximumGap}, energy {initialEnergy}->{maximumEnergy}" );
		// About 4.7 m of rail at 3.3-4 m/s before the nose truck passes its end.
		Assert.InRange( grinding, 260, 360 );
		Assert.InRange( speedAtOneSecond, 4f - expectedLoss * 1.5f, 4f - expectedLoss * 0.5f );
		Assert.InRange( maximumLateral, 0f, RailContacts.GrooveHalfWidth );
		Assert.InRange( maximumTilt, 0f, 0.2f );
		Assert.InRange( maximumGap, 0f, 0.01f );
		Assert.True( maximumEnergy <= initialEnergy * 1.001f, "Grinding must not add energy." );
		Assert.True( left, "The board must drop off past the rail end." );
		Assert.True( board.Deck.Position.Z < RailHeight, "The board must fall once the rail ends." );
	}

	[Fact]
	public void BoardAcrossRailBoardslides()
	{
		var settings = new SkateSettings();
		var across = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI * 0.5f );
		var (board, pilot) = Create( settings, across, 3f, deckOnRail: true );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 4f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var sliding = 0;
		for ( var i = 0; i < 240; i++ )
		{
			var result = Simulate( board, pilot, rails, contacts, default );
			if ( result.Grind == GrindKind.Boardslide )
				sliding++;
		}
		output.WriteLine( $"boardslide {sliding} substeps, x {board.Deck.Position.X}, z {board.Deck.Position.Z}, speed {board.Deck.Velocity.X}" );
		Assert.InRange( sliding, 200, 240 );
		Assert.InRange( board.Deck.Velocity.X, 1.5f, 3f );
		Assert.InRange( board.Deck.Position.Z, RailHeight, RailHeight + 0.05f );
	}

	[Fact]
	public void OllieLeavesTheRail()
	{
		var settings = new SkateSettings();
		var (board, pilot) = Create( settings, Quaternion.Identity, 4f );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 8f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var popped = false;
		var peak = 0f;
		for ( var i = 0; i < 240; i++ )
		{
			var time = i * Step;
			var flick = time < 0.3f ? 0f : time < 0.4f ? -1f : time < 0.45f ? 1f : 0f;
			var result = Simulate( board, pilot, rails, contacts, new ControlInput( FlickY: flick ) );
			popped |= result.Trick == TrickKind.Ollie;
			if ( popped )
				peak = Math.Max( peak, board.Deck.Position.Z );
		}
		output.WriteLine( $"ollie from rail: popped {popped}, deck peak {peak}" );
		Assert.True( popped );
		Assert.True( peak > RailHeight + 0.3f, "The pop must lift the board clear of the rail." );
	}

	[Fact]
	public void NoRailsAddNoContacts()
	{
		var settings = new SkateSettings();
		var (board, _) = Create( settings, Quaternion.Identity, 4f );
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var count = 0;
		var grind = RailContacts.Build( board, ReadOnlySpan<RailSegment>.Empty, Surface, 0.08f, contacts, ref count );
		Assert.False( grind.Active );
		Assert.Equal( 0, count );
	}

	[Fact]
	public void RailOffToTheSideOrFarBelowIsIgnored()
	{
		var settings = new SkateSettings();
		var (board, _) = Create( settings, Quaternion.Identity, 4f );
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var beside = new RailSegment( new Vector3( -1f, 0.2f, RailHeight ), new Vector3( 3f, 0.2f, RailHeight ) );
		var below = new RailSegment( new Vector3( -1f, 0f, RailHeight - 0.2f ), new Vector3( 3f, 0f, RailHeight - 0.2f ) );
		var count = 0;
		Assert.False( RailContacts.Build( board, new[] { beside, below }, Surface, 0.08f, contacts, ref count ).Active );
		Assert.Equal( 0, count );
	}

	private static (ArticulatedBoard Board, BoardPilot Pilot) Create( SkateSettings settings, Quaternion rotation,
		float speed, bool deckOnRail = false )
	{
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var axleDrop = settings.RideHeight - Radius;
		var height = deckOnRail
			? RailHeight + 0.0125f + 0.002f
			: RailHeight + RailContacts.AxleRadius + axleDrop + 0.002f;
		board.Reset( new Vector3( 0f, 0f, height ), rotation, Vector3.UnitX * speed, Vector3.Zero );
		board.Body( Surface ) = new DynamicBody { Rotation = Quaternion.Identity };
		return (board, new BoardPilot( settings ));
	}

	private static PhysicsStep Simulate( ArticulatedBoard board, BoardPilot pilot, RailSegment[] rails,
		CollisionContact[] contacts, ControlInput input )
	{
		board.BeginStep();
		var count = 0;
		var grind = RailContacts.Build( board, rails, Surface, new SkateSettings().GrindFriction, contacts, ref count );
		var span = contacts.AsSpan( 0, count );
		var result = pilot.Apply( board, span, input, Step, grind );
		board.Step( span, 0f, 0f, Step );
		return result;
	}

	private static float Potential( ArticulatedBoard board, SkateSettings settings )
	{
		var energy = 0f;
		foreach ( var body in board.Bodies )
			energy += body.Position.Z * settings.Gravity / body.InverseMass;
		return energy;
	}
}
