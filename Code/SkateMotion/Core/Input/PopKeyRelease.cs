namespace SkateMotion.Core.Input;

/// <summary>
/// A pop key (the ollie key, winding the trick stick back, or the nollie key, forward) and the flick it gives as it is let
/// go: the stick swung the other way for a moment. On the ground that flick is the pop, however long the key was held. In
/// the air a quick tap flicks too (a late shove-it, or a late flip with a flip key held), but a key held longer has been
/// holding a board adjust, and letting it go only lets the board go.
/// </summary>
internal sealed class PopKeyRelease
{
	/// <summary>Seconds a pop key may be held in the air and still flick as it is let go.</summary>
	internal const float AirTap = 0.3f;

	private int _winding;
	private float _heldFor;

	/// <summary>
	/// Steps the key. Returns the way it was winding (-1 back, 1 forward) on the step its release flicks, otherwise 0.
	/// </summary>
	/// <param name="winding">The way the key winds the stick now: -1 back, 1 forward, 0 let go.</param>
	/// <param name="airborne">Whether the board is in the air.</param>
	/// <param name="delta">Seconds since the last step.</param>
	internal int Step( int winding, bool airborne, float delta )
	{
		var released = _winding != 0 && winding == 0 ? _winding : 0;
		var flicks = released != 0 && (!airborne || _heldFor < AirTap);
		_heldFor = winding != 0 && winding == _winding ? _heldFor + delta : 0f;
		_winding = winding;
		return flicks ? released : 0;
	}

	/// <summary>Forgets the key held, so its release flicks nothing (its pop spent on a trick key).</summary>
	internal void Reset()
	{
		_winding = 0;
		_heldFor = 0f;
	}
}
