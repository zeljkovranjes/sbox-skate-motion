using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TiltBlendTests
{
	[Theory]
	[InlineData( -1f )]
	[InlineData( 1f )]
	public void CrossingTiltThresholdDoesNotReplaceTheWholePose( float side )
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		director.Update( new RiderAnimationInput { DeckRoll = side * .699f }, 1f / 60f, parameters );
		var before = parameters.Number( "blend-wall" );
		director.Update( new RiderAnimationInput { DeckRoll = side * .701f }, 1f / 60f, parameters );
		Assert.InRange( MathF.Abs( parameters.Number( "blend-wall" ) - before ), 0f, .01f );
	}

	[Fact]
	public void FullTiltAndReleaseRemainContinuousAndConverge()
	{
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		for ( var frame = 0; frame < 120; frame++ )
			director.Update( new RiderAnimationInput { DeckRoll = 1f }, 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-wall" ), .99f, 1f );
		director.Update( new RiderAnimationInput(), 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-wall" ), .8f, .99f );
		for ( var frame = 0; frame < 120; frame++ )
			director.Update( new RiderAnimationInput(), 1f / 60f, parameters );
		Assert.InRange( parameters.Number( "blend-wall" ), 0f, .01f );
	}
}
