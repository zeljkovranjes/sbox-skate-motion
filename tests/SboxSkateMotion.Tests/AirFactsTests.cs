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
	public void TimeToLandUsesThePredictionWhenThereIsOne()
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Airborne = true, TimeToLand = 1.2f }, false, Frame );
		Assert.Equal( 1.2f, facts.Facts[ "time-to-land" ] );
	}
}
