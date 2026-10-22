using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class LateFlickTests
{
	private const float Frame = 1f / 60f;

	// The graph as far as late tricks go: asked for one while not playing one, it enters the late state and plays its
	// clip for the given seconds, then leaves it. Flicks are made at the given seconds of the air; returns how many times
	// the graph entered the late state.
	private static int Air( float seconds, float clip, params float[] flicks )
	{
		var late = new LateFlick();
		var playing = 0f;
		var entries = 0;
		var next = 0;
		for ( var t = 0f; t < seconds; t += Frame )
		{
			// The animator steps the flick with what the graph last showed, then reads a new flick, then the graph updates.
			late.Step( playing > 0f, 0f, Frame );
			if ( next < flicks.Length && t >= flicks[ next ] )
			{
				late.Flick( "shuvit-low" );
				next++;
			}
			if ( playing > 0f )
				playing = Math.Max( 0f, playing - Frame );
			else if ( late.Trick is not null )
			{
				playing = clip;
				entries++;
			}
		}
		return entries;
	}

	[Theory]
	[InlineData( 1.4f, 0.5f )]
	[InlineData( 3f, 0.5f )]
	[InlineData( 1.4f, 0.25f )]
	[InlineData( 0.8f, 0.5f )]
	public void OneFlickIsOneLateTrick( float air, float clip )
	{
		// Held for a fixed 0.6 s, a flick played a 0.5 s clip twice in a long air.
		Assert.Equal( 1, Air( air, clip, 0.3f ) );
	}

	[Theory]
	// The second flick during the first trick's clip, and after it.
	[InlineData( 0.5f )]
	[InlineData( 1.0f )]
	public void ASecondFlickIsASecondLateTrick( float second )
	{
		Assert.Equal( 2, Air( 2f, 0.5f, 0.3f, second ) );
	}

	[Fact]
	public void AFlickTheGraphNeverStartsLapsesAfterTheWait()
	{
		var late = new LateFlick();
		late.Flick( "kickflip" );
		for ( var t = 0f; t < LateFlick.Wait - 0.05f; t += Frame )
			late.Step( false, 0f, Frame );
		Assert.Equal( "kickflip", late.Trick );
		for ( var i = 0; i < 6; i++ )
			late.Step( false, 0f, Frame );
		Assert.Null( late.Trick );
	}

	[Fact]
	public void TheWaitDoesNotRunWhileAnEarlierLateTrickStillPlays()
	{
		// Flicked again with a second of the first trick's clip still to play, it is still asked for when that ends.
		var late = new LateFlick();
		late.Step( true, 0f, Frame );
		late.Flick( "heelflip" );
		for ( var t = 0f; t < 1f; t += Frame )
			late.Step( true, 0f, Frame );
		late.Step( false, 0f, Frame );
		Assert.Equal( "heelflip", late.Trick );
	}

	[Fact]
	public void TheStickComesBackToTheMiddleBeforeItArmsAnotherFlick()
	{
		var late = new LateFlick();
		Assert.True( late.MayArm );
		late.Flick( "shuvit-low" );
		late.Step( true, 1f, Frame );
		late.Step( true, 0.6f, Frame );
		Assert.False( late.MayArm );
		late.Step( true, 0.2f, Frame );
		Assert.True( late.MayArm );
		// Swung straight through the middle between two steps counts too.
		late.Flick( "shuvit-low" );
		late.Step( true, 1f, Frame );
		Assert.False( late.MayArm );
		late.Step( true, -1f, Frame );
		Assert.True( late.MayArm );
	}

	[Fact]
	public void TwoFlicksAreTwoLateTricksWheneverTheSecondComes()
	{
		// The trick stick pulled back for 6 frames then pushed forward for 6, twice, the second flick at every frame from
		// before the first trick's clip ends to after it; the flick read as the rider's animator reads it. The forward
		// half used to leave the stick armed for a back-foot flick at once, so the next flick's back half fired a trick of
		// its own and two flicks could play three.
		for ( var second = 0.3f; second < 0.95f; second += Frame )
			Assert.Equal( 2, Flicked( 1.6f, 0.5f, 0.1f, second ) );
	}

	// The air with flicks of the stick (back 6 frames, then forward 6) at the given seconds, read as the animator reads
	// them and played by the graph as in Air; returns how many times the graph entered the late state.
	private static int Flicked( float seconds, float clip, params float[] flicks )
	{
		var late = new LateFlick();
		var armed = false;
		var back = false;
		var playing = 0f;
		var entries = 0;
		for ( var t = 0f; t < seconds; t += Frame )
		{
			var y = 0f;
			foreach ( var at in flicks )
			{
				var into = (t - at) / Frame;
				if ( into >= 0f && into < 6f )
					y = -1f;
				else if ( into >= 6f && into < 12f )
					y = 1f;
			}
			late.Step( playing > 0f, y, Frame );
			if ( MathF.Abs( y ) > 0.65f && !armed && late.MayArm )
			{
				armed = true;
				back = y > 0f;
			}
			else if ( armed && (back ? y < -0.35f : y > 0.35f) )
			{
				armed = false;
				late.Flick( "shuvit-low" );
			}
			if ( playing > 0f )
				playing = Math.Max( 0f, playing - Frame );
			else if ( late.Trick is not null )
			{
				playing = clip;
				entries++;
			}
		}
		return entries;
	}

	[Fact]
	public void LandingForgetsTheFlick()
	{
		var late = new LateFlick();
		late.Flick( "kickflip" );
		late.Clear();
		Assert.Null( late.Trick );
	}

	[Theory]
	[InlineData( "OnBoard.InAir.Lateflip.LateBSShuv", true )]
	[InlineData( "OnBoard.InAir.DarkLateflip.LATEFLIP_DARKCATCH", true )]
	[InlineData( "OnBoard.InAir.DarkToLightflip.DarkToLightKickflip", true )]
	[InlineData( "OnBoard.InAir.GrabsTweaks.FSGrab.FingerFlip.Into", true )]
	[InlineData( "OnBoard.InAir.GrabsTweaks.StaleGrab.StaleFingerFlipShuv", true )]
	[InlineData( "OnBoard.InAir.InAirStatic.Air.Default", false )]
	[InlineData( "OnBoard.InAir.GrabsTweaks.FSGrab.Cyc", false )]
	[InlineData( null, false )]
	public void TheLateTricksStatesAreKnownByName( string state, bool shows )
	{
		Assert.Equal( shows, LateFlick.Shows( state ) );
	}
}
