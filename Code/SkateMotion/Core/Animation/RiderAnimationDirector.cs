using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>
/// Turns gameplay into the graph's selection parameters: family, action, phase and the preferred categories
/// (style, side, height, lean, speed, strength, direction, detail, kind). A requested motion overrides
/// gameplay until it expires. Speed and lean bands use hysteresis so held controls do not flicker clips.
/// </summary>
internal sealed class RiderAnimationDirector
{
	private static readonly string[] Categories = { "style", "side", "height", "lean", "speed", "strength", "direction", "detail", "kind" };
	private readonly Dictionary<string, string> _request = new( StringComparer.OrdinalIgnoreCase );
	private readonly bool _clipsGoofy;
	private float _requestTime;
	private bool _fast;
	private int _lean;
	private float _blendLean;

	// How fast each spring-followed blend is moving (see Follow).
	private float _blendLeanRate;
	private float _blendTwistRate;
	private float _carvingRate;
	private float _crouchRate;
	private float _tuckRate;
	private float _blendTwist;
	private float _carving;
	private float _crouch;
	private float _tuck;
	private float _wall;
	private float _balance;
	private float _impactBlend;
	private float _impactRate;
	private bool _impactShown;

	// Stiffness (per second) of the spring the impact blend follows a landing's impact on the ground and the board adjust's
	// in the air with: critically damped, so it is nine tenths of the way there in under 0.3 s and never overshoots.
	private const float ImpactEase = 14f;

	/// <param name="clipsGoofy">Whether the clips were authored with the right foot forward.</param>
	internal RiderAnimationDirector( bool clipsGoofy ) => _clipsGoofy = clipsGoofy;

	/// <summary>Plays the motion described by <paramref name="tags"/> (family and action at least) for <paramref name="seconds"/>.</summary>
	internal void Request( IReadOnlyDictionary<string, string> tags, float seconds )
	{
		if ( !tags.ContainsKey( "family" ) || !tags.ContainsKey( "action" ) )
			throw new ArgumentException( "A requested motion needs a family and an action." );
		_request.Clear();
		foreach ( var (key, value) in tags )
			_request[ key ] = value;
		_requestTime = Math.Max( seconds, 0f );
	}

	internal void CancelRequest() => _requestTime = 0f;

	/// <summary>True while a requested motion is playing.</summary>
	internal bool Requesting => _requestTime > 0f;

	internal void Update( in RiderAnimationInput input, float delta, AnimationParameters parameters )
	{
		foreach ( var category in Categories )
			parameters.Set( category, "" );
		parameters.Set( "blend", "" );
		parameters.Set( "urgent", input.Airborne || input.Windup > 0.15f || input.TimeSinceLanding < 0.35f );
		// Mirroring swaps left and right, so the lean blend reads steering from the mirrored side.
		var mirror = input.Goofy != _clipsGoofy;
		var steer = Math.Clamp( mirror ? -input.Steer : input.Steer, -1f, 1f );
		parameters.Set( "steer", steer );
		parameters.Set( "mirror", mirror );
		UpdateBands( input, steer );
		Blends( input, steer, delta, parameters );
		if ( _requestTime > 0f )
		{
			_requestTime -= delta;
			parameters.Set( "phase", "once" );
			foreach ( var (key, value) in _request )
				parameters.Set( key, value );
			return;
		}
		parameters.Set( "style", input.Style ?? "" );
		switch ( input.Activity )
		{
			case RiderActivity.Walking:
				OnFoot( "onfoot", input, parameters );
				break;
			case RiderActivity.Carrying:
				OnFoot( "carry", input, parameters );
				break;
			case RiderActivity.Mounting:
				Mount( input, parameters );
				break;
			case RiderActivity.Dismounting:
				Dismount( input, parameters );
				break;
			case RiderActivity.PickingUp:
				Set( parameters, "offboard", "retrieve", "into" );
				parameters.Set( "height", "high" );
				break;
			case RiderActivity.Dropping:
			case RiderActivity.Throwing:
				Set( parameters, "offboard", "throw", "once" );
				break;
			case RiderActivity.Bailed:
				Set( parameters, "wipeout", "flail", "once" );
				break;
			default:
				if ( input.Airborne ) Air( input, parameters );
				else Ground( input, parameters );
				break;
		}
	}

	// Hysteresis keeps a held push or a steady carve on one clip instead of flickering at the boundary.
	private void UpdateBands( in RiderAnimationInput input, float steer )
	{
		var speed = MathF.Abs( input.Speed );
		if ( speed > 3.3f ) _fast = true;
		else if ( speed < 2.7f ) _fast = false;
		if ( steer < -0.4f ) _lean = -1;
		else if ( steer > 0.4f ) _lean = 1;
		else if ( MathF.Abs( steer ) < 0.2f ) _lean = 0;
	}

	private string Lean => _lean < 0 ? "left" : _lean > 0 ? "right" : "neutral";

	// Standing and crouched distance from the deck to the rider's centre of mass, metres, as blends store them.
	private const float StandingHeight = 0.9649f;
	private const float CrouchedHeight = 0.5726f;

	// Numeric parameters that blends read, in the units their clips store: lean -1 to 1, crouch as the centre of
	// mass height in metres, speed in metres per second, wall tilt +1 backside and -1 frontside. Push weights are
	// 0 to 1. Each eases toward its target so held input never snaps a pose; the ones the stick and the crouch move
	// follow as springs (see Follow).
	// Rate (per second) of the springs the stick's and the crouch's blends follow, critically damped: about as quick as
	// the ease they replaced, which moved at its fastest the frame the stick did, so the body's lean jerked as a turn
	// began or swapped sides (the fingers and eyes 1.3 to 2.1 as a steer went over from one side to the other).
	private const float BlendSpring = 12f;

	// Moves a blend toward its target as a critically damped spring, exactly over the step: its speed starts and ends at
	// nothing and never steps, however the target jumps.
	private static void Follow( ref float value, ref float rate, float target, float delta )
	{
		if ( !(delta > 0f) )
			return;
		var offset = value - target;
		var push = rate + BlendSpring * offset;
		var fade = MathF.Exp( -BlendSpring * delta );
		value = target + (offset + push * delta) * fade;
		rate = (rate - push * BlendSpring * delta) * fade;
	}

	private void Blends( in RiderAnimationInput input, float steer, float delta, AnimationParameters parameters )
	{
		var speed = MathF.Abs( input.Speed );
		var ease = 1f - MathF.Exp( -8f * delta );
		Follow( ref _blendLean, ref _blendLeanRate, steer, delta );
		Follow( ref _carving, ref _carvingRate, MathF.Abs( steer ) > 0.15f && speed > 1f ? 1f : 0f, delta );
		// The handling's crouch (pumping, low on steep ground) shows too.
		var crouch = input.Pumping || input.Windup > 0.15f ? 1f : Math.Clamp( input.Crouch, 0f, 1f );
		Follow( ref _crouch, ref _crouchRate, crouch, delta );
		Follow( ref _tuck, ref _tuckRate, Math.Clamp( (speed - 6f) / 3f, 0f, 1f ), delta );
		parameters.Set( "blend-lean", _blendLean );
		// Steering leans a grind's or slide's pose over the rail: eased like the lean, so starting or letting go of a turn
		// moves the rider's body and feet across instead of jumping them to the other pose in one frame.
		Follow( ref _blendTwist, ref _blendTwistRate, steer, delta );
		parameters.Set( "blend-twist", _blendTwist );
		parameters.Set( "blend-carving", _carving );
		// Slow, tight carves are quick; fast ones are long.
		parameters.Set( "blend-carve-speed", 1f - Math.Clamp( (speed - 2f) / 6f, 0f, 1f ) );
		parameters.Set( "blend-carve-direction", 0f );
		parameters.Set( "blend-tuck", _tuck );
		parameters.Set( "blend-speed", speed );
		parameters.Set( "blend-crouch", StandingHeight - _crouch * (StandingHeight - CrouchedHeight) );
		// Roll crossing the wall threshold must not replace the whole body pose in one frame.
		// Keep the existing activation range, but blend continuously within it and ease its release.
		var wall = MathF.Sign( input.DeckRoll ) * Math.Clamp( (MathF.Abs( input.DeckRoll ) - 0.7f) / 0.3f, 0f, 1f );
		_wall += (wall - _wall) * ease;
		parameters.Set( "blend-wall", _wall );
		// A grab's tweak: the trick stick held while grabbing (x toward the toes, y forward) blends its tweaked poses.
		parameters.Set( "tweak_x", input.TweakX );
		parameters.Set( "tweak_y", input.TweakY );
		// A hippy jump's clips are picked by how far its own crouch wound it up, as its flight's height is: a quick crouch
		// hops up upright, a full one leaps into the hurdle's lean. Read from the last trick's pop instead, a high ollie
		// earlier leant every hippy jump after it 60 degrees along the board.
		parameters.Set( "blend-pop-height", input.HippyCrouch || input.OffBoard ? Math.Clamp( input.HippyCharge, 0f, 1f )
			: input.HighPop ? 1f : 0f );
		parameters.Set( "blend-windup", Math.Clamp( input.Windup, 0f, 1f ) );
		// In the air this weight is the board adjust's magnitude: how far the trick stick is held over, which blends in a
		// board adjust and tweaks the nose and tail grabs. On the ground it is the last landing's impact, which eases in: taken
		// in a frame it gave the riding pose's knees and arms at once (a board thrown off its wheels at a bank's foot and
		// meeting its face turned the head 10 degrees and an arm 24 in a frame). It follows the same way in the air and across
		// the take-off and the landing: taken from the stick in a frame, letting the stick go in the air straightened a leg
		// 37 degrees in a frame. It follows a spring whose speed starts from rest: eased toward it at a rate, the weight took
		// its fastest step on its first frame, which threw the arms 7 degrees a frame faster as a board adjust began. Only the
		// first weight given is taken at once.
		var impact = input.Airborne
			? Math.Min( 1f, MathF.Sqrt( input.AirStickX * input.AirStickX + input.AirStickY * input.AirStickY ) )
			: Math.Clamp( input.LandingSpeed / 6f, 0f, 1f );
		if ( _impactShown && delta > 0f )
		{
			var off = _impactBlend - impact;
			var pull = _impactRate + ImpactEase * off;
			var decay = MathF.Exp( -ImpactEase * delta );
			_impactBlend = impact + (off + pull * delta) * decay;
			_impactRate = (_impactRate - ImpactEase * pull * delta) * decay;
		}
		else if ( !_impactShown )
		{
			_impactBlend = impact;
			_impactRate = 0f;
		}
		_impactShown = true;
		parameters.Set( "blend-impact", _impactBlend );
		LandingBlend( input, parameters );
		// The deck's pitch eases into the balance blend like the other blends: riding onto a bank pitched the deck over a few
		// frames and swung the upper body as fast.
		_balance += (0.9f * Math.Clamp( input.DeckPitch / 0.4f, -1f, 1f ) - _balance) * ease;
		parameters.Set( "blend-balance", _balance );
		parameters.Set( "rider-style", input.Style ?? "DEFAULT" );
		// Push weights: speed picks the quick or slow push, and speed plus the push's gain picks the strong or light one.
		parameters.Set( "blend-push-speed", Math.Clamp( speed / 8.5f, 0f, 1f ) );
		parameters.Set( "blend-push-effort", Math.Clamp( (speed + PushGain - 5f) / 3f, 0f, 1f ) );
	}

	// The landing blend picks its clips by how hard the board came down (metres per second; light below 2.68,
	// heavy from 6) and one of three variants, chosen afresh at each touchdown. The fall speed is kept through the
	// air too, so a landing that first touches with one wheel still reads as the big drop it was.
	private void LandingBlend( in RiderAnimationInput input, AnimationParameters parameters )
	{
		if ( input.Airborne )
		{
			_fall = MathF.Max( _fall, -input.VerticalSpeed );
			_wasAirborne = true;
		}
		else if ( _wasAirborne )
		{
			_impact = MathF.Max( input.LandingSpeed, _fall );
			_variant = (_variant + 1) % 3;
			_fall = 0f;
			_wasAirborne = false;
		}
		parameters.Set( "avgvely", _impact );
		parameters.Set( "blend-spin", 0f );
		parameters.Set( "random", _variant switch { 0 => "0", 1 => "1", _ => "2" } );
	}

	private float _fall;
	private float _impact;
	private int _variant;
	private bool _wasAirborne;

	// Speed one push adds, metres per second.
	private const float PushGain = 1.5f;

	private void Air( in RiderAnimationInput input, AnimationParameters parameters )
	{
		if ( input.Handplant is not null )
		{
			Set( parameters, "handplant", "handplant", input.Handplant );
			parameters.Set( "side", input.GrindFrontside ? "frontside" : "backside" );
			return;
		}
		if ( input.Plant is not null )
		{
			Set( parameters, "plant", "default", input.TimeSincePop < 0.2f ? "pop" : input.VerticalSpeed < 0f ? "fall" : "air" );
			parameters.Set( "style", input.Plant );
			parameters.Set( "speed", _fast ? "fast" : "slow" );
			return;
		}
		if ( input.HippyJump )
		{
			Set( parameters, "trick", "hippyjump", "cycle" );
			parameters.Set( "style", "catch" );
			return;
		}
		if ( input.LateTrick is not null )
		{
			Set( parameters, "trick", "late-" + input.LateTrick, "once" );
			parameters.Set( "style", "catch" );
			parameters.Set( "height", input.HighPop ? "high" : "low" );
			return;
		}
		if ( !string.IsNullOrEmpty( input.Grab ) && input.TimeSincePop > 0.25f )
		{
			if ( input.TrickFlips && input.TimeSincePop < 0.6f )
			{
				Set( parameters, "grabflip", "flip-" + Short( input.Grab ) + "-grab", "into" );
				return;
			}
			if ( input.OneFoot )
			{
				Set( parameters, "air", "air-grab", "cycle" );
				parameters.Set( "style", "onefoot" );
				return;
			}
			Set( parameters, "grab", input.Grab, "cycle" );
			return;
		}
		if ( !string.IsNullOrEmpty( input.Trick ) )
		{
			// Long hang time holds the trick's catch; the pop and air clips cover the start.
			var hang = input.TrickFlips && input.TimeSincePop > 0.65f;
			Set( parameters, "trick", hang ? CatchName( input.Trick ) : input.Trick, input.TimeSincePop < 0.2f ? "pop" : hang ? "cycle" : "air" );
			parameters.Set( "style", hang ? (input.TrickNollie ? "catch-nollie" : "catch") : input.TrickNollie ? "nollie" : input.Style ?? "" );
			parameters.Set( "height", input.HighPop ? "high" : "low" );
			return;
		}
		if ( input.DeckUp < -0.3f )
		{
			Set( parameters, "air", "darkslide-idle", "cycle" );
			return;
		}
		if ( input.OneFoot )
		{
			Set( parameters, "air", "air-ollie", "cycle" );
			parameters.Set( "style", "onefoot" );
			parameters.Set( "height", input.HighPop ? "high" : "low" );
			return;
		}
		if ( MathF.Abs( input.Steer ) > 0.8f && input.TimeSincePop < 0.5f )
		{
			Set( parameters, "air", "bodyspin-ollie", "once" );
			parameters.Set( "side", input.Steer > 0f ? "frontside" : "backside" );
			return;
		}
		if ( MathF.Abs( input.Adjust ) > 0.2f )
		{
			Set( parameters, "air", input.Adjust > 0f ? "adjust-nose" : "adjust-tail", "cycle" );
			return;
		}
		Set( parameters, "air", input.Pumping ? "extend" : "idle", "cycle" );
		parameters.Set( "style", "inair" );
	}

	private void Ground( in RiderAnimationInput input, AnimationParameters parameters )
	{
		if ( !string.IsNullOrEmpty( input.Grind ) )
		{
			var dark = input.DeckUp < -0.3f;
			Set( parameters, "grind", dark ? "darkslide" : input.Grind, "cycle" );
			parameters.Set( "side", input.GrindFrontside ? "frontside" : "backside" );
			parameters.Set( "height", "low" );
			parameters.Set( "lean", Lean );
			return;
		}
		if ( input.TimeSinceLanding < 0.35f && input.LandingSpeed > 0.5f )
		{
			Landing( input, parameters );
			return;
		}
		if ( input.Skitching )
		{
			Set( parameters, "skitch", "default", "cycle" );
			parameters.Set( "height", "high" );
			return;
		}
		if ( MathF.Abs( input.DeckRoll ) > 0.7f )
		{
			// The deck rolled onto its side: riding a wall.
			Set( parameters, "wallride", "ride", "cycle" );
			parameters.Set( "side", input.DeckRoll > 0f ? "frontside" : "backside" );
			return;
		}
		if ( input.Balance != 0 )
		{
			Set( parameters, "manual", input.Balance > 1 ? "noseidle" : "idle", "cycle" );
			parameters.Set( "lean", Lean );
			return;
		}
		parameters.Set( "lean", Lean );
		if ( input.Windup > 0.15f )
		{
			if ( input.HippyJump )
			{
				Set( parameters, "trick", "hippyjump", "windup" );
				parameters.Set( "style", "catch" );
				return;
			}
			Set( parameters, "riding", input.TrickNollie ? "nollie" : "ollie", "cycle" );
			parameters.Set( "detail", "windup" );
			return;
		}
		if ( input.Pushing )
		{
			Set( parameters, "riding", "push", "cycle" );
			parameters.Set( "speed", _fast ? "fast" : "slow" );
			parameters.Set( "strength", "strong" );
			return;
		}
		if ( input.Sliding )
		{
			Set( parameters, "riding", "slide", "cycle" );
			parameters.Set( "side", _lean < 0 ? "backside" : "frontside" );
			parameters.Set( "speed", _fast ? "fast" : "slow" );
			return;
		}
		if ( input.Brake > 0.2f )
		{
			if ( MathF.Abs( input.Speed ) < 0.3f )
			{
				// Stopped with the brake held: a foot comes off onto the ground.
				Set( parameters, "offboard", "footplant", "cycle" );
				parameters.Set( "side", "frontside-right" );
				return;
			}
			Set( parameters, "riding", "brake", "cycle" );
			return;
		}
		if ( input.Speed < -0.5f )
		{
			// Rolling backwards: the fakie channel replaces the forward riding stance.
			Set( parameters, "channel", "channel", "cycle" );
			return;
		}
		// Rolling: the riding blend covers standing, carving, tucking and wall rides from the blend parameters.
		parameters.Set( "lean", "" );
		Set( parameters, "riding", "idle", "once" );
	}

	private static void Landing( in RiderAnimationInput input, AnimationParameters parameters )
	{
		if ( input.Plant is not null )
		{
			Set( parameters, "plant", "default", "land" );
			parameters.Set( "style", input.Plant );
			return;
		}
		if ( input.TrickFlips && input.CatchStyle is not null )
		{
			Set( parameters, "trick", input.Trick + "-" + input.CatchStyle, "out" );
			parameters.Set( "style", input.TrickNollie ? "catch-nollie" : "catch" );
			parameters.Set( "height", input.HighPop ? "high" : "low" );
			return;
		}
		Set( parameters, "landing", "land", "once" );
		parameters.Set( "height", input.LandingSpeed > 4f ? "high" : "low" );
		parameters.Set( "strength", input.LandingSpeed > 4f ? "aggressive" : "loose" );
	}

	private static void OnFoot( string family, in RiderAnimationInput input, AnimationParameters parameters )
	{
		if ( input.ObjectMotion is not null )
		{
			Set( parameters, "object", input.ObjectMotion, "cycle" );
			parameters.Set( "height", input.ObjectHeight ?? "medium" );
			parameters.Set( "speed", input.MoveSpeed > 1.5f ? "fast" : input.MoveSpeed > 0.7f ? "medium" : "slow" );
			return;
		}
		var moving = input.MoveSpeed >= 0.2f;
		var action = !moving ? "stand" : input.MoveSpeed < 2.2f ? "walk" : input.MoveSpeed < 4.5f ? "run" : "sprint";
		if ( input.Airborne )
		{
			// Jumping on foot: big or small by fall speed, forward when moving.
			Set( parameters, family, moving ? "run" : "stand", "to" );
			parameters.Set( "style", "jump" );
			parameters.Set( "height", input.VerticalSpeed < -4f ? "big" : "small" );
			parameters.Set( "direction", input.VerticalSpeed > 1f ? "up" : moving ? "forward" : "down" );
			return;
		}
		if ( input.TimeSinceLanding < 0.4f && input.LandingSpeed > 2f )
		{
			Set( parameters, family, moving ? "land-run" : "land-stand", "into" );
			parameters.Set( "height", input.LandingSpeed > 5f ? "big" : "small" );
			return;
		}
		if ( action == "stand" && input.Stationary > 6f && family == "onfoot" )
		{
			Set( parameters, family, "stand-readypose", "once" );
			return;
		}
		Set( parameters, family, action, "cycle" );
		parameters.Set( "direction", moving ? "forward" : "" );
		parameters.Set( "lean", moving ? "" : "neutral" );
	}

	private void Mount( in RiderAnimationInput input, AnimationParameters parameters )
	{
		if ( input.DeckPitch > 0.26f )
		{
			// Standing on a ramp edge with the nose down: drop in.
			Set( parameters, "dropin", "step", "into" );
			return;
		}
		if ( input.Airborne )
		{
			Set( parameters, "carry", "air-mount-bsgrab", "into" );
			return;
		}
		var action = input.MoveSpeed < 0.2f ? "stand-mount" : input.MoveSpeed < 2.2f ? "walk-mount"
			: input.MoveSpeed < 4.5f ? "run-mount" : "sprint-mount";
		Set( parameters, "carry", action, "into" );
		parameters.Set( "lean", "neutral" );
	}

	private void Dismount( in RiderAnimationInput input, AnimationParameters parameters )
	{
		var speed = MathF.Abs( input.Speed );
		if ( input.Airborne )
		{
			Set( parameters, "carry", string.IsNullOrEmpty( input.Grab ) ? "dismount-board-air" : $"dismount-{Short( input.Grab )}-board-air", "into" );
			if ( input.Gesturing )
				Set( parameters, "jumpout", "jumpout", "to" );
			return;
		}
		if ( speed > 4.5f )
		{
			// Stepping off at speed runs the momentum out; medium and big runouts by speed.
			Set( parameters, "runout", speed < 6f ? "m1-run" : "b1-run", "to" );
			parameters.Set( "direction", "forward" );
			parameters.Set( "height", "low" );
			return;
		}
		if ( speed > 3f )
		{
			Set( parameters, "runout", "s1-run", "to" );
			parameters.Set( "direction", "forward" );
			return;
		}
		if ( speed > 1.5f )
		{
			Set( parameters, "carry", "dismount-run", "into" );
			return;
		}
		Set( parameters, "carry", "dismount-stand", "into" );
		parameters.Set( "height", "low" );
	}

	// Flip tricks hold a generic catch named by flip direction; spinning tricks keep their own name.
	private static string CatchName( string trick ) => trick switch
	{
		"kickflip" => "kick",
		"heelflip" => "heel",
		_ => trick
	};

	private static string Short( string grab ) => grab.EndsWith( "grab", StringComparison.OrdinalIgnoreCase ) ? grab[ ..^4 ] : grab;

	private static void Set( AnimationParameters parameters, string family, string action, string phase )
	{
		parameters.Set( "family", family );
		parameters.Set( "action", action );
		parameters.Set( "phase", phase );
	}
}
