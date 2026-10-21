using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PlacementSmootherTests
{
	private const float Step = 1f / 90f;

	[Fact]
	public void GettingOnCarriesTheBodyOnAndSettlesOntoTheDeck()
	{
		// On foot the body walks in at 20 in/s; getting on, its placement jumps up onto the deck and forward.
		var smoother = new PlacementSmoother();
		var shown = Vector3.Zero;
		var last = 0f;
		for ( var frame = 0; frame < 30; frame++ )
		{
			var before = shown;
			shown = smoother.Update( new Vector3( 20f * Step * frame, 0f, 0f ), false, true, false, Vector3.UnitZ, Step );
			last = Vector3.Distance( shown, before );
		}
		var deck = new Vector3( 20f * Step * 30f + 3f, 0f, 3.7f );
		var largest = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			var before = shown;
			shown = smoother.Update( deck, true, false, false, Vector3.UnitZ, Step );
			largest = MathF.Max( largest, Vector3.Distance( shown, before ) );
			if ( frame == 0 )
				Assert.True( Vector3.Distance( shown, before ) < 2f * last, "the body jumps on getting on" );
		}
		// The 4.7 in from where the body was shown to the deck is crossed smoothly, never a tenth of it in one frame.
		Assert.True( largest < 0.47f, $"largest step {largest:0.00} in" );
		Assert.True( Vector3.Distance( shown, deck ) < 0.05f, "the body never settles onto the deck" );
	}

	[Fact]
	public void GettingOnKeepsTheHeightUntilRidingBegins()
	{
		// Walked onto the board to get on, the body is bumped against the deck's edge and lifted to the deck's height;
		// the clip stands on the ground the board stands on, so the body stays there until riding places it on the deck.
		var smoother = new PlacementSmoother();
		for ( var frame = 0; frame < 10; frame++ )
			smoother.Update( Vector3.Zero, false, true, false, Vector3.UnitZ, Step );
		foreach ( var height in new[] { 0.57f, 2.11f, 1.33f, 0.47f, 0f, 0f, 1.5f, 3.5f, 4.3f, 4.3f, 4.3f } )
			Assert.Equal( 0f, smoother.Update( new Vector3( 0f, 0f, height ), false, true, true, Vector3.UnitZ, Step ).Z, 2 );
		var shown = 0f;
		var largest = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			var z = smoother.Update( new Vector3( 0f, 0f, 3.7f ), true, false, false, Vector3.UnitZ, Step ).Z;
			largest = MathF.Max( largest, MathF.Abs( z - shown ) );
			shown = z;
		}
		Assert.True( largest < 0.4f, $"largest step {largest:0.00} in" );
		Assert.Equal( 3.7f, shown, 1 );
	}

	[Theory]
	[InlineData( 4f, 1 )]
	[InlineData( -4f, 1 )]
	[InlineData( 8f, 2 )]
	public void StepTakenAtOnceOnFootIsEased( float height, int frames )
	{
		// A kerb taken in one frame, or a stair shown over two (a fixed-step body drawn between its steps).
		var smoother = new PlacementSmoother();
		var shown = Vector3.Zero;
		for ( var frame = 0; frame < 10; frame++ )
			shown = smoother.Update( Vector3.Zero, false, true, false, Vector3.UnitZ, Step );
		var largest = 0f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			var before = shown;
			var target = new Vector3( 0f, 0f, height * Math.Min( frame + 1, frames ) / frames );
			shown = smoother.Update( target, false, true, false, Vector3.UnitZ, Step );
			largest = MathF.Max( largest, Vector3.Distance( shown, before ) );
			// Settling never overshoots the new height.
			Assert.True( MathF.Abs( shown.Z ) <= MathF.Abs( height ) + 0.001f );
		}
		Assert.True( largest < 0.1f * MathF.Abs( height ) + 0.5f, $"largest step {largest:0.00} in" );
		Assert.Equal( height, shown.Z, 1 );
	}

	[Fact]
	public void SlopeIsFollowed()
	{
		// Walking up a slope at 0.6 in a frame the body keeps to it, after at most easing its first step.
		var smoother = new PlacementSmoother();
		var shown = Vector3.Zero;
		for ( var frame = 0; frame < 60; frame++ )
			shown = smoother.Update( new Vector3( frame, 0f, 0.6f * frame ), false, true, false, Vector3.UnitZ, Step );
		Assert.True( MathF.Abs( shown.Z - 0.6f * 59 ) < 0.01f, $"lags the slope by {0.6f * 59 - shown.Z:0.00} in" );
	}

	[Fact]
	public void StoppingIsNotEased()
	{
		// Sliding down faster and faster, up to 1.7 in a frame, and stopping dead: the body stops with it rather than
		// sinking on past.
		var smoother = new PlacementSmoother();
		var z = 60f;
		for ( var frame = 0; frame < 60; frame++ )
		{
			z = MathF.Max( 0f, z - MathF.Min( 1.7f, 0.05f * frame ) );
			Assert.Equal( z, smoother.Update( new Vector3( 0f, 0f, z ), false, true, false, Vector3.UnitZ, Step ).Z, 3 );
		}
		Assert.Equal( 0f, z );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void MovesInTheAirOrRidingAreNotEased( bool riding )
	{
		// A drop in the air on foot, or the deck dropping off a kerb while riding, shows as it is.
		var smoother = new PlacementSmoother();
		for ( var frame = 0; frame < 10; frame++ )
			smoother.Update( new Vector3( 0f, 0f, 5f ), riding, false, false, Vector3.UnitZ, Step );
		Assert.Equal( 0f, smoother.Update( Vector3.Zero, riding, false, false, Vector3.UnitZ, Step ).Z, 4 );
	}

	[Fact]
	public void PlacedElsewhereIsShownThereAtOnce()
	{
		var smoother = new PlacementSmoother();
		for ( var frame = 0; frame < 10; frame++ )
			smoother.Update( Vector3.Zero, false, true, false, Vector3.UnitZ, Step );
		var far = new Vector3( 500f, 0f, 30f );
		Assert.Equal( far, smoother.Update( far, true, true, false, Vector3.UnitZ, Step ) );
		Assert.Equal( Vector3.Zero, smoother.Offset );
	}
}
