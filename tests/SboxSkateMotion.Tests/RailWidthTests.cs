using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class RailWidthTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float RailHeight = 0.4f;
	private const float Step = 1f / 240f;
	private const int Surface = 7;

	// The lab's 2 in square rail.
	private const float RailHalfWidth = 0.0254f;

	[Fact]
	public void PlayKeepsTheWholeRailBetweenTheWheels()
	{
		var square = new BoardBody( Vector3.Zero, Quaternion.Identity, Vector3.Zero, Vector3.Zero, Vector3.One );
		Assert.Equal( RailContacts.GrooveHalfWidth, RailContacts.Play( square, Vector3.UnitY, 0f ), 5 );
		Assert.Equal( RailContacts.GrooveHalfWidth - RailHalfWidth, RailContacts.Play( square, Vector3.UnitY, RailHalfWidth ), 5 );
		// A truck turned off the rail's line reaches less far across it.
		var turned = square with { Rotation = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 0.58f ) };
		Assert.Equal( RailContacts.GrooveHalfWidth * MathF.Cos( 0.58f ) - RailHalfWidth,
			RailContacts.Play( turned, Vector3.UnitY, RailHalfWidth ), 5 );
		// A rail wider than the gap between the wheels leaves the hanger almost no room.
		Assert.InRange( RailContacts.Play( square, Vector3.UnitY, 0.07f ), 0.001f, 0.003f );
	}

	[Fact]
	public void LookupSpaceKeepsTheRailsWidth()
	{
		var rail = new RailSegment( new Vector3( 1f, 2f, 3f ), new Vector3( 4f, 2f, 3f ) ) { HalfWidth = RailHalfWidth };
		var back = RailEdgeSpace.ToWorld( RailEdgeSpace.ToLookup( rail ) );
		Assert.Equal( rail, back );
		Assert.Equal( RailHalfWidth, back.HalfWidth );
	}

	[Fact]
	public void FiftyFiftyLandingOffCentreIsHeldOverTheRailAndCentres()
	{
		// Coming down 5 cm to the side of a 2 in rail's line (the lock-on's move used up), a wheel would ride the rail's
		// edge. The wheels keep the whole rail between them, so the trucks sit within the play at once and the board
		// then centres over the rail.
		var settings = SkateSettings.RecoveredDefaults;
		var board = Create( settings, Quaternion.Identity, 4f, 0.05f );
		var pilot = new BoardPilot( settings );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 12f, 0f, RailHeight ) ) { HalfWidth = RailHalfWidth } };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var play = RailContacts.GrooveHalfWidth - RailHalfWidth;
		var widest = 0f;
		var settled = 0f;
		var grinding = 0;
		for ( var i = 0; i < 480; i++ )
		{
			var result = Simulate( board, pilot, rails, contacts, default );
			if ( result.Grind != GrindKind.None )
				grinding++;
			var lateral = MathF.Max( MathF.Abs( board.Body( 1 ).Position.Y ), MathF.Abs( board.Body( 2 ).Position.Y ) );
			if ( i >= 24 )
				widest = MathF.Max( widest, lateral );
			if ( i >= 240 )
				settled = MathF.Max( settled, MathF.Abs( board.Deck.Position.Y ) );
		}
		output.WriteLine( $"grinding {grinding} substeps, widest after 0.1 s {widest}, off centre after 1 s {settled}" );
		Assert.True( grinding > 400 );
		Assert.InRange( widest, 0f, play + 0.003f );
		Assert.InRange( settled, 0f, 0.005f );
	}

	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void CrookKeepsTheRailBetweenItsTurnedWheels( float steer )
	{
		// Turned off the rail's line on its nose truck, the wheels reach less far across the rail: the truck is held
		// within the smaller play, so neither front wheel rides up onto the rail's edge.
		var settings = SkateSettings.RecoveredDefaults;
		var board = Create( settings, Quaternion.Identity, 4f, 0f );
		var pilot = new BoardPilot( settings );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 12f, 0f, RailHeight ) ) { HalfWidth = RailHalfWidth } };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var clearance = float.MaxValue;
		var result = default( PhysicsStep );
		for ( var i = 0; i < 480; i++ )
		{
			var input = i < 60 ? default : new ControlInput( Steer: i < 120 ? 0f : steer, FlickY: 0.45f );
			result = Simulate( board, pilot, rails, contacts, input );
			if ( i < 160 )
				continue;
			for ( var wheel = 3; wheel < 5; wheel++ )
			{
				var centre = board.Body( wheel ).Position;
				var side = MathF.Max( 0f, MathF.Abs( centre.Y ) - RailHalfWidth );
				var above = MathF.Max( 0f, centre.Z - RailHeight );
				clearance = MathF.Min( clearance, MathF.Sqrt( side * side + above * above ) - Radius );
			}
		}
		output.WriteLine( $"{result.Grind}: front wheel clearance {clearance}, twisted {result.GrindShape.Twisted}" );
		Assert.Equal( GrindKind.Nosegrind, result.Grind );
		Assert.True( result.GrindShape.Twisted );
		Assert.True( clearance > 0f );
	}

	private static ArticulatedBoard Create( SkateSettings settings, Quaternion rotation, float speed, float side )
	{
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var height = RailHeight + RailContacts.AxleRadius + settings.RideHeight - Radius + 0.002f;
		board.Reset( new Vector3( 0f, side, height ), rotation, Vector3.UnitX * speed, Vector3.Zero );
		board.Body( Surface ) = new DynamicBody { Rotation = Quaternion.Identity };
		return board;
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
}
