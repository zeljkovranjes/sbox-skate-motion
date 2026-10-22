using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class WindupShoveHysteresisTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void AStickHeldOnTheShoveItLeanKeepsOneWindup()
	{
		// A diagonal held still wobbles a little either side of the shove-it lean: the wind-up stays the one first chosen.
		var rider = new RiderStateFacts();
		var chosen = Windup( rider, 0.52f );
		Assert.Equal( "n-fs-pop-shuvit-anticipation", chosen );
		for ( var frame = 0; frame < 60; frame++ )
			Assert.Equal( chosen, Windup( rider, frame % 2 == 0 ? 0.47f : 0.53f ) );
	}

	[Fact]
	public void AStickHeldOnThe360LeanKeepsOneWindup()
	{
		var rider = new RiderStateFacts();
		var chosen = Windup( rider, 0.85f );
		Assert.Equal( "n-fs-pop-shuvit-anticipation", chosen );
		for ( var frame = 0; frame < 60; frame++ )
			Assert.Equal( chosen, Windup( rider, frame % 2 == 0 ? 0.89f : 0.84f ) );
	}

	[Fact]
	public void AStickMovedClearlyOverChangesTheWindup()
	{
		var rider = new RiderStateFacts();
		Assert.Equal( "n-fs-pop-shuvit-anticipation", Windup( rider, 0.6f ) );
		Assert.Equal( "nollie-anticipation", Windup( rider, 0.4f ) );
		Assert.Equal( "n-360-pop-shuvit-anticipation", Windup( rider, -0.95f ) );
		Assert.Equal( "n-pop-shuvit-anticipation", Windup( rider, -0.7f ) );
	}

	[Fact]
	public void ANewWindupChoosesAfresh()
	{
		// Let go between them, the next wind-up takes the lean as it is.
		var rider = new RiderStateFacts();
		Assert.Equal( "n-fs-pop-shuvit-anticipation", Windup( rider, 0.53f ) );
		for ( var frame = 0; frame < 12; frame++ )
			rider.Fill( new StateFacts(), new RiderAnimationInput(), false, Frame );
		Assert.Equal( "nollie-anticipation", Windup( rider, 0.47f ) );
	}

	// One frame wound up off the nose with this lean (positive toward the toes, frontside), and the wind-up asked for.
	private static string Windup( RiderStateFacts rider, float spin )
	{
		var facts = new StateFacts();
		rider.Fill( facts, new RiderAnimationInput { Windup = 1f, WindupNose = true, WindupSpin = spin }, false, Frame );
		foreach ( var intent in facts.Intents.Keys )
			if ( intent.EndsWith( "-anticipation" ) )
				return intent;
		return null;
	}
}
