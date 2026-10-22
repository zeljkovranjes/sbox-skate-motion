namespace SkateMotion.Core.Animation;

/// <summary>
/// A late trick flicked in the air, asked of the graph until it has played it once. The graph checks a late trick only as
/// it enters its state, so asked for after the clip had ended it played the trick again: one flick in a long air was two
/// late shove-its. A flick waits up to <see cref="Wait"/> for the graph to start it (not counting time an earlier late
/// trick still plays); started, it lasts until the graph leaves the trick's state. Another flick asks afresh, once the
/// trick stick has come back near the middle (<see cref="MayArm"/>).
/// </summary>
internal sealed class LateFlick
{
	/// <summary>Seconds a flick waits for the graph to start its trick.</summary>
	internal const float Wait = 0.6f;

	/// <summary>How near the middle the trick stick must come back after a flick before it can arm another.</summary>
	internal const float Centre = 0.35f;

	/// <summary>The trick flicked (kickflip, heelflip, shuvit-high or shuvit-low) while it is asked for, or null.</summary>
	internal string Trick { get; private set; }

	private float _waited;
	private bool _started;
	private bool _shownBefore;
	private bool _centred = true;
	private float _stickBefore;

	/// <summary>
	/// Whether the trick stick may arm a new flick: it has come back near the middle, or swung through it between two
	/// steps, since the last one. Held out where a
	/// flick left it, the stick armed again the other way at once, and the next flick's first half fired that stale arm
	/// as a late trick of its own: two flicks played three late tricks.
	/// </summary>
	internal bool MayArm => _centred;

	/// <summary>A new flick, asked for from now.</summary>
	internal void Flick( string trick )
	{
		Trick = trick;
		_waited = 0f;
		_started = false;
		_centred = false;
	}

	/// <summary>
	/// Steps with whether the rider shows a late trick now (as the graph last left it) and how far the trick stick is
	/// pushed forward or pulled back.
	/// </summary>
	internal void Step( bool shown, float stickY, float delta )
	{
		if ( MathF.Abs( stickY ) < Centre || stickY * _stickBefore < 0f )
			_centred = true;
		_stickBefore = stickY;
		var entered = shown && !_shownBefore;
		_shownBefore = shown;
		if ( Trick is null )
			return;
		if ( _started )
		{
			if ( !shown )
				Trick = null;
			return;
		}
		if ( entered )
			_started = true;
		else if ( !shown && (_waited += delta) >= Wait )
			Trick = null;
	}

	/// <summary>Forgets the flick (the board landed).</summary>
	internal void Clear()
	{
		Trick = null;
		_started = false;
		_waited = 0f;
		_centred = true;
	}

	/// <summary>Whether a graph state plays a late trick: a late flip (lit, dark or dark to lit) or a grab's finger flip.</summary>
	internal static bool Shows( string state ) =>
		state is not null && (state.Contains( ".Lateflip." ) || state.Contains( ".DarkLateflip." )
			|| state.Contains( ".DarkToLightflip." ) || state.Contains( "FingerFlip" ));
}
