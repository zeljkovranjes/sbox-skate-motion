using SkateMotion.Core;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TrickRecognitionTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void RollingTheStickWhileKickflippingAddsSpin()
	{
		Assert.Equal( TrickKind.Kickflip, Flick( new[] { -0.4f }, -0.8f ) );
		Assert.Equal( TrickKind.VarialKickflip, Flick( new[] { 0.6f, -0.6f }, -0.8f ) );
		Assert.Equal( TrickKind.Hardflip, Flick( new[] { -0.6f, 0.6f }, -0.8f ) );
		Assert.Equal( TrickKind.Treflip, Flick( new[] { 1f, -1f }, -0.8f ) );
	}

	[Fact]
	public void HeelflipSpinsBecomeVarialInwardAndLaserFlips()
	{
		Assert.Equal( TrickKind.Heelflip, Flick( new[] { 0.4f }, 0.8f ) );
		Assert.Equal( TrickKind.VarialHeelflip, Flick( new[] { -0.6f, 0.6f }, 0.8f ) );
		Assert.Equal( TrickKind.InwardHeelflip, Flick( new[] { 0.6f, -0.6f }, 0.8f ) );
		Assert.Equal( TrickKind.Laserflip, Flick( new[] { -1f, 1f }, 0.8f ) );
	}

	[Fact]
	public void FlipAndSpinAmountsMatchTheTrick()
	{
		Assert.Equal( (-1f, 2f), (TrickMotion.Flip( TrickKind.Treflip ), TrickMotion.Spin( TrickKind.Treflip )) );
		Assert.Equal( (1f, 1f), (TrickMotion.Flip( TrickKind.InwardHeelflip ), TrickMotion.Spin( TrickKind.InwardHeelflip )) );
		Assert.Equal( (0f, 2f), (TrickMotion.Flip( TrickKind.Shuvit360 ), TrickMotion.Spin( TrickKind.Shuvit360 )) );
		Assert.Equal( (0f, 0f), (TrickMotion.Flip( TrickKind.Ollie ), TrickMotion.Spin( TrickKind.Ollie )) );
	}

	// Pull the stick back, roll it sideways through `path` in small steps, then flick forward ending at `release`.
	private static TrickKind Flick( float[] path, float release )
	{
		var tricks = new SkateTricks();
		var trick = tricks.Step( new ControlInput( FlickY: -1f ), true, Step );
		var x = 0f;
		foreach ( var target in path )
		{
			for ( var i = 1; i <= 8 && trick == TrickKind.None; i++ )
				trick = tricks.Step( new ControlInput( FlickX: x + (target - x) * i / 8f, FlickY: -1f ), true, Step );
			x = target;
		}
		for ( var i = 0; i < 3 && trick == TrickKind.None; i++ )
			trick = tricks.Step( new ControlInput( FlickX: release, FlickY: 0.8f ), true, Step );
		return trick;
	}
}
