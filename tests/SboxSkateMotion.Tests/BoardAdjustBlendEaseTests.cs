using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

// The board adjust's weight across a take-off: eased from the ground's weight to the stick's over a few frames, not
// switched in one (every board adjust and grab a take-off went into moved the hands and fingers in a frame).
public class BoardAdjustBlendEaseTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void TheStickHeldThroughATakeOffEasesItsWeightIn()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 30; frame++ )
			director.Update( new RiderAnimationInput { Airborne = false }, Frame, parameters );
		Assert.Equal( 0f, parameters.Number( "blend-impact" ), 3 );
		var weights = new float[ 30 ];
		for ( var frame = 0; frame < weights.Length; frame++ )
		{
			director.Update( new RiderAnimationInput { Airborne = true, AirStickY = -1f }, Frame, parameters );
			weights[ frame ] = parameters.Number( "blend-impact" );
		}
		// The first frame in the air takes a little of the stick, not all of it; the weight rises every frame, by no more
		// than a quarter at a time, and has reached the stick within half a second.
		Assert.InRange( weights[ 0 ], 0.05f, 0.4f );
		for ( var frame = 1; frame < weights.Length; frame++ )
		{
			Assert.True( weights[ frame ] >= weights[ frame - 1 ] );
			Assert.True( weights[ frame ] - weights[ frame - 1 ] <= 0.25f );
		}
		Assert.True( weights[ ^1 ] > 0.99f );
	}

	[Fact]
	public void LandingWithTheStickHeldEasesBackToTheLanding()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 40; frame++ )
			director.Update( new RiderAnimationInput { Airborne = true, AirStickY = -1f }, Frame, parameters );
		director.Update( new RiderAnimationInput { Airborne = false, LandingSpeed = 0f }, Frame, parameters );
		Assert.InRange( parameters.Number( "blend-impact" ), 0.6f, 0.95f );
	}
}
