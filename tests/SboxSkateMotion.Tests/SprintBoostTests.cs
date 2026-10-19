using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class SprintBoostTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void SprintingRaisesTheTopSpeedAndItEasesBackAfterLettingGo()
	{
		var sprint = new SprintBoost();
		Assert.Equal( 6f, sprint.TopSpeed( false, 6f, Step ) );
		Assert.Equal( SprintBoost.SprintSpeed, sprint.TopSpeed( true, 6f, Step ) );
		// Still sprinting just after letting go, then slowing a little at a time.
		var top = 0f;
		for ( var t = 0f; t < SprintBoost.Hold; t += Step )
			top = sprint.TopSpeed( false, 6f, Step );
		Assert.True( top > 9.9f );
		var previous = top;
		for ( var t = 0f; t < 1f; t += Step )
		{
			top = sprint.TopSpeed( false, 6f, Step );
			Assert.True( top <= previous && previous - top < 0.05f );
			previous = top;
		}
		for ( var t = 0f; t < SprintBoost.Fade; t += Step )
			top = sprint.TopSpeed( false, 6f, Step );
		Assert.Equal( 6f, top, 3 );
	}
}
