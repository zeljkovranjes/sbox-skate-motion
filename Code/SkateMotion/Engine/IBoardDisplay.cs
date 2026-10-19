using Sandbox;

namespace SkateMotion.Engine;

/// <summary>Where a board is shown and the shape of its deck, for animation that places feet and hands on it.</summary>
internal interface IBoardDisplay
{
	/// <summary>Shows the whole board at <paramref name="pose"/> blended by <paramref name="weight"/> (0 shows physics).</summary>
	void SetDisplayPose( Transform pose, float weight );

	/// <summary>Where the deck is shown this frame.</summary>
	Transform DisplayedTransform { get; }

	/// <summary>The physical pose as shown this frame (smoothed between physics steps).</summary>
	Transform RenderTransform { get; }

	/// <summary>Deck top and underside at a point of the shown deck's frame, or null off the deck.</summary>
	(float Top, float Bottom)? DeckSurface( float x, float y );

	/// <summary>The deck model's box in the shown deck's frame, or null without a deck model.</summary>
	BBox? DeckBounds { get; }

	/// <summary>The whole board (deck, trucks, wheels) in the shown deck's frame, or null without a deck model.</summary>
	BBox? BoardBounds { get; }

	/// <summary>Each truck and wheel as a box in the shown deck's frame.</summary>
	System.Collections.Generic.IReadOnlyList<BBox> PartBounds { get; }
}
