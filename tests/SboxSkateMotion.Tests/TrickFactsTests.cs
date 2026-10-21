using System;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickFactsTests
{
	private const float Frame = 1f / 60f;

	[Theory]
	// Stick back (y -1), forward (y 1), toward the toes (x 1 for a regular stance), or centred.
	[InlineData( true, false, 0f, -1f, (int)GrabPick.Nose )]
	[InlineData( false, true, 0f, -1f, (int)GrabPick.NoseCrail )]
	[InlineData( true, true, 0f, -1f, (int)GrabPick.Rocket )]
	[InlineData( false, true, 0f, 1f, (int)GrabPick.Tail )]
	[InlineData( true, false, 0f, 1f, (int)GrabPick.Seatbelt )]
	[InlineData( true, false, 1f, 0f, (int)GrabPick.Mute )]
	[InlineData( false, true, 1f, 0f, (int)GrabPick.Stale )]
	[InlineData( true, false, -1f, 0f, (int)GrabPick.Melon )]
	[InlineData( false, true, 0f, 0f, (int)GrabPick.Indy )]
	[InlineData( true, true, 0f, 0f, (int)GrabPick.Double )]
	public void TheHandsAndTheStickPickTheGrab( bool front, bool back, float x, float y, int pick )
	{
		Assert.Equal( (GrabPick)pick, GrabSelection.Pick( front, back, x, y, false ) );
	}

	[Fact]
	public void TheToesAreOnTheOtherSideForAGoofyStance()
	{
		Assert.Equal( GrabPick.Mute, GrabSelection.Pick( true, false, -1f, 0f, true ) );
		Assert.Equal( GrabPick.Melon, GrabSelection.Pick( true, false, 1f, 0f, true ) );
	}

	[Fact]
	public void TheGrabIsPickedAsTheHandReachesAndThenKept()
	{
		var selection = new GrabSelection();
		// With the stick centred the grab waits a moment for it, so a stick moved as the hand reaches still picks it.
		Assert.Equal( GrabPick.None, selection.Step( true, false, 0f, 0f, false, Frame ) );
		Assert.Equal( GrabPick.Nose, selection.Step( true, false, 0f, -1f, false, Frame ) );
		for ( var frame = 0; frame < 20; frame++ )
			selection.Step( true, false, 0f, -1f, false, Frame );
		// Once settled, moving the stick tweaks the grab; it does not change it.
		Assert.Equal( GrabPick.Nose, selection.Step( true, false, 0f, 1f, false, Frame ) );
		// The other hand joining in picks again, the nose grab carrying on until it does.
		Assert.Equal( GrabPick.Nose, selection.Step( true, true, 0f, 0f, false, Frame ) );
		for ( var frame = 0; frame < 6; frame++ )
			selection.Step( true, true, 0f, 0f, false, Frame );
		Assert.Equal( GrabPick.Double, selection.Step( true, true, 0f, 0f, false, Frame ) );
		Assert.Equal( GrabPick.None, selection.Step( false, false, 0f, 0f, false, Frame ) );
		// Left centred, it is the plain grab once the wait is over.
		for ( var frame = 0; frame < 6; frame++ )
			selection.Step( true, false, 0f, 0f, false, Frame );
		Assert.Equal( GrabPick.Melon, selection.Step( true, false, 0f, 0f, false, Frame ) );
	}

	[Theory]
	[InlineData( "nosegrab", "board-adjust-down" )]
	[InlineData( "nosecrail", "board-adjust-down" )]
	[InlineData( "rocketair", "board-adjust-down" )]
	[InlineData( "tailgrab", "board-adjust-up" )]
	[InlineData( "seatbeltgrab", "board-adjust-up" )]
	public void NoseAndTailGrabsAskForTheirBoardAdjust( string grab, string adjust )
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Grab = grab } );
		Assert.True( facts.Intents.ContainsKey( adjust ) );
	}

	[Theory]
	// Without a grab the trick stick held in the air adjusts the board: back (y -1) the tail, forward the nose, toward
	// the toes (x 1) or the heels; a light touch or the ground does nothing.
	[InlineData( true, 0f, -1f, "board-adjust-down" )]
	[InlineData( true, 0f, 1f, "board-adjust-up" )]
	[InlineData( true, 1f, 0f, "board-adjust-right" )]
	[InlineData( true, -0.7f, -0.2f, "board-adjust-left" )]
	[InlineData( true, 0f, -0.45f, "board-adjust-down" )]
	[InlineData( true, 0.2f, 0.1f, null )]
	[InlineData( false, 0f, -1f, null )]
	public void TheTrickStickHeldInTheAirWithoutAGrabAdjustsTheBoard( bool airborne, float x, float y, string intent )
	{
		var facts = Fill( new RiderAnimationInput { Airborne = airborne, AirStickX = x, AirStickY = y } );
		var adjusts = new[] { "board-adjust-down", "board-adjust-up", "board-adjust-left", "board-adjust-right" };
		foreach ( var adjust in adjusts )
			Assert.Equal( adjust == intent, facts.Intents.ContainsKey( adjust ) );
		if ( intent is not null )
			Assert.Equal( MathF.Min( 1f, MathF.Sqrt( x * x + y * y ) ), facts.Intents[ "board-adjust-mag" ], 3 );
	}

	[Fact]
	public void AGrabsOwnFamilyAdjustsTheBoardInsteadOfTheStick()
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Grab = "melon", AirStickX = 1f } );
		Assert.False( facts.Intents.ContainsKey( "board-adjust-right" ) );
	}

	[Fact]
	public void TheBoardAdjustsMagnitudeWeightsItsBlendsInTheAir()
	{
		var parameters = new AnimationParameters();
		new RiderAnimationDirector( false ).Update( new RiderAnimationInput { Airborne = true, AirStickY = -0.6f }, Frame, parameters );
		Assert.Equal( 0.6f, parameters.Number( "blend-impact" ), 3 );
	}

	[Theory]
	[InlineData( "into", true, "fs" )]
	[InlineData( "cycle", false, "bs" )]
	[InlineData( "out", true, "fs" )]
	public void AHandPlantIsToldWithItsSideAndPart( string part, bool frontside, string side )
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Handplant = part, HandplantFrontside = frontside } );
		Assert.Equal( 1f, facts.Facts[ "is-hand-planting" ] );
		Assert.True( facts.MatchesArguments( Test( "is-hand-planting", ("state", "air"), ("dir", side) ) ) );
		Assert.Equal( part is "into" or "out" ? 1f : 0f, facts.Facts[ "should-play-hand-plant-anim" ] );
		Assert.True( facts.MatchesArguments( Test( "should-play-hand-plant-anim", ("anim", part) ) ) );
		facts = Fill( new RiderAnimationInput { Airborne = true } );
		Assert.Equal( 0f, facts.Facts[ "is-hand-planting" ] );
	}

	[Theory]
	[InlineData( 1, false, "hand-plant-one-foot-left" )]
	[InlineData( 2, false, "hand-plant-one-foot-right" )]
	[InlineData( 3, false, "hand-plant-dismount" )]
	[InlineData( 0, true, "hand-plant-dismount" )]
	public void TheFeetComeOffAPlantedHand( int feet, bool catching, string intent )
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Handplant = "cycle", FeetOff = feet, Catch = catching } );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	private static StateTestDefinition Test( string fact, params (string Name, string Value)[] arguments )
	{
		var test = new StateTestDefinition { Fact = fact };
		foreach ( var (name, value) in arguments )
			test.Arguments[ name ] = value;
		return test;
	}

	[Fact]
	public void TheTweakIsToldToTheGraph()
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Grab = "melon", TweakX = 0.6f, TweakY = 0.8f } );
		Assert.Equal( 1f, facts.Facts[ "has-tweak" ], 3 );
		facts = Fill( new RiderAnimationInput { Airborne = true, Grab = "melon" } );
		Assert.True( facts.Facts[ "has-tweak" ] < 0.2f );
	}

	[Theory]
	[InlineData( "indy", "fs-christ" )]
	[InlineData( "melon", "bs-christ" )]
	public void TheCatchOffAGrabIsAChristAir( string grab, string intent )
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Grab = grab, Catch = true } );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	[Fact]
	public void TheCatchWithBothHandsIsASupermanUntilLetGo()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Airborne = true, Grab = "crailgrab" }, false, Frame );
		Assert.False( facts.Intents.ContainsKey( "superman" ) );
		source.Fill( facts, new RiderAnimationInput { Airborne = true, Grab = "crailgrab", Catch = true }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "superman" ) );
		source.Fill( facts, new RiderAnimationInput { Airborne = true, Grab = "crailgrab", Catch = true }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "superman" ) );
		source.Fill( facts, new RiderAnimationInput { Airborne = true, Grab = "crailgrab" }, false, Frame );
		Assert.False( facts.Intents.ContainsKey( "superman" ) );
	}

	[Theory]
	[InlineData( 2, "right-one-foot-air" )]
	[InlineData( 1, "left-one-foot-air" )]
	public void AFootOffInTheAirIsAOneFootAir( int feet, string intent )
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Grab = "indy", FeetOff = feet } );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	[Fact]
	public void BothFeetOffANoseGrabWalkOnAir()
	{
		var facts = Fill( new RiderAnimationInput { Airborne = true, Grab = "nosegrab", FeetOff = 3 } );
		Assert.True( facts.Intents.ContainsKey( "no-foot-air-walk" ) );
	}

	[Theory]
	[InlineData( "mutegrab", "ground-bs-grab" )]
	[InlineData( "stalegrab", "ground-fs-grab" )]
	[InlineData( "crailgrab", "ground-dbl-grab" )]
	public void GrabsOnTheGroundAreTheGroundsOwn( string grab, string intent )
	{
		var facts = Fill( new RiderAnimationInput { Grab = grab } );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	[Fact]
	public void AGroundGrabHeldAWhileHasTheRiderCrouchedIntoIt()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Grab = "melon" }, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "is-crouched-enough-for-blend-to-grab-cycle" ] );
		for ( var frame = 0; frame < 30; frame++ )
			source.Fill( facts, new RiderAnimationInput { Grab = "melon" }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-crouched-enough-for-blend-to-grab-cycle" ] );
	}

	[Theory]
	[InlineData( "melon", false, false, "bs-boneless", "right-foot-plant" )]
	[InlineData( "indy", false, false, "fs-fast-plant", "left-foot-plant" )]
	[InlineData( "indy", true, false, "fs-boneless", "right-foot-plant" )]
	[InlineData( "melon", false, true, "bs-fast-plant", "left-foot-plant" )]
	public void AFootPlantPopIsABonelessOrAFastplant( string grab, bool rightFoot, bool leftFoot, string trick,
		string foot )
	{
		var facts = Fill( new RiderAnimationInput
		{
			Trick = "plant", TimeSincePop = 0f, Grab = grab, PushRight = rightFoot, PushLeft = leftFoot
		} );
		Assert.True( facts.Intents.ContainsKey( "trick" ) );
		Assert.True( facts.Intents.ContainsKey( trick ) );
		Assert.True( facts.Intents.ContainsKey( "new-foot-plant" ) );
		Assert.True( facts.Intents.ContainsKey( foot ) );
		Assert.False( facts.Intents.ContainsKey( "plant" ) );
	}

	[Fact]
	public void AFootPlantPopsOutOfTheGroundGrabItWasMadeFrom()
	{
		// Crouched into a ground grab, then the plant pops: on its first frames the board is already off the ground, yet
		// the grab still counts as the ground grab the rider crouched into, so the graph can take the foot plant.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 30; frame++ )
			source.Fill( facts, new RiderAnimationInput { Grab = "melon" }, false, Frame );
		source.Fill( facts, new RiderAnimationInput { Grab = "melon", Airborne = true, Trick = "plant", TimeSincePop = 0.02f },
			false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-crouched-enough-for-blend-to-grab-cycle" ] );
		Assert.True( facts.Intents.ContainsKey( "bs-boneless" ) );
	}

	[Fact]
	public void AnOllieFlickedAsAPushStartsIsANoComply()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Pushing = true }, false, Frame );
		source.Fill( facts, new RiderAnimationInput { Pushing = true, Trick = "ollie", TimeSincePop = 0f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "no-comply" ) );
		Assert.False( facts.Intents.ContainsKey( "ollie" ) );
		// Pushed for a while first, it is an ollie.
		var held = new RiderStateFacts();
		for ( var frame = 0; frame < 40; frame++ )
			held.Fill( facts, new RiderAnimationInput { Pushing = true }, false, Frame );
		held.Fill( facts, new RiderAnimationInput { Pushing = true, Trick = "ollie", TimeSincePop = 0f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie" ) );
	}

	[Theory]
	[InlineData( "kickflip", false, "kickflip-hold" )]
	[InlineData( "heelflip", true, "n-heelflip-hold" )]
	public void AFlipHeldAtTheEndOfItsFlickKeepsTurning( string trick, bool nollie, string intent )
	{
		var facts = Fill( new RiderAnimationInput
		{
			Airborne = true, Trick = trick, TrickNollie = nollie, TrickFlips = true, TimeSincePop = 0.3f, FlipHeld = true
		} );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	[Fact]
	public void AFlickDuringAFlipIsAnUnderflipAndTheCatchADarkCatch()
	{
		var facts = new StateFacts();
		var under = new RiderStateFacts();
		under.Fill( facts, new RiderAnimationInput
		{
			Airborne = true, Trick = "kickflip", TrickFlips = true, TimeSincePop = 0.4f, LateTrick = "heelflip"
		}, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-underflip-requested" ] );
		Assert.Equal( 1f, facts.Facts[ "clip-flag:cantranstounderflip" ] );
		Assert.False( facts.Intents.ContainsKey( "l-f-heelflip" ) );
		var dark = new RiderStateFacts();
		dark.Fill( facts, new RiderAnimationInput
		{
			Airborne = true, Trick = "kickflip", TrickFlips = true, TimeSincePop = 0.4f, Catch = true
		}, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-dark-catch-requested" ] );
		Assert.True( facts.Intents.ContainsKey( "new-dark-catch" ) );
	}

	[Fact]
	public void ACatchHeldFromBeforeThePopIsNotADarkCatchButAPressDuringTheFlipIs()
	{
		var facts = new StateFacts();
		var rider = new RiderStateFacts();
		// Held down on the ground before the pop and on through the flip.
		rider.Fill( facts, new RiderAnimationInput { Catch = true }, false, Frame );
		rider.Fill( facts, new RiderAnimationInput
		{
			Airborne = true, Trick = "kickflip", TrickFlips = true, TimeSincePop = 0.3f, Catch = true
		}, false, Frame );
		Assert.Equal( 0f, facts.Facts[ "is-dark-catch-requested" ] );
		// Let go, then pressed again during the flip.
		rider.Fill( facts, new RiderAnimationInput { Airborne = true, Trick = "kickflip", TrickFlips = true, TimeSincePop = 0.32f }, false, Frame );
		rider.Fill( facts, new RiderAnimationInput
		{
			Airborne = true, Trick = "kickflip", TrickFlips = true, TimeSincePop = 0.34f, Catch = true
		}, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-dark-catch-requested" ] );
	}

	[Fact]
	public void BrakingInAManualDragsIt()
	{
		var facts = Fill( new RiderAnimationInput { Balance = 1, Brake = 1f } );
		Assert.True( facts.Intents.ContainsKey( "manual-brake" ) );
	}

	[Theory]
	[InlineData( false, 0f, "ollie-anticipation" )]
	[InlineData( false, 0.7f, "fs-pop-shuvit-anticipation" )]
	[InlineData( false, -0.95f, "360-pop-shuvit-anticipation" )]
	[InlineData( true, 0.95f, "n-fs360-pop-shuvit-anticipation" )]
	public void AWindupLeanedToASideWindsUpAShoveIt( bool nose, float spin, string intent )
	{
		var facts = Fill( new RiderAnimationInput { Windup = 1f, WindupNose = nose, WindupSpin = spin } );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	private static StateFacts Fill( RiderAnimationInput input )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, input, false, Frame );
		return facts;
	}
}
