using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>
/// The trick side of what a state graph is told: which grab family a grab belongs to and how far it is tweaked, the
/// grab specials (christ airs, supermen, coffins, one-foot and no-foot airs), ground grabs and the foot-plant tricks
/// popped from them (bonelesses, fastplants, no complies), and how a flip carries on (held for more turns, flicked
/// again for an underflip, caught dark side up), with the clip windows those graph transitions wait for.
/// </summary>
internal sealed class RiderTrickFacts
{
	/// <summary>Seconds after its pop during which a flip can still be flicked into an underflip or caught dark.</summary>
	internal const float FlipWindow = 0.8f;

	/// <summary>Seconds after a new push stroke during which a flicked pop is a no comply rather than an ollie.</summary>
	internal const float NoComplyWindow = 0.35f;

	/// <summary>Seconds a ground grab is held before the rider has crouched down into it.</summary>
	internal const float GroundGrabCrouch = 0.3f;

	// Seconds after a pop during which the rider still counts as leaving the ground.
	private const float PopMoment = 0.15f;

	// Seconds after a pop before a flip may be redirected: its first frames pop and flick the board.
	private const float FlipWindowStart = 0.1f;

	// Tweak below which a grab can be let go (the graph's own threshold is a fact compared against 0.2).
	private const float TweakFloor = 0.05f;

	/// <summary>Trick stick deflection in the air, without a grab, past which it adjusts the board.</summary>
	internal const float AdjustStart = 0.4f;

	// Half the angle of each of the stick's four directions for a board adjust (as for picking a grab).
	private const float AdjustSector = 0.785f;

	private float _sincePushStart = 10f;
	private bool _pushing;
	private float _groundGrab;

	// A grab held since the air: carried into a landing it lets go there, rather than becoming a ground grab.
	private bool _grabFromAir;
	private bool _catchHeld;
	private bool _superman;
	private bool _underflip;
	private bool _darkCatch;

	// The catch button pressed during this flip (not held over from before the pop): only that asks for a dark catch.
	private bool _catchAsked;

	/// <summary>Adds this frame's trick intents and facts.</summary>
	internal void Fill( StateFacts facts, in RiderAnimationInput input, bool mirrored, float delta )
	{
		var intents = facts.Intents;
		var known = facts.Facts;
		_sincePushStart = input.Pushing && !_pushing ? 0f : _sincePushStart + delta;
		_pushing = input.Pushing;
		var newCatch = input.Catch && !_catchHeld;
		_catchHeld = input.Catch;
		var hand = Hand( input.Grab );
		var onRail = !string.IsNullOrEmpty( input.Grind );
		Grabs( intents, known, input, hand, newCatch );
		GroundGrabs( intents, known, input, hand, onRail, delta );
		// A no comply goes from the push stroke straight into its foot plant: as it pops, the graph is told the push's own
		// gesture, its way from a push's start into the no comply's foot plant (which has no lead-in clip of its own).
		if ( NoComply( input ) && input.TimeSincePop < PopMoment )
		{
			known[ "has-gesture-intent" ] = 1f;
			facts.SetArgument( "has-gesture-intent", "group", "square" );
		}
		Continuations( intents, known, input, newCatch );
		// Brake held in a manual drags its tail (or nose).
		if ( input.Balance != 0 && input.Brake > 0.2f )
			intents[ "manual-brake" ] = input.Brake;
	}

	/// <summary>
	/// The intents for a pop that is not asked for by its own trick name: a foot plant off a ground grab, or an ollie
	/// flicked as a push stroke starts (a no comply). Returns false for every other pop.
	/// </summary>
	internal bool Pop( Dictionary<string, float> intents, in RiderAnimationInput input, bool mirrored )
	{
		if ( input.Trick == "plant" )
		{
			var hand = Hand( input.Grab );
			var side = hand == GrabHand.Front ? "bs" : "fs";
			// Planting the front foot is a boneless and the back foot a fastplant; without a foot named, the front hand's
			// grab goes with a boneless and the back hand's with a fastplant.
			var front = input.PushRight || input.PushLeft ? input.PushRight != mirrored : hand == GrabHand.Front;
			intents[ front ? $"{side}-boneless" : $"{side}-fast-plant" ] = 1f;
			FootPlant( intents, front != mirrored );
			return true;
		}
		if ( NoComply( input ) )
		{
			intents[ "no-comply" ] = 1f;
			FootPlant( intents, mirrored );
			return true;
		}
		return false;
	}

	// An ollie popped as a push stroke starts, still on the ground: a no comply.
	private bool NoComply( in RiderAnimationInput input ) =>
		input.Trick is "ollie" or "nollie" && _sincePushStart < NoComplyWindow && !input.Airborne;

	/// <summary>
	/// A board adjust without a grab: the trick stick held in the air tilts the board toward the rider's toes or heels
	/// (right or left), its nose (stick forward, up) or its tail (stick back, down). Null on the ground, while grabbing
	/// (a grab's family adjusts it instead) and with the stick near the middle.
	/// </summary>
	internal static (string Intent, float Magnitude)? BoardAdjust( in RiderAnimationInput input )
	{
		if ( !input.Airborne || Hand( input.Grab ) != GrabHand.None )
			return null;
		var magnitude = MathF.Min( 1f, MathF.Sqrt( input.AirStickX * input.AirStickX + input.AirStickY * input.AirStickY ) );
		if ( magnitude < AdjustStart )
			return null;
		// Zero with the stick pulled back, a half turn with it pushed forward, negative toward the heels.
		var angle = MathF.Atan2( input.AirStickX, -input.AirStickY );
		var intent = MathF.Abs( angle ) <= AdjustSector ? "board-adjust-down"
			: MathF.Abs( angle ) >= MathF.PI - AdjustSector ? "board-adjust-up"
			: angle < 0f ? "board-adjust-left" : "board-adjust-right";
		return (intent, magnitude);
	}

	/// <summary>True while a late flick would redirect the flip under way (an underflip) instead of starting a late flip.</summary>
	internal static bool Redirects( in RiderAnimationInput input ) =>
		input.TrickFlips && input.TimeSincePop < FlipWindow;

	private static void FootPlant( Dictionary<string, float> intents, bool right )
	{
		intents[ "new-foot-plant" ] = 1f;
		intents[ right ? "right-foot-plant" : "left-foot-plant" ] = 1f;
	}

	// The grab family's board adjust (nose grabs pull it back, tail grabs forward), the tweak, and the specials.
	private void Grabs( Dictionary<string, float> intents, Dictionary<string, float> known, in RiderAnimationInput input,
		GrabHand hand, bool newCatch )
	{
		// The tweak is told all through the air: a grab's release clip waits for it to be small.
		if ( input.Airborne )
			known[ "has-tweak" ] = MathF.Max( TweakFloor, MathF.Sqrt( input.TweakX * input.TweakX + input.TweakY * input.TweakY ) );
		if ( !input.Airborne || hand == GrabHand.None )
		{
			_superman = false;
			if ( BoardAdjust( input ) is { } free )
			{
				intents[ free.Intent ] = 1f;
				intents[ "board-adjust-mag" ] = free.Magnitude;
			}
			return;
		}
		var adjust = input.Grab switch
		{
			"nosegrab" or "nosecrail" or "rocketair" => "board-adjust-down",
			"tailgrab" or "seatbeltgrab" => "board-adjust-up",
			_ => null
		};
		if ( adjust is not null )
		{
			intents[ adjust ] = 1f;
			intents[ "board-adjust-mag" ] = 1f;
		}
		var bothFeet = input.FeetOff == 3;
		// Both hands on and the catch pressed, the rider stretches out flat behind the board for as long as it is held.
		_superman = hand == GrabHand.Both && input.Catch && (_superman || newCatch);
		if ( hand == GrabHand.Both && bothFeet )
			intents[ "coffin" ] = 1f;
		else if ( _superman )
			intents[ "superman" ] = 1f;
		// The feet off a frontside or backside grab: a christ air (the graph's no-foot dismount).
		if ( input.Catch && ChristSide( input.Grab ) is { } christ )
			intents[ christ ] = 1f;
		if ( adjust is not null && (input.Catch || bothFeet) )
			intents[ "no-foot-air-walk" ] = 1f;
		else if ( !bothFeet && input.FeetOff != 0 )
			intents[ (input.FeetOff & 2) != 0 ? "right-one-foot-air" : "left-one-foot-air" ] = 1f;
	}

	// A grab held on the ground (by the hand that reaches), crouching into it, and a coffin lying back on the board.
	private void GroundGrabs( Dictionary<string, float> intents, Dictionary<string, float> known,
		in RiderAnimationInput input, GrabHand hand, bool onRail, float delta )
	{
		// A pop's first moments still count as on the ground: a foot plant pops out of the ground grab it was made from.
		var popping = !string.IsNullOrEmpty( input.Trick ) && input.TimeSincePop < PopMoment;
		var inAir = input.Airborne && !popping;
		// (In the air a grab can also be cleared for a moment before touchdown, its key still held.)
		if ( hand == GrabHand.None && !inAir )
			_grabFromAir = false;
		else if ( inAir )
			_grabFromAir = true;
		var grounded = !inAir && !onRail && hand != GrabHand.None && !_grabFromAir;
		_groundGrab = grounded ? _groundGrab + delta : 0f;
		// A foot plant popped from the grab stays crouched into it until it is back down, so the plant plays through.
		if ( input.Trick == "plant" && hand != GrabHand.None && input.Airborne )
			known[ "is-crouched-enough-for-blend-to-grab-cycle" ] = 1f;
		if ( !grounded )
			return;
		intents[ hand switch
		{
			GrabHand.Front => "ground-bs-grab",
			GrabHand.Back => "ground-fs-grab",
			_ => "ground-dbl-grab"
		} ] = 1f;
		if ( hand == GrabHand.Both && (input.HippyCrouch || input.PushLeft && input.PushRight) )
			intents[ "coffin" ] = 1f;
		known[ "is-crouched-enough-for-blend-to-grab-cycle" ] = _groundGrab >= GroundGrabCrouch ? 1f : 0f;
	}

	// A flip held at the end of its flick turns again; flicked again it underflips back; the catch button, pressed during
	// the flip, catches it dark side up (held over from before the pop it does not, so a button already down, or a reflex
	// click before the trick, is not a dark catch). The windows those graph transitions wait for are clip attributes, stood
	// in for here by time.
	private void Continuations( Dictionary<string, float> intents, Dictionary<string, float> known,
		in RiderAnimationInput input, bool newCatch )
	{
		var flipping = input.Airborne && input.TrickFlips;
		if ( !flipping )
		{
			_underflip = false;
			_darkCatch = false;
			_catchAsked = false;
		}
		else
		{
			var open = input.TimeSincePop >= FlipWindowStart && input.TimeSincePop < FlipWindow;
			known[ "clip-flag:cantranstounderflip" ] = open ? 1f : 0f;
			known[ "clip-flag:underflipwindowend" ] = input.TimeSincePop >= FlipWindow ? 1f : 0f;
			_catchAsked |= newCatch && input.TimeSincePop > 0f;
			if ( open && !string.IsNullOrEmpty( input.LateTrick ) )
				_underflip = true;
			else if ( open && _catchAsked && !_underflip )
				_darkCatch = true;
			if ( input.FlipHeld && input.Trick is "kickflip" or "heelflip" )
				intents[ (input.TrickNollie ? "n-" : "") + input.Trick + "-hold" ] = 1f;
		}
		known[ "is-underflip-requested" ] = _underflip ? 1f : 0f;
		known[ "is-dark-catch-requested" ] = _darkCatch ? 1f : 0f;
		if ( input.Airborne && input.Catch )
		{
			intents[ "dark-catch" ] = 1f;
			if ( newCatch )
				intents[ "new-dark-catch" ] = 1f;
		}
	}

	private static string ChristSide( string grab ) => grab switch
	{
		"indy" or "method" or "stiffy" => "fs-christ",
		"melon" or "japan" => "bs-christ",
		_ => null
	};

	private enum GrabHand { None, Front, Back, Both }

	// The hand a grab is made with: the back hand's grabs, the front hand's, or both hands.
	private static GrabHand Hand( string grab ) => grab switch
	{
		"indy" or "stalegrab" or "method" or "tailgrab" or "stiffy" or "nosecrail" => GrabHand.Back,
		"melon" or "mutegrab" or "japan" or "nosegrab" or "seatbeltgrab" => GrabHand.Front,
		"crailgrab" or "rocketair" => GrabHand.Both,
		_ => GrabHand.None
	};
}
