using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindTwistBlendTests
{
	private static readonly RiderAnimationInput Sliding = new() { Airborne = true, Grind = "boardslide" };

	[Fact]
	public void LettingGoOfATurnOnARailEasesTheTwistBack()
	{
		// A grind's and a slide's pose leans over the rail by the twist blend: a key let go must not jump it across.
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 120; frame++ )
			director.Update( Sliding with { Steer = 1f }, 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-twist" ), .99f, 1f );
		director.Update( Sliding, 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-twist" ), .8f, .99f );
		Assert.Equal( 0f, parameters.Number( "steer" ) );
		for ( var frame = 0; frame < 120; frame++ )
			director.Update( Sliding, 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-twist" ), 0f, .01f );
	}

	[Fact]
	public void TwistFollowsTheMirroredSteering()
	{
		// The clips are mirrored for the other stance, and the twist with them.
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 120; frame++ )
			director.Update( Sliding with { Steer = 1f, Goofy = true }, 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-twist" ), -1f, -.99f );
	}
}
