using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using Xunit;
using Xunit.Abstractions;

namespace SboxSkateMotion.Tests;

public class GrindKindsTests( ITestOutputHelper output )
{
	private const float Radius = 0.0285f;
	private const float RailHeight = 0.4f;
	private const float Step = 1f / 240f;
	private const int Surface = 7;

	[Theory]
	[InlineData( -0.33f, (int)GrindKind.Noseslide )]
	[InlineData( 0.33f, (int)GrindKind.Tailslide )]
	public void ADeckAcrossTheRailPastATruckSlidesOnThatEnd( float offset, int kind )
	{
		var expected = (GrindKind)kind;
		var settings = SkateSettings.RecoveredDefaults;
		// Turned a quarter round the deck's forward is +Y, so the rail along X crosses it at -offset from its centre.
		var across = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI * 0.5f );
		var (board, pilot) = Create( settings, across, 3f, new Vector3( 0f, offset, RailHeight + 0.0125f + 0.002f ) );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 4f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var sliding = 0;
		var boardslides = 0;
		for ( var i = 0; i < 240; i++ )
		{
			var result = Simulate( board, pilot, rails, contacts, default );
			if ( result.Grind == expected )
				sliding++;
			if ( result.Grind == GrindKind.Boardslide )
				boardslides++;
		}
		output.WriteLine( $"{expected}: {sliding} substeps, boardslide {boardslides}, x {board.Deck.Position.X}, y {board.Deck.Position.Y}, z {board.Deck.Position.Z}" );
		Assert.InRange( sliding, 150, 240 );
		Assert.Equal( 0, boardslides );
		Assert.InRange( board.Deck.Position.Z, RailHeight - 0.05f, RailHeight + 0.05f );
	}

	[Theory]
	[InlineData( -0.29f, (int)GrindKind.Noseslide )]
	[InlineData( 0.29f, (int)GrindKind.Tailslide )]
	public void ADeckAcrossTheRailWithATruckOverItSlidesOnThatEndInsteadOfGrinding( float offset, int kind )
	{
		// Turned across the rail and coming down with a truck's axle on it: the truck lies along the rail, so it does
		// not grind, and the deck's end comes down on the rail and slides.
		var expected = (GrindKind)kind;
		var settings = SkateSettings.RecoveredDefaults;
		var across = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI * 0.5f );
		var axle = RailHeight + RailContacts.AxleRadius + settings.RideHeight - Radius + 0.002f;
		var (board, pilot) = Create( settings, across, 3f, new Vector3( 0f, offset, axle ) );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 4f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var sliding = 0;
		var trucks = 0;
		for ( var i = 0; i < 240; i++ )
		{
			var result = Simulate( board, pilot, rails, contacts, default );
			if ( i >= 60 && result.Grind == expected )
				sliding++;
			if ( result.Grind is GrindKind.FiftyFifty or GrindKind.FiveO or GrindKind.Nosegrind )
				trucks++;
		}
		output.WriteLine( $"{expected}: {sliding} of the last 180 substeps, on a truck {trucks}, z {board.Deck.Position.Z}" );
		Assert.Equal( 0, trucks );
		Assert.InRange( sliding, 150, 180 );
	}

	[Theory]
	[InlineData( -0.45f, (int)GrindKind.FiveO )]
	[InlineData( 0.45f, (int)GrindKind.Nosegrind )]
	public void TheTrickStickRocksAFiftyFiftyOntoOneTruck( float stick, int kind )
	{
		var expected = (GrindKind)kind;
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, Quaternion.Identity, 3f, null );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 8f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var onOne = 0;
		var lost = 0;
		for ( var i = 0; i < 360; i++ )
		{
			var result = Simulate( board, pilot, rails, contacts, new ControlInput( FlickY: i < 60 ? 0f : stick ) );
			if ( i >= 180 && result.Grind == expected )
				onOne++;
			if ( result.Grind == GrindKind.None )
				lost++;
		}
		var noseUp = board.Body( 1 ).Position.Z - board.Body( 2 ).Position.Z;
		output.WriteLine( $"{expected}: {onOne} of the last 180 substeps, off the rail {lost}, nose above tail {noseUp}" );
		Assert.InRange( onOne, 150, 180 );
		Assert.Equal( 0, lost );
		Assert.True( expected == GrindKind.FiveO ? noseUp > 0.03f : noseUp < -0.03f );
	}

	[Fact]
	public void SteeringTurnsAOneTruckGrindOffTheRailsLine()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, Quaternion.Identity, 3f, null );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 8f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var result = default( PhysicsStep );
		for ( var i = 0; i < 360; i++ )
		{
			var input = i < 60 ? default : new ControlInput( Steer: i < 120 ? 0f : 1f, FlickY: -0.45f );
			result = Simulate( board, pilot, rails, contacts, input );
		}
		var heading = MathF.Atan2( board.Deck.Forward.Y, board.Deck.Forward.X );
		output.WriteLine( $"grind {result.Grind}, heading {heading}, twisted {result.GrindShape.Twisted}, tail truck y {board.Body( 2 ).Position.Y}" );
		Assert.Equal( GrindKind.FiveO, result.Grind );
		Assert.True( result.GrindShape.Twisted );
		Assert.InRange( MathF.Abs( board.Body( 2 ).Position.Y ), 0f, RailContacts.GrooveHalfWidth );
	}

	[Theory]
	[InlineData( -0.45f, 0.45f, (int)GrindKind.FiveO )]
	[InlineData( 0.45f, -0.45f, (int)GrindKind.Nosegrind )]
	public void ATurnedOneTruckGrindTippedTheOtherWayHangsItsFreeTruckLow( float onto, float tip, int kind )
	{
		// Onto one truck, turned off the line, then tipped to drop the free end: a smith or feeble on the back truck, a
		// willy on the front.
		var expected = (GrindKind)kind;
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, Quaternion.Identity, 3f, null );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 8f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var low = 0;
		var result = default( PhysicsStep );
		for ( var i = 0; i < 480; i++ )
		{
			var input = i < 60 ? default
				: i < 180 ? new ControlInput( Steer: i < 100 ? 0f : 1f, FlickY: onto )
				: new ControlInput( Steer: 1f, FlickY: tip );
			result = Simulate( board, pilot, rails, contacts, input );
			if ( i >= 300 && result.Grind == expected && result.GrindShape.Low )
				low++;
		}
		output.WriteLine( $"{expected}: low {low} of the last 180 substeps, now {result.Grind}, low {result.GrindShape.Low}, deck up {board.Deck.Up}" );
		Assert.InRange( low, 150, 180 );
	}

	[Fact]
	public void AFreeTruckDroppedBelowTheRailHangsLow()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (level, _) = Create( settings, Quaternion.Identity, 0f, null );
		var grind = new GrindLock( GrindKind.FiveO, Vector3.UnitX, Vector3.UnitZ, new Vector3( 0f, 0f, RailHeight ) );
		Assert.False( GrindShape.Measure( level, grind ).Low );
		// Nose dipped 15 degrees about the tail truck, and turned off the rail.
		var dipped = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, 0.4f ) * Quaternion.CreateFromAxisAngle( Vector3.UnitY, 0.26f );
		var (board, _) = Create( settings, dipped, 0f, null );
		var shape = GrindShape.Measure( board, grind );
		Assert.True( shape.Low );
		Assert.True( shape.Twisted );
		Assert.True( shape.NoseOffset.Y > 0.1f );
	}

	[Fact]
	public void ANosePointingAgainstTheTravelIsBackward()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var (board, _) = Create( settings, Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI ), 3f, null );
		var grind = new GrindLock( GrindKind.FiftyFifty, Vector3.UnitX, Vector3.UnitZ, new Vector3( 0f, 0f, RailHeight ) );
		// The board moves along +X with its nose pointing along -X.
		Assert.True( GrindShape.Measure( board, grind ).Backward );
	}

	[Theory]
	[InlineData( 1f, -0.45f )]
	[InlineData( -1f, -0.45f )]
	[InlineData( 1f, 0.45f )]
	[InlineData( -1f, 0.45f )]
	public void TurningAOneTruckGrindKeepsItsTruckOnTheRailsLine( float steer, float stick )
	{
		// A one-truck grind turned off the rail's line turns about its grinding truck: the truck stays over the rail and
		// the deck does not roll, so the wheels hanging either side of a 2 in rail stay clear of it. Leaning the deck as
		// well slid the truck 2 cm off the line, and a wheel into the rail's side made the rider step off.
		const float RailHalfWidth = 0.0254f;
		var settings = SkateSettings.RecoveredDefaults;
		var (board, pilot) = Create( settings, Quaternion.Identity, 4f, null );
		var rails = new[] { new RailSegment( new Vector3( -1f, 0f, RailHeight ), new Vector3( 12f, 0f, RailHeight ) ) };
		var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
		var truck = stick < 0f ? 2 : 1;
		var drift = 0f;
		var clearance = float.MaxValue;
		var roll = 0f;
		var result = default( PhysicsStep );
		for ( var i = 0; i < 480; i++ )
		{
			var input = i < 60 ? default : new ControlInput( Steer: i < 120 ? 0f : steer, FlickY: stick );
			result = Simulate( board, pilot, rails, contacts, input );
			if ( i < 120 )
				continue;
			drift = MathF.Max( drift, MathF.Abs( board.Body( truck ).Position.Y ) );
			for ( var wheel = 3; wheel < 7; wheel++ )
			{
				var centre = board.Body( wheel ).Position;
				var side = MathF.Max( 0f, MathF.Abs( centre.Y ) - RailHalfWidth );
				var above = MathF.Max( 0f, centre.Z - RailHeight );
				clearance = MathF.Min( clearance, MathF.Sqrt( side * side + above * above ) - Radius );
			}
			var across = Vector3.Normalize( Vector3.Cross( Vector3.UnitZ, board.Deck.Forward ) );
			roll = MathF.Max( roll, MathF.Abs( Vector3.Dot( board.Deck.Up, across ) ) );
		}
		output.WriteLine( $"{result.Grind}: truck drift {drift}, wheel clearance {clearance}, roll {roll}, twisted {result.GrindShape.Twisted}" );
		Assert.Equal( stick < 0f ? GrindKind.FiveO : GrindKind.Nosegrind, result.Grind );
		Assert.True( result.GrindShape.Twisted );
		Assert.InRange( drift, 0f, 0.01f );
		Assert.True( clearance > 0.01f );
		Assert.InRange( roll, 0f, 0.1f );
	}

	private static (ArticulatedBoard Board, BoardPilot Pilot) Create( SkateSettings settings, Quaternion rotation,
		float speed, Vector3? at )
	{
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var axleDrop = settings.RideHeight - Radius;
		var height = RailHeight + RailContacts.AxleRadius + axleDrop + 0.002f;
		board.Reset( at ?? new Vector3( 0f, 0f, height ), rotation, Vector3.UnitX * speed, Vector3.Zero );
		if ( at is null && rotation != Quaternion.Identity && speed == 0f )
		{
			// Rest the tail truck on the rail, wherever the turn put it.
			var tail = board.Body( 2 ).Position;
			var shift = new Vector3( -tail.X, -tail.Y, RailHeight + RailContacts.AxleRadius - tail.Z );
			board.Reset( new Vector3( 0f, 0f, height ) + shift, rotation, Vector3.Zero, Vector3.Zero );
		}
		board.Body( Surface ) = new DynamicBody { Rotation = Quaternion.Identity };
		return (board, new BoardPilot( settings ));
	}

	private static PhysicsStep Simulate( ArticulatedBoard board, BoardPilot pilot, RailSegment[] rails,
		CollisionContact[] contacts, ControlInput input )
	{
		board.BeginStep();
		var count = 0;
		var grind = RailContacts.Build( board, rails, Surface, SkateSettings.RecoveredDefaults.GrindFriction, contacts, ref count );
		var span = contacts.AsSpan( 0, count );
		var result = pilot.Apply( board, span, input, Step, grind );
		board.Step( span, 0f, 0f, Step );
		return result;
	}
}
