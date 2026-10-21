using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AirFactsTests
{
	private const float Frame = 1f / 60f;

	[Theory]
	[InlineData( "mutegrab", "mute-grab" )]
	[InlineData( "stalegrab", "stale-grab" )]
	[InlineData( "melon", "bs-grab" )]
	[InlineData( "method", "fs-grab" )]
	[InlineData( "crailgrab", "dbl-grab" )]
	[InlineData( "nosegrab", "nose-grab" )]
	[InlineData( "tailgrab", "tail-grab" )]
	public void GrabsAreAskedForByTheGraphsOwnNames( string grab, string intent )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Airborne = true, Grab = grab }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( intent ) );
		Assert.True( facts.Intents.ContainsKey( "grab" ) );
	}

	[Fact]
	public void AWindupLetGoWaitsForItsPopBeforeCountingAsGivenUp()
	{
		// Letting go of a wind-up pops the board on the next physics step, which can come a frame after the animation
		// sees the release: the wind-up is still asked for until the pop arrives, and on the pop's frame too, so the
		// graph goes from the wind-up straight into the trick instead of the wind-up being given up first. With no pop
		// it lets go a moment later.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Windup = 1f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		source.Fill( facts, new RiderAnimationInput(), false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		source.Fill( facts, new RiderAnimationInput { Trick = "ollie", TimeSincePop = 0f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie" ) );
		Assert.True( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		for ( var frame = 0; frame < 10; frame++ )
			source.Fill( facts, new RiderAnimationInput { Trick = "ollie", TimeSincePop = 0.1f }, false, Frame );
		Assert.False( facts.Intents.ContainsKey( "ollie-anticipation" ) );

		var given = new RiderStateFacts();
		given.Fill( facts, new RiderAnimationInput { Windup = 1f }, false, Frame );
		for ( var frame = 0; frame < 10; frame++ )
			given.Fill( facts, new RiderAnimationInput(), false, Frame );
		Assert.False( facts.Intents.ContainsKey( "ollie-anticipation" ) );
	}

	[Fact]
	public void AWindupHeldWhilePushingIsNotAlsoAPush()
	{
		// Push and the wind-up held together: the rider crouches for the pop instead of pushing, so the graph is not
		// asked for both and does not swap between the push and the wind-up every frame. Let go, the push comes back.
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Pushing = true, Windup = 1f }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "ollie-anticipation" ) );
		Assert.False( facts.Intents.ContainsKey( "left-push" ) || facts.Intents.ContainsKey( "right-push" ) );
		for ( var frame = 0; frame < 10; frame++ )
			source.Fill( facts, new RiderAnimationInput { Pushing = true }, false, Frame );
		Assert.True( facts.Intents.ContainsKey( "left-push" ) );
	}

	[Theory]
	[InlineData( "fiftyfifty", true, "fs-50-50" )]
	[InlineData( "fiftyfifty", false, "bs-50-50" )]
	[InlineData( "fivezero", false, "bs-5-o" )]
	[InlineData( "nosegrind", true, "fs-nosegrind" )]
	[InlineData( "boardslide", false, "bs-board" )]
	public void GrindsAreToldToTheGraphByItsOwnNamesWithTheirSide( string grind, bool frontside, string named )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Grind = grind, GrindFrontside = frontside }, false, Frame );
		Assert.Equal( "grind", facts.Physics );
		Assert.Equal( named, facts.Grind );
	}

	[Fact]
	public void TheTweakIsAlwaysReportedInTheAirSoAGrabCanLetGo()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { Airborne = true }, false, Frame );
		Assert.Equal( 0f, facts.Intents[ "tweak-y" ] );
		source.Fill( facts, new RiderAnimationInput { Airborne = true, Adjust = 0.4f }, false, Frame );
		Assert.Equal( 0.4f, facts.Intents[ "tweak-y" ] );
	}

	[Fact]
	public void ABodyFlipIsNotTheDarkSide()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 30; frame++ )
			source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = -1f, BodyFlipping = true }, false, Frame );
		Assert.Equal( 1f, facts.Facts[ "is-body-flipping" ] );
		Assert.Equal( 0f, facts.Facts[ "is-dark" ] );
	}

	[Fact]
	public void ABoardCaughtDarkSideUpAndSlidOnARailIsADarkslideUntilTheSlideEnds()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 20; frame++ )
			source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = -1f }, false, Frame );
		Assert.False( source.Darksliding );
		source.Fill( facts, new RiderAnimationInput { Grind = "boardslide", DeckUp = -1f }, false, Frame );
		Assert.True( source.Darksliding );
		// The board shown is then the clip's, upside down, so the latch holds it whatever the deck reads.
		source.Fill( facts, new RiderAnimationInput { Grind = "boardslide", DeckUp = 1f }, false, Frame );
		Assert.True( source.Darksliding );
		source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = -1f }, false, Frame );
		Assert.False( source.Darksliding );
	}

	[Theory]
	[InlineData( "boardslide", 1f )]
	[InlineData( "fiftyfifty", -1f )]
	public void OnlyASlideCaughtDarkSideUpIsADarkslide( string grind, float deckUp )
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		for ( var frame = 0; frame < 20; frame++ )
			source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = deckUp }, false, Frame );
		source.Fill( facts, new RiderAnimationInput { Grind = grind, DeckUp = deckUp }, false, Frame );
		Assert.False( source.Darksliding );
	}

	[Fact]
	public void TimeToLandUsesThePredictionWhenThereIsOne()
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Airborne = true, TimeToLand = 1.2f }, false, Frame );
		Assert.Equal( 1.2f, facts.Facts[ "time-to-land" ] );
	}
}
