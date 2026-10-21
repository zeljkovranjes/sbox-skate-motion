namespace SkateMotion.Core.Input;

/// <summary>
/// Picks a grab the way the reaching hands and the trick stick ask for it, as a hand reaches (or the hands change), and
/// keeps it while they hold on: moving the stick afterwards tweaks the grab. With the stick centred as a hand reaches,
/// the grab waits up to <see cref="Settle"/> seconds for it to point somewhere, so a stick and a grab pressed together
/// count as one.
/// <list type="bullet">
/// <item>Stick pulled back: the front hand grabs the nose, the back hand crails, both make a rocket air.</item>
/// <item>Stick pushed forward: the back hand grabs the tail, the front hand reaches back for a seatbelt.</item>
/// <item>Stick toward the toes: the front hand mutes, the back hand stalefishes.</item>
/// <item>Otherwise: the back hand grabs frontside (an indy), the front hand backside (a melon), both hands double.</item>
/// </list>
/// </summary>
internal sealed class GrabSelection
{
	/// <summary>Stick deflection that counts as pointing somewhere.</summary>
	internal const float Pointing = 0.4f;

	// Half the width (radians) of the stick's back and forward sectors; between them it points to a side.
	private const float Sector = 0.785f;

	/// <summary>Longest wait (seconds) after a hand reaches for the stick to pick the grab.</summary>
	internal const float Settle = 0.1f;

	private float _held;
	private bool _picked;

	private bool _front;
	private bool _back;
	private GrabPick _pick;

	/// <summary>The grab held this frame. <paramref name="stickX"/> is positive to the right, <paramref name="stickY"/>
	/// positive forward; the toes are on the right for a regular stance and on the left for goofy.</summary>
	internal GrabPick Step( bool front, bool back, float stickX, float stickY, bool goofy, float delta )
	{
		if ( !front && !back )
		{
			Reset();
			return GrabPick.None;
		}
		if ( front != _front || back != _back )
		{
			_front = front;
			_back = back;
			_held = 0f;
			_picked = false;
		}
		if ( !_picked && (Pointed( stickX, stickY ) || _held >= Settle) )
		{
			_pick = Pick( front, back, stickX, stickY, goofy );
			_picked = true;
		}
		_held += delta;
		// While a changed hold settles, the grab already held carries on (nothing before the first one).
		return _pick;
	}

	internal void Reset()
	{
		_front = false;
		_back = false;
		_pick = GrabPick.None;
		_held = 0f;
		_picked = false;
	}

	private static bool Pointed( float x, float y ) => x * x + y * y > Pointing * Pointing;

	internal static GrabPick Pick( bool front, bool back, float stickX, float stickY, bool goofy )
	{
		var pointing = Pointed( stickX, stickY );
		// Zero points the stick straight back, pi straight forward.
		var angle = MathF.Atan2( stickX, -stickY );
		var backward = pointing && MathF.Abs( angle ) <= Sector;
		var forward = pointing && MathF.Abs( angle ) >= MathF.PI - Sector;
		var toes = pointing && !backward && !forward && stickX * (goofy ? -1f : 1f) > 0f;
		if ( front && back )
			return backward ? GrabPick.Rocket : GrabPick.Double;
		if ( front )
			return backward ? GrabPick.Nose : forward ? GrabPick.Seatbelt : toes ? GrabPick.Mute : GrabPick.Melon;
		return backward ? GrabPick.NoseCrail : forward ? GrabPick.Tail : toes ? GrabPick.Stale : GrabPick.Indy;
	}
}
