using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

// The board adjust's weight across a take-off and as the stick is let go in the air: it follows from rest over a few
// frames, never switched in one (letting the stick go straightened a leg 37 degrees in a frame) and never at its fastest
// on its first frame (which threw the arms 7 degrees a frame faster as a board adjust began).
public class BoardAdjustBlendEaseTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void TheStickHeldThroughATakeOffEasesItsWeightInFromRest()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 30; frame++ )
			director.Update( new RiderAnimationInput { Airborne = false }, Frame, parameters );
		Assert.Equal( 0f, parameters.Number( "blend-impact" ), 3 );
		var weights = Held( director, parameters, new RiderAnimationInput { Airborne = true, AirStickY = -1f }, 30 );
		// The first frame in the air takes only a little of the stick, and the next takes more: the weight starts from rest.
		// It rises every frame, by no more than a sixth at a time, and has reached the stick within half a second.
		Assert.InRange( weights[ 0 ], 0.005f, 0.1f );
		Assert.True( weights[ 1 ] - weights[ 0 ] > weights[ 0 ] );
		for ( var frame = 1; frame < weights.Length; frame++ )
		{
			Assert.True( weights[ frame ] >= weights[ frame - 1 ] );
			Assert.True( weights[ frame ] - weights[ frame - 1 ] <= 1f / 6f );
		}
		Assert.True( weights[ ^1 ] > 0.99f );
	}

	[Fact]
	public void LettingTheStickGoInTheAirEasesTheWeightOutWithoutPassingNothing()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		Held( director, parameters, new RiderAnimationInput { Airborne = true, AirStickY = -0.6f }, 60 );
		var weights = Held( director, parameters, new RiderAnimationInput { Airborne = true }, 60 );
		Assert.InRange( weights[ 0 ], 0.5f, 0.6f );
		for ( var frame = 1; frame < weights.Length; frame++ )
		{
			Assert.True( weights[ frame ] <= weights[ frame - 1 ] );
			Assert.True( weights[ frame ] >= 0f );
		}
		Assert.True( weights[ ^1 ] < 0.01f );
	}

	[Fact]
	public void LandingWithTheStickHeldEasesBackToTheLanding()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		Held( director, parameters, new RiderAnimationInput { Airborne = true, AirStickY = -1f }, 40 );
		director.Update( new RiderAnimationInput { Airborne = false, LandingSpeed = 0f }, Frame, parameters );
		Assert.InRange( parameters.Number( "blend-impact" ), 0.85f, 0.999f );
	}

	private static float[] Held( RiderAnimationDirector director, AnimationParameters parameters, RiderAnimationInput input, int frames )
	{
		var weights = new float[ frames ];
		for ( var frame = 0; frame < frames; frame++ )
		{
			director.Update( input, Frame, parameters );
			weights[ frame ] = parameters.Number( "blend-impact" );
		}
		return weights;
	}
}
