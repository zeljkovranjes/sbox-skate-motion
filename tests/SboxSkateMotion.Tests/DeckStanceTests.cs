using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class DeckStanceTests
{
	private const float Step = 1f / 60f;
	private const float Inch = 0.0254f;

	[Fact]
	public void AFootStepsOnceTheClipsPlaceIsFarEnoughAlongTheDeck()
	{
		Assert.False( DeckStance.OutOfStance( new Vector3( 4.8f * Inch, 0f, 0f ) ) );
		Assert.True( DeckStance.OutOfStance( new Vector3( 5.2f * Inch, 0f, 0f ) ) );
		Assert.True( DeckStance.OutOfStance( new Vector3( -5.2f * Inch, 0f, 0f ) ) );
	}

	[Fact]
	public void AFootStepsSoonerAcrossTheDeckThanAlongIt()
	{
		// A varial kickflip's front foot planted 4.7 in from the riding stance's place, 3.9 of it across the deck: its heel
		// over the edge, it steps back onto the deck, where as far along the deck it would stand.
		Assert.True( DeckStance.OutOfStance( new Vector3( -2.6f * Inch, -3.9f * Inch, 0f ) ) );
		Assert.False( DeckStance.OutOfStance( new Vector3( -4.7f * Inch, 0f, 0f ) ) );
		Assert.False( DeckStance.OutOfStance( new Vector3( 0f, 2.4f * Inch, 0f ) ) );
		Assert.True( DeckStance.OutOfStance( new Vector3( 0f, 2.6f * Inch, 0f ) ) );
	}

	[Fact]
	public void AFootsHeightOverTheDeckNeverMakesItStep()
	{
		// Planted as it came down, a foot 4.4 in over where the clip stands it is in its place on the deck.
		Assert.False( DeckStance.OutOfStance( new Vector3( 1f * Inch, 0.5f * Inch, -4.4f * Inch ) ) );
	}

	[Fact]
	public void AHeldFootComesDownWithTheClipsAtOnce()
	{
		Assert.Equal( 3f * Inch, DeckStance.HeldHeight( 3f * Inch, 7f * Inch, Step ), 6 );
	}

	[Fact]
	public void AHeldFootRisesNoQuickerThanTheLandingAllows()
	{
		var held = DeckStance.HeldHeight( 6f * Inch, 3f * Inch, Step );
		Assert.Equal( 3f * Inch + DeckStance.LandingRise * Step, held, 6 );
		Assert.Equal( 3f * Inch, DeckStance.HeldHeight( 6f * Inch, 3f * Inch, -Step ), 6 );
	}

	[Fact]
	public void AHeldFootStaysDownThroughTheClipsJumpAfterAVarialKickflip()
	{
		// The front foot's height from the clip's pull, a frame apart, after a varial kickflip lands (inches up the shown
		// deck's frame, where an ankle standing on the deck is 4.2 in up): it jumps 3 in in three frames and comes back down.
		// Held by the landing, the foot rises with it less than half as far, no higher than it stands on the deck, and is
		// back with the clip's once that is lower again.
		var pull = new[] { 3.18f, 2.99f, 2.90f, 2.89f, 3.85f, 5.40f, 5.88f, 5.62f, 5.09f, 4.59f, 4.23f, 3.89f, 3.66f, 3.42f };
		var held = pull[ 0 ] * Inch;
		var highest = held;
		foreach ( var height in pull )
		{
			held = DeckStance.HeldHeight( height * Inch, held, Step );
			highest = MathF.Max( highest, held );
		}
		Assert.True( highest < 4.25f * Inch, $"the held foot rose to {highest / Inch:F2} in" );
		Assert.Equal( 3.42f * Inch, held, 5 );
	}
}
