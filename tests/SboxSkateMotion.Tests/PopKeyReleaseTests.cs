using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PopKeyReleaseTests
{
	private const float Frame = 1f / 60f;

	// Holds the key for the given seconds, then lets it go; returns what each step gave back.
	private static int[] Hold( PopKeyRelease key, int winding, float seconds, bool airborne )
	{
		var steps = new System.Collections.Generic.List<int>();
		for ( var t = 0f; t < seconds; t += Frame )
			steps.Add( key.Step( winding, airborne, Frame ) );
		steps.Add( key.Step( 0, airborne, Frame ) );
		steps.Add( key.Step( 0, airborne, Frame ) );
		return steps.ToArray();
	}

	[Theory]
	[InlineData( -1, 0.05f )]
	[InlineData( -1, 0.3f )]
	[InlineData( -1, 1f )]
	[InlineData( -1, 3f )]
	[InlineData( 1, 1f )]
	public void OnTheGroundEveryReleasePops( int winding, float seconds )
	{
		var steps = Hold( new PopKeyRelease(), winding, seconds, false );
		Assert.Equal( winding, steps[ ^2 ] );
		// Once, on the step it is let go.
		Assert.Single( steps, s => s != 0 );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 1 )]
	public void InTheAirAQuickTapFlicks( int winding )
	{
		var steps = Hold( new PopKeyRelease(), winding, 0.15f, true );
		Assert.Equal( winding, steps[ ^2 ] );
	}

	[Theory]
	[InlineData( -1, 0.35f )]
	[InlineData( -1, 1f )]
	[InlineData( 1, 1f )]
	public void InTheAirALongerHoldIsABoardAdjustWhoseReleaseFlicksNothing( int winding, float seconds )
	{
		Assert.All( Hold( new PopKeyRelease(), winding, seconds, true ), s => Assert.Equal( 0, s ) );
	}

	[Fact]
	public void TheHoldIsCountedFromThePressNotFromTakeOff()
	{
		// Wound up on the ground and let go in the air after a long hold (rolled off a ledge crouched): no flick.
		var key = new PopKeyRelease();
		for ( var i = 0; i < 30; i++ )
			key.Step( -1, false, Frame );
		for ( var i = 0; i < 10; i++ )
			key.Step( -1, true, Frame );
		Assert.Equal( 0, key.Step( 0, true, Frame ) );
	}

	[Fact]
	public void ATapAfterALongHoldFlicksAgain()
	{
		var key = new PopKeyRelease();
		Hold( key, -1, 1f, true );
		Assert.Equal( -1, Hold( key, -1, 0.1f, true )[ ^2 ] );
	}

	[Fact]
	public void ASpentKeyFlicksNothingWhenLetGo()
	{
		var key = new PopKeyRelease();
		key.Step( -1, false, Frame );
		key.Step( -1, false, Frame );
		key.Reset();
		Assert.Equal( 0, key.Step( 0, false, Frame ) );
	}

	[Fact]
	public void SwitchingFromOneKeyToTheOtherFlicksNothing()
	{
		var key = new PopKeyRelease();
		key.Step( -1, false, Frame );
		Assert.Equal( 0, key.Step( 1, false, Frame ) );
		Assert.Equal( 1, key.Step( 0, false, Frame ) );
	}
}
