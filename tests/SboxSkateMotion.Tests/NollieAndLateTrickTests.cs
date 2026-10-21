using SkateMotion.Core;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class NollieAndLateTrickTests
{
	private const float Step = 1f / 60f;

	[Fact]
	public void AFullFrontsideRollOnAKickflipIsA360HardflipAndBacksideOnAHeelflipA360InwardHeelflip()
	{
		Assert.Equal( TrickKind.Hardflip360, Flick( new[] { -1f, 1f }, -0.8f ) );
		Assert.Equal( TrickKind.InwardHeelflip360, Flick( new[] { 1f, -1f }, 0.8f ) );
		Assert.Equal( (-1f, 2f), (TrickMotion.Flip( TrickKind.Hardflip360 ), TrickMotion.Spin( TrickKind.Hardflip360 )) );
		Assert.Equal( (1f, 2f), (TrickMotion.Flip( TrickKind.InwardHeelflip360 ), TrickMotion.Spin( TrickKind.InwardHeelflip360 )) );
	}

	[Fact]
	public void AFlipPoppedOffTheNoseRemembersIt()
	{
		var tricks = new SkateTricks();
		tricks.Step( new ControlInput( FlickY: 1f ), true, Step );
		tricks.Step( new ControlInput( FlickY: 1f ), true, Step );
		var trick = tricks.Step( new ControlInput( FlickX: -0.8f, FlickY: -0.8f ), true, Step );
		Assert.Equal( TrickKind.Kickflip, trick );
		Assert.True( tricks.Nose );
	}

	[Theory]
	[InlineData( "kickflip", "n-kickflip" )]
	[InlineData( "treflip", "n-360-flip" )]
	[InlineData( "hardflip360", "n-360-hardflip" )]
	[InlineData( "inwardheelflip360", "n-360-inward-heelflip" )]
	public void NollieFlipsAreAskedForByTheirNollieNames( string trick, string intent )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Trick = trick, TrickNollie = true, TimeSincePop = 0f },
			false, Step );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	[Fact]
	public void AWindupOffTheNoseIsTheNolliesOwn()
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput { Windup = 1f, WindupNose = true }, false, Step );
		Assert.True( facts.Intents.ContainsKey( "nollie-anticipation" ) );
		Assert.False( facts.Intents.ContainsKey( "ollie-anticipation" ) );
	}

	[Theory]
	[InlineData( "kickflip", false, false, "l-f-kickflip" )]
	[InlineData( "heelflip", true, false, "l-b-heelflip" )]
	[InlineData( "shuvit-low", false, true, "l-fs-shuvit" )]
	[InlineData( "shuvit-high", false, false, "l-bs-shuvit" )]
	public void LateFlicksAreTheGraphsLateFlips( string late, bool backFoot, bool frontside, string intent )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput
		{
			Airborne = true, LateTrick = late, LateBackFoot = backFoot, LateFrontside = frontside
		}, false, Step );
		Assert.True( facts.Intents.ContainsKey( "trick" ) );
		Assert.True( facts.Intents.ContainsKey( intent ) );
	}

	[Theory]
	[InlineData( "kickflip", false, "finger-flip" )]
	[InlineData( "shuvit-low", true, "finger-flip-shuv" )]
	[InlineData( "shuvit-low", false, "finger-flip-var" )]
	public void ALateFlickWhileGrabbingIsAFingerFlip( string late, bool frontside, string intent )
	{
		var facts = new StateFacts();
		new RiderStateFacts().Fill( facts, new RiderAnimationInput
		{
			Airborne = true, Grab = "mutegrab", LateTrick = late, LateFrontside = frontside
		}, false, Step );
		Assert.True( facts.Intents.ContainsKey( intent ) );
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
