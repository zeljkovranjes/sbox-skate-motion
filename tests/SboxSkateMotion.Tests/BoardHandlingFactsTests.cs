using System;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BoardHandlingFactsTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void AThrowHoldsTheBoardUntilItIsLetGo()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Throwing, HoldingBoard = true }, false, Step );
		Assert.False( facts.NoBoard );
		Assert.Equal( "offboard", facts.Physics );
		Assert.Equal( 1f, facts.Facts[ "is-holding-skateboard" ] );
		Assert.Equal( 1f, facts.Facts[ "is-dropping-skateboard" ] );
		Assert.True( facts.Intents.ContainsKey( "ob-throw-board" ) );
	}

	[Fact]
	public void SteppingStraightFromTheBoardOntoFootIsASpawn()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding }, false, Step );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Step );
		Assert.True( facts.Intents.ContainsKey( "ob-spawn-dismount" ) );
		for ( var frame = 0; frame < 20; frame++ )
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Step );
		Assert.False( facts.Intents.ContainsKey( "ob-spawn-dismount" ) );
		// Stepping off normally, or getting up from a bail, is not one.
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Bailed }, false, Step );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Step );
		Assert.False( facts.Intents.ContainsKey( "ob-spawn-dismount" ) );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding }, false, Step );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Dismounting }, false, Step );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Step );
		Assert.False( facts.Intents.ContainsKey( "ob-spawn-dismount" ) );
	}

	[Theory]
	[InlineData( 1.5f, "walk" )]
	[InlineData( 3.5f, "run" )]
	[InlineData( 5f, "sprint" )]
	public void AMountKeepsTheGaitItWasStartedFrom( float speed, string gait )
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 30; frame++ )
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying, MoveSpeed = speed, MoveAmount = 1f }, false, Step );
		// Getting on, the step onto the board starts from rest; the clips still get on from the gait the rider had.
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Mounting, MoveSpeed = 0f, HoldingBoard = true }, false, Step );
		Assert.True( facts.MatchesArguments( Gait( gait ) ) );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Mounting, MoveSpeed = 0.1f, HoldingBoard = true }, false, Step );
		Assert.True( facts.MatchesArguments( Gait( gait ) ) );
	}

	[Fact]
	public void SettingOffFromAStandStepsOutOfItFirst()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 10; frame++ )
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Step );
		// At walking pace at once, the gait still reads standing for the first moment of the start.
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking, MoveSpeed = 1.5f, MoveAmount = 0.4f }, false, Step );
		Assert.True( facts.MatchesArguments( Gait( "stand" ) ) );
		for ( var frame = 0; frame < 10; frame++ )
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking, MoveSpeed = 1.5f, MoveAmount = 0.4f }, false, Step );
		Assert.True( facts.MatchesArguments( Gait( "walk" ) ) );
	}

	[Fact]
	public void AGrabLetGoInTheAirKeepsTheBoardsSituationWhileTheHandsTakeIt()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 10; frame++ )
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding, Airborne = true, Grab = "mutegrab" }, false, Step );
		var air = 0;
		for ( var frame = 0; frame < 30; frame++ )
		{
			// The board's controls are let go as the rider steps off: the grab is known from the riding before.
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Dismounting, Airborne = true, HoldingBoard = true }, false, Step );
			air += facts.Physics == "air" ? 1 : 0;
		}
		// The grab's let-go lasts a quarter of a second: the rider counts as on the board in the air all through it.
		Assert.InRange( air * Step, 0.27f, 0.32f );
		Assert.Equal( "offboardair", facts.Physics );
		// Without a grab the first moment is all it keeps.
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding, Airborne = true }, false, Step );
		air = 0;
		for ( var frame = 0; frame < 30; frame++ )
		{
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Dismounting, Airborne = true }, false, Step );
			air += facts.Physics == "air" ? 1 : 0;
		}
		Assert.InRange( air * Step, 0.08f, 0.12f );
	}

	[Fact]
	public void AGrabLetGoOfEarlyInItsIntoStaysInTheAirUntilTheLetGoEnds()
	{
		// Let go of 0.2 s before the grab's into clip ends, the let-go waits for that end and then plays 0.23 s of its own,
		// ending 0.43 s after the press: past the 0.3 s the rider keeps the board's situation by itself. While the graph
		// plays the let-go the rider stays on the board in the air, so the graph goes on into the air dismount rather than a
		// step off on the ground.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 10; frame++ )
			source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding, Airborne = true, Grab = "mutegrab" }, false, Step );
		var stepping = new RiderAnimationInput { Activity = RiderActivity.Dismounting, Airborne = true, HoldingBoard = true };
		var frames = 0;
		for ( ; frames < 12; frames++ )
		{
			source.Fill( facts, stepping, false, Step );
			Assert.Equal( "air", facts.Physics );
		}
		source.GraphAirDismounting = true;
		for ( ; frames < 26; frames++ )
		{
			source.Fill( facts, stepping, false, Step );
			Assert.True( facts.Physics == "air", $"{facts.Physics} {frames * Step:0.000} s after the press" );
		}
		// The let-go over, the rider is off the board in the air.
		source.GraphAirDismounting = false;
		source.Fill( facts, stepping, false, Step );
		Assert.Equal( "offboardair", facts.Physics );
	}

	[Fact]
	public void ABoardInTheHandIsNotUnderTheRider()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Mounting, Airborne = true }, false, Step );
		Assert.Equal( 1f, facts.Facts[ "can-land-on-board" ] );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Mounting, Airborne = true, HoldingBoard = true }, false, Step );
		Assert.Equal( 0f, facts.Facts[ "can-land-on-board" ] );
		Assert.Equal( 1f, facts.Facts[ "is-in-biped-air" ] );
	}

	[Theory]
	// A jump rises as it leaves the ground: a long one is big, a short hop small; both land level.
	[InlineData( 4.2f, 0.86f, "big-fwd" )]
	[InlineData( 1f, 0.3f, "sml-fwd" )]
	// Already falling as the feet leave the ground, the rider stepped off an edge: it lands lower.
	[InlineData( -0.5f, 0.45f, "sml-dn" )]
	[InlineData( -0.5f, 0.9f, "big-dn" )]
	public void AJumpOnFootIsClassedAsItsFeetLeaveTheGround( float rising, float flight, string arc )
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying }, false, Step );
		Assert.Equal( 0f, facts.Facts[ "ob-jump-arc" ] );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying, Airborne = true, VerticalSpeed = rising, TimeToLand = flight }, false, Step );
		Assert.True( facts.MatchesArguments( Arc( arc ) ) );
		// Kept to the landing, however the flight goes on.
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying, Airborne = true, VerticalSpeed = -3f, TimeToLand = 0.1f }, false, Step );
		Assert.True( facts.MatchesArguments( Arc( arc ) ) );
		source.Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying }, false, Step );
		Assert.Equal( 0f, facts.Facts[ "ob-jump-arc" ] );
	}

	[Fact]
	public void TheGroundWalkedOnPicksTheWalkingClips()
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Carrying, GroundSlope = "stair-up-steep" }, false, Step );
		Assert.True( facts.MatchesArguments( Slope( "stair-up-steep" ) ) );
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Walking }, false, Step );
		Assert.True( facts.MatchesArguments( Slope( "flat" ) ) );
		// Riding, the board's own states play; the walking ground does not apply.
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Activity = RiderActivity.Riding, GroundSlope = "ramp-up-steep" }, false, Step );
		Assert.True( facts.MatchesArguments( Slope( "flat" ) ) );
	}

	private static StateTestDefinition Gait( string gait ) => Argument( "loco-state", "locostate", gait );

	private static StateTestDefinition Arc( string arc ) => Argument( "ob-jump-arc", "arc", arc );

	private static StateTestDefinition Slope( string slope ) => Argument( "ground-slope-type", "slopetype", slope );

	private static StateTestDefinition Argument( string fact, string name, string value )
	{
		var test = new StateTestDefinition { Test = "fact", Fact = fact, Mask = 1 };
		test.Arguments[ name ] = value;
		return test;
	}
}
