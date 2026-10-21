using System;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Input;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class HandPlantTests
{
	private const float Step = 1f / 240f;
	private const float Radius = 0.0285f;

	// The top of a vertical ramp facing -x: its surface normal points out over the ramp.
	private static readonly Vector3 Wall = -Vector3.UnitX;

	[Fact]
	public void AHandPlantsOnlyOffASteepRampSoonAfterTakeOffAndNearItsLip()
	{
		var plant = new HandPlant();
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		Assert.True( plant.Step( true, new Vector3( -0.1f, 0f, 3.6f ), Step, out _ ) );
		// Off flat ground there is no lip.
		plant = new HandPlant();
		plant.Ground( new Vector3( 0f, 0f, 3f ), Vector3.UnitZ );
		Assert.False( plant.Step( true, new Vector3( 0f, 0f, 3.6f ), Step, out _ ) );
		// Too far from the lip, or too long after leaving it.
		plant = new HandPlant();
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		Assert.False( plant.Step( true, new Vector3( 0f, 0f, 5f ), Step, out _ ) );
		plant = new HandPlant();
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		for ( var t = 0f; t < HandPlant.Window + 0.1f; t += Step )
			plant.Step( false, new Vector3( 0f, 0f, 3.5f ), Step, out _ );
		Assert.False( plant.Step( true, new Vector3( 0f, 0f, 3.5f ), Step, out _ ) );
	}

	[Fact]
	public void AWheelLiftingPartWayUpTheWallDoesNotPlantBelowTheTop()
	{
		var plant = new HandPlant();
		// The nose wheels last touched at 3 m, and the deck's middle is still below them, beside the wall.
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		Assert.False( plant.Step( true, new Vector3( -0.05f, 0f, 2.8f ), Step, out _ ) );
		// Risen past the top, the held hand plants.
		Assert.True( plant.Step( true, new Vector3( -0.1f, 0f, 3.1f ), Step, out _ ) );
	}

	[Fact]
	public void APlantLastsWhileHeldUpToItsLongestAndOncePerFlight()
	{
		var plant = new HandPlant();
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		var position = new Vector3( 0f, 0f, 3.5f );
		Assert.True( plant.Step( true, position, Step, out _ ) );
		Assert.True( plant.Step( true, position, Step, out _ ) );
		Assert.False( plant.Step( false, position, Step, out var released ) );
		Assert.True( released );
		// Pressed again in the same flight: no second plant.
		Assert.False( plant.Step( true, position, Step, out _ ) );
		// A new flight, the button let go and pressed again: held, it lasts its longest.
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		plant.Step( false, position, Step, out _ );
		Assert.True( plant.Step( true, position, Step, out _ ) );
		var held = 0f;
		while ( plant.Step( true, position, Step, out released ) )
			held += Step;
		Assert.True( released );
		Assert.InRange( held, HandPlant.MaximumHold - 0.05f, HandPlant.MaximumHold + 0.05f );
	}

	[Fact]
	public void AButtonHeldThroughABounceAtTheLipStillPlants()
	{
		// The board leaves the lip, touches it again and leaves: the button held all the while plants on the way off.
		var plant = new HandPlant();
		plant.Ground( new Vector3( 0f, 0f, 3f ), Wall );
		plant.Step( true, new Vector3( 0f, 0f, 3.05f ), Step, out _ );
		plant.Ground( new Vector3( 0f, 0f, 3.1f ), Wall );
		Assert.True( plant.Step( true, new Vector3( 0f, 0f, 3.2f ), Step, out _ ) );
	}

	[Theory]
	[InlineData( true, false )]
	[InlineData( false, true )]
	public void EitherDeviceCanPlantAHand( bool keys, bool pad )
	{
		var keyboard = new BoardControls( 0f, 0f, 0f, 0f, 0f, 0f, false, false, 0, false, false, false, 0, 0, false ) { HandPlant = keys };
		var controller = new BoardControls( 0f, 0f, 0f, 0f, 0f, 0f, false, false, 0, false, false, false, 0, 0, false ) { HandPlant = pad };
		Assert.True( DeviceInputMerge.Board( keyboard, false, controller, new DeviceActivity( false, false, false ) ).HandPlant );
	}

	[Fact]
	public void TheBoardDropsBackInNoseDownTheRamp()
	{
		var plant = new HandPlant();
		plant.Ground( Vector3.Zero, Wall );
		var turn = plant.DropRotation();
		Assert.True( Vector3.Dot( Vector3.Transform( Vector3.UnitX, turn ), -Vector3.UnitZ ) > 0.99f );
		Assert.True( Vector3.Dot( Vector3.Transform( Vector3.UnitZ, turn ), Wall ) > 0.99f );
		Assert.True( plant.DropVelocity().Z < 0f );
	}

	[Fact]
	public void APlantedBoardStallsOverTheLipAndDropsBackInWhenLetGo()
	{
		var settings = SkateSettings.RecoveredDefaults;
		var origins = new Vector3[ 4 ];
		for ( var i = 0; i < origins.Length; i++ )
			origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - Radius);
		var board = new ArticulatedBoard( settings, origins, Radius );
		var lip = new Vector3( 0f, 0f, 3f );
		board.Reset( lip, Quaternion.Identity, new Vector3( 0f, 0f, 3f ), Vector3.Zero );
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var pilot = new BoardPilot( settings );
		// The last step on the ramp, its wheels against the vertical top.
		var contacts = new CollisionContact[ 4 ];
		for ( var i = 0; i < 4; i++ )
		{
			var wheel = board.Body( i + 3 ).Position;
			// The nose wheels touch a little higher, at the wall's top.
			var touch = wheel + Vector3.UnitZ * (i < 2 ? 0.01f : 0f);
			contacts[ i ] = new CollisionContact( i + 3, 7, wheel, touch, Wall, Vector3.UnitZ, Vector3.UnitY, 0.85f, 0.68f, 0f );
		}
		board.BeginStep();
		pilot.Apply( board, contacts, default, Step );
		board.Step( contacts, 0f, 0f, Step );
		var planted = false;
		for ( var i = 0; i < 120; i++ )
		{
			board.BeginStep();
			var result = pilot.Apply( board, ReadOnlySpan<CollisionContact>.Empty, new ControlInput() { HandPlant = true }, Step );
			board.Step( ReadOnlySpan<CollisionContact>.Empty, 0f, 0f, Step );
			planted |= result.HandPlanted;
		}
		Assert.True( planted );
		Assert.True( pilot.HandPlanted );
		// The lip is where the wheels left the wall: the highest of their contacts, the wall's top.
		var wheels = contacts[ 0 ].PointB;
		Assert.True( Vector3.Distance( pilot.HandPlantLip, wheels ) < 0.001f );
		var hold = wheels + Vector3.UnitZ * HandPlant.HoldHeight + Wall * HandPlant.HoldOut;
		Assert.InRange( Vector3.Distance( board.Deck.Position, hold ), 0f, 0.05f );
		Assert.InRange( board.Deck.Velocity.Length(), 0f, 0.2f );
		board.BeginStep();
		pilot.Apply( board, ReadOnlySpan<CollisionContact>.Empty, default, Step );
		board.Step( ReadOnlySpan<CollisionContact>.Empty, 0f, 0f, Step );
		Assert.False( pilot.HandPlanted );
		Assert.True( board.Deck.Velocity.Z < 0f );
		Assert.True( Vector3.Dot( board.Deck.Forward, -Vector3.UnitZ ) > 0.95f );
	}
}
