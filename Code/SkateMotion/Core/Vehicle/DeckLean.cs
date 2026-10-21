namespace SkateMotion.Core.Vehicle;

/// <summary>
/// How far the deck rolls into a turn on its trucks (radians; positive dips its left side, the side a positive steer
/// turns to). Steering asks each truck to twist about its pivot, an axis rising steeply from the deck, so the deck rolls
/// over the hangers while the wheels stay on the ground. The twist follows the stick through the steering response
/// curves (gentle near centre, less at speed, while pushing and in a manual), is filtered as the stick and the deck
/// take it up, and eases into the most a truck twists, so the deck dips in and out without snapping. Off the ground the
/// trucks let go of their twist slowly; coming back down they take up the deck's again over a moment.
/// </summary>
internal sealed class DeckLean
{
	// Truck twist (radians) asked for at full steer: the steering's scale times the stick curve's full value.
	private const float FullTwist = 0.6f * 0.834f;

	/// <summary>The most a truck twists on its pivot (14 degrees).</summary>
	internal const float TwistLimit = 0.2443461f;

	// The truck pivot rises 31 degrees from the deck's plane: its twist rolls the deck by the cosine of that.
	private const float RollShare = 0.8571673f;

	/// <summary>The furthest the deck leans.</summary>
	internal const float MaximumRoll = TwistLimit * RollShare;

	// Per sixtieth of a second: share of the new twist the deck takes up, and the share of its twist a truck off the
	// ground keeps.
	private const float DeckShare = 0.2f;
	private const float AirborneKeep = 0.983f;

	// Seconds a truck back on the ground takes to take up the deck's twist again.
	private const float Contact = 0.165f;

	private float _stick;
	private float _push = 1f;
	private float _deck;
	private float _truck;
	private float _landedTruck;
	private float _grounded = float.PositiveInfinity;

	/// <summary>The deck's lean now (radians, positive dips its left side).</summary>
	internal float Roll => SoftLimit( _truck ) * RollShare;

	/// <summary>Advances the lean by <paramref name="step"/> seconds and returns it.</summary>
	/// <param name="steer">Steering input, -1 to 1 (positive turns left).</param>
	/// <param name="speed">Riding speed, m/s.</param>
	/// <param name="pushing">The rider is pushing: steering eases down.</param>
	internal float Update( float steer, float speed, bool pushing, DeckLeanMode mode, float step )
	{
		if ( !float.IsFinite( step ) || step <= 0f )
			return Roll;
		var ticks = step * 60f;
		steer = float.IsFinite( steer ) ? Math.Clamp( steer, -1f, 1f ) : 0f;
		_stick += (steer - _stick) * (1f - MathF.Pow( 1f - SteeringResponse.InputShare, ticks ));
		_push = pushing
			? MathF.Max( SteeringResponse.PushFloor, _push - SteeringResponse.PushDrop * ticks )
			: MathF.Min( 1f, _push + SteeringResponse.PushRecover * ticks );
		var share = mode switch
		{
			DeckLeanMode.Carving => 1f,
			DeckLeanMode.Manual => SteeringResponse.ManualShare,
			_ => 0f
		};
		// The stick curve is applied to the stick's size and then scaled by the stick itself: half steer leans little.
		var twist = FullTwist * HandlingCurves.SteerResponse( _stick ) * MathF.Abs( _stick )
			* HandlingCurves.SteerAtSpeed( speed ) * _push * share;
		_deck += (twist - _deck) * (1f - MathF.Pow( 1f - DeckShare, ticks ));
		if ( mode == DeckLeanMode.Airborne )
		{
			_truck *= MathF.Pow( AirborneKeep, ticks );
			_grounded = 0f;
			_landedTruck = _truck;
		}
		else
		{
			_grounded += step;
			var taken = Math.Clamp( _grounded / Contact, 0f, 1f );
			taken = taken * taken * (3f - 2f * taken);
			_truck = _landedTruck + (_deck - _landedTruck) * taken;
		}
		return Roll;
	}

	internal void Reset()
	{
		_stick = 0f;
		_push = 1f;
		_deck = 0f;
		_truck = 0f;
		_landedTruck = 0f;
		_grounded = float.PositiveInfinity;
	}

	// A truck's twist eases into its limit instead of stopping against it.
	private static float SoftLimit( float twist ) => TwistLimit * MathF.Tanh( twist / TwistLimit );
}
