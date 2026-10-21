namespace SkateMotion.Core.Grinding;

/// <summary>
/// The grind as named to the rider and the game: decided as the board locks on, then held. The rail contacts classify the
/// grind afresh every step, and a truck lifting for a moment, the deck rocking on the rail or the nose truck rolling off
/// a rail's end would otherwise rename it (a plain 50-50 turning into a 5-0 and then a smith). A different classification
/// takes over at once when the rider asks for it (leaning onto one truck, letting the lean go, steering on a truck), and
/// otherwise only once it has lasted <see cref="Settle"/> seconds.
/// </summary>
internal sealed class GrindHold
{
	/// <summary>Seconds a classification the rider did not ask for must last before it renames the grind.</summary>
	internal const float Settle = 0.3f;

	private Name _held;
	private Name _candidate;
	private float _candidateTime;

	// What names a grind: its kind and the shape flags the names read.
	private readonly record struct Name( GrindKind Kind, bool Backward, bool Twisted, bool Low, bool OnLedgeTop );

	internal void Reset()
	{
		_held = default;
		_candidate = default;
		_candidateTime = 0f;
	}

	/// <summary>
	/// The grind to report this step from the contacts' <paramref name="kind"/> and <paramref name="shape"/>: the held
	/// kind, and the measured shape with the held name's flags.
	/// </summary>
	internal (GrindKind Kind, GrindShape Shape) Update( GrindKind kind, GrindShape shape, ControlInput input, float step )
	{
		if ( kind == GrindKind.None )
		{
			Reset();
			return (GrindKind.None, default);
		}
		var measured = new Name( kind, shape.Backward, shape.Twisted, shape.Low, shape.OnLedgeTop );
		if ( _held.Kind == GrindKind.None )
		{
			_held = LockOn( measured, input );
			_candidate = _held;
			_candidateTime = 0f;
		}
		else if ( measured == _held )
		{
			_candidate = _held;
			_candidateTime = 0f;
		}
		else
		{
			if ( measured != _candidate )
			{
				_candidate = measured;
				_candidateTime = 0f;
			}
			_candidateTime += step;
			if ( Asked( _held, measured, input ) || _candidateTime >= Settle )
			{
				_held = measured;
				_candidateTime = 0f;
			}
		}
		return (_held.Kind, shape with
		{
			Backward = _held.Backward, Twisted = _held.Twisted, Low = _held.Low, OnLedgeTop = _held.OnLedgeTop
		});
	}

	// Locking on, a grind on one truck that the rider is not leaning or steering onto is a 50-50 that has not settled yet,
	// and a truck grind's twist or drop is only kept when the rider is steering or leaning into it.
	private static Name LockOn( Name measured, ControlInput input )
	{
		if ( measured.Kind is not (GrindKind.FiftyFifty or GrindKind.FiveO or GrindKind.Nosegrind) || Working( input ) )
			return measured;
		return measured with { Kind = GrindKind.FiftyFifty, Twisted = false, Low = false };
	}

	// Whether the rider's input asks for the change from the held name to the measured one.
	private static bool Asked( Name held, Name measured, ControlInput input )
	{
		if ( measured.Backward != held.Backward || measured.OnLedgeTop != held.OnLedgeTop || !Trucks( measured.Kind ) || !Trucks( held.Kind ) )
			return false;
		// Onto one truck by leaning, back onto both by letting go.
		if ( measured.Kind != held.Kind )
			return measured.Kind == GrindKind.FiftyFifty ? !Working( input ) : Leaning( input );
		// Turned or dropped on its truck by working the board, straightened by letting go.
		return Working( input ) == (measured.Twisted || measured.Low);
	}

	private static bool Trucks( GrindKind kind ) => kind is GrindKind.FiftyFifty or GrindKind.FiveO or GrindKind.Nosegrind;

	private static bool Leaning( ControlInput input ) => MathF.Abs( input.FlickY ) > GrindControl.LeanThreshold;

	private static bool Working( ControlInput input ) =>
		Leaning( input ) || MathF.Abs( input.Steer ) > GrindControl.LeanThreshold;
}
