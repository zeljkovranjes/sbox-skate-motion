using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LandingBlendTests
{
	[Fact]
	public void TouchdownKeepsTheFallSpeedAndPicksANewVariant()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		// Falling at 7 m/s; the first contact reports a much softer landing (one wheel touching first).
		director.Update( new RiderAnimationInput { Airborne = true, VerticalSpeed = -7f }, 1f / 60f, parameters );
		var before = parameters.Text( "random" );
		director.Update( new RiderAnimationInput { LandingSpeed = 1.5f }, 1f / 60f, parameters );
		Assert.Equal( 7f, parameters.Number( "avgvely" ) );
		Assert.NotEqual( before, parameters.Text( "random" ) );
		Assert.Contains( parameters.Text( "random" ), new[] { "0", "1", "2" } );

		// It holds through the landing and the next, gentle drop reads as light.
		director.Update( new RiderAnimationInput(), 1f / 60f, parameters );
		Assert.Equal( 7f, parameters.Number( "avgvely" ) );
		director.Update( new RiderAnimationInput { Airborne = true, VerticalSpeed = -1f }, 1f / 60f, parameters );
		director.Update( new RiderAnimationInput { LandingSpeed = 1f }, 1f / 60f, parameters );
		Assert.Equal( 1f, parameters.Number( "avgvely" ) );
	}

	[Fact]
	public void ADeckSpinningThroughUpsideDownIsNotDarkSideUp()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		// A flip: the deck's up axis sweeps through upside down and back within a few frames.
		foreach ( var up in new[] { 1f, 0.2f, -0.6f, -1f, -0.6f, 0.2f, 1f } )
		{
			source.Fill( facts, new RiderAnimationInput { Airborne = true, DeckUp = up }, false, 1f / 60f );
			Assert.Equal( 0f, facts.Facts[ "is-dark" ] );
		}

		// Staying upside down and steady counts after a moment.
		for ( var frame = 0; frame < 6; frame++ )
			source.Fill( facts, new RiderAnimationInput { DeckUp = -1f }, false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "is-dark" ] );
		for ( var frame = 0; frame < 6; frame++ )
			source.Fill( facts, new RiderAnimationInput { DeckUp = -1f }, false, 1f / 60f );
		Assert.Equal( 1f, facts.Facts[ "is-dark" ] );
	}

	[Fact]
	public void ALandingAbsorbsForHalfASecond()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { TimeSinceLanding = 0.1f }, false, 1f / 60f );
		Assert.Equal( 1f, facts.Facts[ "clip-flag:absorb-land" ] );
		source.Fill( facts, new RiderAnimationInput { TimeSinceLanding = 0.6f }, false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "clip-flag:absorb-land" ] );
		source.Fill( facts, new RiderAnimationInput { Airborne = true }, false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "clip-flag:absorb-land" ] );
	}

	[Fact]
	public void SketchyAndSpinLandingsReachTheGraph()
	{
		var facts = new StateFacts();
		var source = new RiderStateFacts();
		source.Fill( facts, new RiderAnimationInput { LandingKind = "sketchy", TimeSinceLanding = 0.1f }, false, 1f / 60f );
		Assert.Equal( 1f, facts.Facts[ "has-landing-type" ] );
		Assert.True( facts.MatchesArguments( LandingType( "sketchy" ) ) );
		Assert.False( facts.MatchesArguments( LandingType( "spin" ) ) );
		source.Fill( facts, new RiderAnimationInput(), false, 1f / 60f );
		Assert.Equal( 0f, facts.Facts[ "has-landing-type" ] );
	}

	private static StateTestDefinition LandingType( string value ) => new()
	{
		Test = "fact", Fact = "has-landing-type", Mask = 1, Arguments = { [ "landing-type" ] = value }
	};
}
