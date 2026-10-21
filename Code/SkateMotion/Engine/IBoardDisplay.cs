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

	/// <summary>How far (world inches) the shown board is raised out of a rail this frame: the rider on it rises too.</summary>
	Vector3 RailLift { get; }

	/// <summary>
	/// Whether the rider's animation is still in a grind or slide this frame: a nose or tail slide's pitch (SlideTip) is
	/// then held after the board leaves the rail, so the feet the slide's clip holds on the pitched nose or tail stay on it,
	/// and eases out once the animation leaves the slide.
	/// </summary>
	void HoldSlidePitch( bool hold ) { }

	/// <summary>The rails near the board (metres), for keeping the rider out of them.</summary>
	System.Collections.Generic.IReadOnlyList<SkateMotion.Core.Grinding.RailSolid> RailSolids { get; }

	/// <summary>Deck top and underside at a point of the shown deck's frame, or null off the deck.</summary>
	(float Top, float Bottom)? DeckSurface( float x, float y );

	/// <summary>The deck model's box in the shown deck's frame, or null without a deck model.</summary>
	BBox? DeckBounds { get; }

	/// <summary>The whole board (deck, trucks, wheels) in the shown deck's frame, or null without a deck model.</summary>
	BBox? BoardBounds { get; }

	/// <summary>Each truck and wheel as a box in the shown deck's frame.</summary>
	System.Collections.Generic.IReadOnlyList<BBox> PartBounds { get; }

	/// <summary>
	/// The physical pose as shown this frame with the deck's lean into a turn: what a rider standing on the deck is
	/// carried by, so the rider leans with it.
	/// </summary>
	Transform RiddenTransform { get; }

	/// <summary>The board's measured size: the rider's animation is fitted onto it.</summary>
	SkateBoardDimensions Dimensions { get; }
}
