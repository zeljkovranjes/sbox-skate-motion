using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WindupCompressionTests
{
	private const float Step = 1f / 60f;

	[Theory]
	[InlineData( 1f, 0f, 1f )]
	[InlineData( 0.7f, 0f, 0.7f )]
	[InlineData( 0.7f, 0.7f, 0.99f )]
	[InlineData( 0.9f, -0.9f, 1f )]
	public void TheCrouchIsAsDeepAsTheStickIsPulled( float pull, float spin, float expected )
	{
		// The wind-up's clips blend on the trick stick's magnitude, pulled back and across alike.
		var parameters = new AnimationParameters();
		new RiderAnimationDirector( false ).Update( new RiderAnimationInput { Windup = pull, WindupSpin = spin }, Step, parameters );
		Assert.Equal( expected, parameters.Number( "blend-compression" ), 2 );
	}

	[Fact]
	public void TheDepthStaysOnceTheStickIsLetGo()
	{
		// Let go, the crouch comes back up out of the depth it was wound to, not out of the high crouch.
		var director = new RiderAnimationDirector( false );
		var parameters = new AnimationParameters();
		director.Update( new RiderAnimationInput { Windup = 0.8f }, Step, parameters );
		director.Update( new RiderAnimationInput(), Step, parameters );
		Assert.Equal( 0.8f, parameters.Number( "blend-compression" ), 3 );
	}

	[Fact]
	public void NeverWoundTheCrouchIsHigh()
	{
		var parameters = new AnimationParameters();
		new RiderAnimationDirector( false ).Update( new RiderAnimationInput(), Step, parameters );
		Assert.True( parameters.TryGetNumber( "blend-compression", out var compression ) );
		Assert.Equal( 0f, compression );
	}
}
