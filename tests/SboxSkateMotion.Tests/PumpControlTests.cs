using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PumpControlTests
{
	private const float Step = 1f / 120f;

	// Holds pump for a while, then lets go; returns the speed gained over the whole stroke.
	private static float Stroke( float speed, float groundTurn, float crouch = 0.6f, float stand = 1f )
	{
		var pump = new PumpControl();
		var gained = 0f;
		for ( var t = 0f; t < crouch + stand; t += Step )
			gained += pump.Acceleration( t < crouch, speed, groundTurn, Step ) * Step;
		return gained;
	}

	[Fact]
	public void CrouchingGivesNothingAndStandingUpInATransitionPumps()
	{
		var pump = new PumpControl();
		for ( var t = 0f; t < 0.6f; t += Step )
			Assert.Equal( 0f, pump.Acceleration( true, 5f, 2f, Step ) );
		Assert.True( pump.Height < PumpControl.Standing - 0.3f );
		Assert.InRange( Stroke( 5f, 2f ), 0.5f, 5f );
	}

	[Fact]
	public void DroppingIntoATransitionPumpsALittleWithoutTheButton()
	{
		// Held low on steep ground, the rider rises as the ground flattens out: a share of a pump, set by the difficulty.
		float Drop( float unheld )
		{
			var pump = new PumpControl();
			for ( var t = 0f; t < 0.5f; t += Step )
				pump.Acceleration( false, 5f, 2f, Step, 1.2f, unheld );
			Assert.True( pump.Crouch > 0.5f );
			var gained = 0f;
			for ( var t = 0f; t < 1f; t += Step )
				gained += pump.Acceleration( false, 5f, 2f, Step, 0f, unheld ) * Step;
			return gained;
		}
		Assert.True( Drop( 0.7f ) > 0.2f );
		Assert.Equal( 0f, Drop( 0f ) );
		Assert.True( Drop( 1f ) > Drop( 0.7f ) );
	}

	[Fact]
	public void FlatGroundAndGroundCurvingAwayGiveNothing()
	{
		Assert.Equal( 0f, Stroke( 5f, 0f ) );
		Assert.Equal( 0f, Stroke( 5f, -2f ) );
	}

	[Fact]
	public void PumpingFadesWithSpeedAndNeverPassesTheCap()
	{
		Assert.True( Stroke( 2f, 2f ) > Stroke( 9f, 2f ) );
		Assert.True( Stroke( 15f, 2f ) < 0.1f );
		var pump = new PumpControl();
		for ( var t = 0f; t < 0.6f; t += Step )
			pump.Acceleration( true, 0f, 50f, Step );
		for ( var t = 0f; t < 0.5f; t += Step )
			Assert.True( pump.Acceleration( false, 0f, 50f, Step ) <= PumpControl.MaximumAcceleration );
	}
}
