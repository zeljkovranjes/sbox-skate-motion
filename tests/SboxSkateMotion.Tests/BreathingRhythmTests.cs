using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class BreathingRhythmTests
{
	private const float Frame = 1f / 60f;

	[Fact]
	public void RestsAtFifteenBreathsAMinute()
	{
		var rhythm = new BreathingRhythm();
		var breaths = 0;
		var previous = rhythm.Phase;
		for ( var frame = 0; frame < 60 * 60; frame++ )
		{
			rhythm.Update( Frame, true );
			if ( rhythm.Phase < previous )
				breaths++;
			previous = rhythm.Phase;
		}
		Assert.InRange( breaths, 14, 15 );
		Assert.Equal( BreathingRhythm.RestingRate, rhythm.Rate, 4 );
		Assert.Equal( 1f, rhythm.Depth, 4 );
		Assert.Equal( 1f, rhythm.Weight, 4 );
	}

	[Fact]
	public void PhaseRunsOnSmoothlyWhateverThePace()
	{
		// Winded and settling again, and shown and hidden on the way: the phase only ever moves on by the pace of the moment,
		// so a change of pace never jumps the breath.
		var rhythm = new BreathingRhythm();
		for ( var frame = 0; frame < 60 * 20; frame++ )
		{
			if ( frame % 300 == 0 )
				rhythm.Exert( 1f );
			var before = rhythm.Phase;
			rhythm.Update( Frame, frame % 200 < 150 );
			var step = rhythm.Phase - before;
			if ( step < 0f )
				step += 1f;
			Assert.InRange( step, BreathingRhythm.RestingRate * Frame - 1e-6f, BreathingRhythm.WindedRate * Frame + 1e-6f );
		}
	}

	[Fact]
	public void EasesInAndOutAlongACurveThatStartsAndEndsLevel()
	{
		var rhythm = new BreathingRhythm();
		Assert.Equal( 0f, rhythm.Weight );
		rhythm.Update( Frame, true );
		// One frame in, the curve has barely left zero: no jolt as the breath starts.
		Assert.True( rhythm.Weight < 0.002f );
		for ( var time = Frame; time < BreathingRhythm.EaseIn / 2f - 1e-4f; time += Frame )
			rhythm.Update( Frame, true );
		Assert.Equal( 0.5f, rhythm.Weight, 2 );
		for ( var time = 0f; time < BreathingRhythm.EaseIn; time += Frame )
			rhythm.Update( Frame, true );
		Assert.Equal( 1f, rhythm.Weight );

		// Hidden, it eases out over the shorter time and starts down level too.
		rhythm.Update( Frame, false );
		Assert.True( rhythm.Weight > 0.98f );
		for ( var time = Frame; time < BreathingRhythm.EaseOut + Frame; time += Frame )
			rhythm.Update( Frame, false );
		Assert.Equal( 0f, rhythm.Weight );
		Assert.Equal( 0f, rhythm.Inhale );
	}

	[Fact]
	public void ExertionWindsUpOverAMomentAndSettlesOverSeconds()
	{
		var rhythm = new BreathingRhythm();
		rhythm.Exert( 1f );
		rhythm.Update( Frame, true );
		// Taken up over a moment rather than at once.
		Assert.InRange( rhythm.Exertion, 0.01f, 0.1f );
		for ( var time = 0f; time < 1f; time += Frame )
			rhythm.Update( Frame, true );
		Assert.True( rhythm.Exertion > 0.6f );
		Assert.True( rhythm.Rate > 0.4f && rhythm.Depth > 1.5f );
		// A few seconds on it is settling, and after ten it is back to resting.
		for ( var time = 0f; time < 4f; time += Frame )
			rhythm.Update( Frame, true );
		Assert.InRange( rhythm.Exertion, 0.15f, 0.45f );
		for ( var time = 0f; time < 10f; time += Frame )
			rhythm.Update( Frame, true );
		Assert.True( rhythm.Exertion < 0.03f );
		Assert.True( rhythm.Rate < BreathingRhythm.RestingRate + 0.01f );
	}

	[Fact]
	public void AWeakerExertionNeverLowersAStrongerOne()
	{
		var rhythm = new BreathingRhythm();
		rhythm.Exert( 0.8f );
		rhythm.Exert( 0.2f );
		rhythm.Exert( float.NaN );
		for ( var time = 0f; time < 2f; time += Frame )
			rhythm.Update( Frame, true );
		var strong = rhythm.Exertion;
		var reference = new BreathingRhythm();
		reference.Exert( 0.8f );
		for ( var time = 0f; time < 2f; time += Frame )
			reference.Update( Frame, true );
		Assert.Equal( reference.Exertion, strong, 5 );

		// Past one it counts as one.
		var over = new BreathingRhythm();
		over.Exert( 5f );
		var full = new BreathingRhythm();
		full.Exert( 1f );
		for ( var time = 0f; time < 2f; time += Frame )
		{
			over.Update( Frame, true );
			full.Update( Frame, true );
		}
		Assert.Equal( full.Exertion, over.Exertion, 5 );
	}

	[Fact]
	public void BreathTurnsOverLevelAtTheTopAndTheBottom()
	{
		Assert.Equal( 0f, BreathingRhythm.Fill( 0f ), 5 );
		Assert.Equal( 1f, BreathingRhythm.Fill( 0.4f ), 5 );
		Assert.Equal( 0f, BreathingRhythm.Fill( 1f ), 5 );
		// Breathing in is quicker than breathing out, and both are smooth: rising all the way in, falling all the way out,
		// and barely moving either side of the turns.
		for ( var step = 0; step < 40; step++ )
			Assert.True( BreathingRhythm.Fill( (step + 1) * 0.01f ) >= BreathingRhythm.Fill( step * 0.01f ) );
		for ( var step = 40; step < 99; step++ )
			Assert.True( BreathingRhythm.Fill( (step + 1) * 0.01f ) <= BreathingRhythm.Fill( step * 0.01f ) + 1e-6f );
		Assert.True( BreathingRhythm.Fill( 0.005f ) < 0.001f );
		Assert.True( BreathingRhythm.Fill( 0.995f ) < 0.001f );
		Assert.True( 1f - BreathingRhythm.Fill( 0.395f ) < 0.001f );
		Assert.True( 1f - BreathingRhythm.Fill( 0.405f ) < 0.001f );
		Assert.Equal( BreathingRhythm.Fill( 0.3f ), BreathingRhythm.Fill( 2.3f ), 5 );
	}

	[Fact]
	public void TheChestNeverJumpsBetweenFrames()
	{
		// Shown and hidden, winded hard again and again: the breath moves on smoothly every frame.
		var rhythm = new BreathingRhythm();
		var previous = rhythm.Inhale;
		var largest = 0f;
		for ( var frame = 0; frame < 60 * 30; frame++ )
		{
			if ( frame % 97 == 0 )
				rhythm.Exert( 1f );
			rhythm.Update( Frame, frame % 173 < 120 );
			largest = MathF.Max( largest, MathF.Abs( rhythm.Inhale - previous ) );
			previous = rhythm.Inhale;
		}
		// At its quickest (easing out at the top of a winded breath) it changes by about 0.13 of a resting breath in a
		// frame: a steady slide, where a snap would be a whole breath at once.
		Assert.True( largest < 0.15f, $"largest step {largest}" );
		Assert.InRange( rhythm.Inhale, 0f, BreathingRhythm.WindedDepth );
	}

	[Fact]
	public void BadTimeStepsChangeNothing()
	{
		var rhythm = new BreathingRhythm();
		rhythm.Exert( 1f );
		for ( var time = 0f; time < 1f; time += Frame )
			rhythm.Update( Frame, true );
		var (phase, weight, exertion) = (rhythm.Phase, rhythm.Weight, rhythm.Exertion);
		foreach ( var delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity } )
			rhythm.Update( delta, false );
		Assert.Equal( phase, rhythm.Phase );
		Assert.Equal( weight, rhythm.Weight );
		Assert.Equal( exertion, rhythm.Exertion );
	}
}
