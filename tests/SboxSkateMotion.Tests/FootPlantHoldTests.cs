using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class FootPlantHoldTests
{
	private const float Step = 1f / 240f;

	[Fact]
	public void AFootPlantPopsAsTheRiderSpringsOffThePlantedFoot()
	{
		// The plant is reported as it is asked for, but the board stays down while the rider's animation plants the foot,
		// and pops (once, without reporting the trick again) as the rider springs off it.
		var (pilot, board, contacts) = Rolling();
		pilot.Apply( board, contacts, new ControlInput( Grab: true ), Step );
		var asked = pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step );
		Assert.Equal( TrickKind.Plant, asked.Trick );
		Assert.Equal( Vector3.Zero, asked.Impulse );
		for ( var step = 0; step < 120; step++ )
		{
			var planting = pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ) { FootPlanting = true }, Step );
			Assert.Equal( TrickKind.None, planting.Trick );
			Assert.Equal( Vector3.Zero, planting.Impulse );
		}
		var sprung = pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step );
		Assert.Equal( TrickKind.None, sprung.Trick );
		Assert.True( sprung.Impulse.Z > 0f, $"no pop as the foot let go: {sprung.Impulse}" );
	}

	[Fact]
	public void AFootPlantWithNoPlantingShownPopsAMomentLater()
	{
		// A rider with no foot-plant animation never says it is planting: the pop comes a moment after the trick.
		var (pilot, board, contacts) = Rolling();
		pilot.Apply( board, contacts, new ControlInput( Grab: true ), Step );
		Assert.Equal( TrickKind.Plant, pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step ).Trick );
		var popped = -1;
		for ( var step = 1; step <= 60 && popped < 0; step++ )
		{
			if ( pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step ).Impulse.Z > 0f )
				popped = step;
		}
		Assert.InRange( popped * Step, 0.05f, 0.12f );
	}

	[Fact]
	public void AFootPlantIsNeverHeldPastItsLongest()
	{
		var (pilot, board, contacts) = Rolling();
		pilot.Apply( board, contacts, new ControlInput( Grab: true ), Step );
		pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step );
		var popped = false;
		for ( var step = 0; step < 240 && !popped; step++ )
			popped = pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ) { FootPlanting = true }, Step ).Impulse.Z > 0f;
		Assert.True( popped, "a foot plant held a whole second never popped" );
	}

	[Fact]
	public void AnOlliePoppedAsAPushStartsWaitsForTheFootPlantButOtherOlliesPopAtOnce()
	{
		// An ollie popped as a push stroke starts is a no comply: held for its foot plant like a plant.
		var (pilot, board, contacts) = Rolling();
		pilot.Apply( board, contacts, default, Step );
		pilot.Apply( board, contacts, new ControlInput( Push: 1f ), Step );
		var popped = Ollie( pilot, board, contacts, new ControlInput( Push: 1f ) { FootPlanting = true } );
		Assert.Equal( TrickKind.Ollie, popped.Trick );
		Assert.Equal( Vector3.Zero, popped.Impulse );
		// An ollie from a ride with no push starting pops on the step it is flicked.
		var (plain, plainBoard, plainContacts) = Rolling();
		for ( var step = 0; step < 240; step++ )
			plain.Apply( plainBoard, plainContacts, default, Step );
		var ollie = Ollie( plain, plainBoard, plainContacts, default );
		Assert.Equal( TrickKind.Ollie, ollie.Trick );
		Assert.True( ollie.Impulse.Z > 0f, $"a plain ollie did not pop at once: {ollie.Impulse}" );
	}

	[Fact]
	public void AHeldFootPlantRolledOffAnEdgeIsLetGoUnpopped()
	{
		var (pilot, board, contacts) = Rolling();
		pilot.Apply( board, contacts, new ControlInput( Grab: true ), Step );
		pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step );
		// No ground under the wheels: the plant is let go, and coming back down nothing pops.
		for ( var step = 0; step < 10; step++ )
			Assert.Equal( Vector3.Zero, pilot.Apply( board, System.ReadOnlySpan<CollisionContact>.Empty,
				new ControlInput( Push: 1f, Grab: true ) { FootPlanting = true }, Step ).Impulse );
		for ( var step = 0; step < 60; step++ )
			Assert.Equal( Vector3.Zero, pilot.Apply( board, contacts, new ControlInput( Push: 1f, Grab: true ), Step ).Impulse );
	}

	[Fact]
	public void ANoComplysPopTellsTheGraphThePushGesture()
	{
		// The graph's way from a push's start into the no comply's foot plant is the push's own gesture.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Pushing = true }, false, 1f / 60f );
		source.Fill( facts, new RiderAnimationInput { Pushing = true, Trick = "ollie", TimeSincePop = 0f }, false, 1f / 60f );
		Assert.Equal( 1f, facts.Facts[ "has-gesture-intent" ] );
		var gesture = new StateTestDefinition { Test = "fact", Fact = "has-gesture-intent" };
		gesture.Arguments[ "group" ] = "square";
		Assert.True( facts.MatchesArguments( gesture ) );
		// A plain ollie tells no gesture.
		var plain = new RiderStateFacts();
		for ( var frame = 0; frame < 40; frame++ )
			plain.Fill( facts, new RiderAnimationInput { Pushing = true }, false, 1f / 60f );
		plain.Fill( facts, new RiderAnimationInput { Pushing = true, Trick = "ollie", TimeSincePop = 0f }, false, 1f / 60f );
		Assert.False( facts.Facts.ContainsKey( "has-gesture-intent" ) );
	}

	// The trick stick pulled back and flicked up: the step the ollie fires on.
	private static PhysicsStep Ollie( BoardPilot pilot, ArticulatedBoard board, CollisionContact[] contacts, ControlInput with )
	{
		for ( var step = 0; step < 24; step++ )
			pilot.Apply( board, contacts, with with { FlickY = -1f }, Step );
		PhysicsStep fired = default;
		for ( var step = 0; step < 4 && fired.Trick == TrickKind.None; step++ )
			fired = pilot.Apply( board, contacts, with with { FlickY = 1f }, Step );
		return fired;
	}

	private static (BoardPilot Pilot, ArticulatedBoard Board, CollisionContact[] Contacts) Rolling()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var board = Board( settings );
		return (new BoardPilot( settings ), board, Ground( board ));
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
