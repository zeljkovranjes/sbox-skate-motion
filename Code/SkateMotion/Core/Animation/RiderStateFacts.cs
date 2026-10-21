using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;

namespace SkateMotion.Core.Animation;

/// <summary>
/// Describes the rider to a state graph each frame: intents from the controls (held ones with their amount,
/// "new-" ones only on the frame they start), the physical situation, the grind, speed, stance and mirroring.
/// </summary>
internal sealed class RiderStateFacts
{
	private bool _pushing;
	private readonly KickTurn _kickTurn = new();
	private float _balanceTime;
	private float _hippyHeld;
	private float _fakieTime;

	// Backwards speeds (m/s) above which the rider counts as fakie after FakieConfirm seconds, and at once.
	private const float FakieSpeed = 0.5f;
	private const float SureFakieSpeed = 1f;
	private const float FakieConfirm = 0.2f;

	// Seconds after the hippy jump is first held that it still counts as a new push, so the graph can start it.
	private const float NewHippyWindow = 0.2f;
	private float _airTime;
	private float _darkTime;

	// Whether the board was dark side up as the current grind began, or null off a rail.
	private bool? _grindDark;

	/// <summary>True while the rider slides a board that came onto the rail dark side up (a darkslide): its clip holds
	/// the board upside down on the rail, so the clip's board is the one to show.</summary>
	internal bool Darksliding { get; private set; }

	/// <summary>
	/// Whether the state graph, as of its last update, was in a state that holds the board dark side up (one with the
	/// graph's set-dark action: a dark catch, the dark air and ground states, a darkslide). Told by the graph's player
	/// after each update, so it carries across into the next frame's facts: the board stays dark from the moment the
	/// graph makes it so until the graph leaves those states (a flip back to light, a bail).
	/// </summary>
	internal bool GraphDark { get; set; }

	/// <summary>
	/// Whether the state graph, as of its last update, was in a state stepping off the board from a grab in the air (one
	/// with the graph's air-dismounting action). Told by the graph's player after each update, like <see cref="GraphDark"/>:
	/// a grab let go of keeps the board's situation until that step off has played out, however long the grab's own into
	/// clip held it back.
	/// </summary>
	internal bool GraphAirDismounting { get; set; }

	// The board is dark side up: the graph holds it so, or the deck shown has stayed upside down and steady.
	private bool Dark => GraphDark || _darkTime >= DarkHold;

	private readonly RiderTrickFacts _tricks = new();
	private int _slideSide = 1;
	private float _lastDeckUp = 1f;

	// The deck counts as dark side up once it has stayed upside down and steady (its up axis turning slower than
	// DarkSteadyRate per second) for DarkHold seconds; a flipping board passes through upside down while spinning.
	private const float DarkHold = 0.15f;
	private const float DarkSteadyRate = 3f;

	// Seconds a landing counts as absorbing.
	private const float AbsorbTime = 0.5f;

	// A wind-up let go pops the board on the next physics step, which can come a frame after the animation sees the
	// release: the wind-up is still asked for PopGrace seconds after it, through the pop's own frame, so the graph goes
	// from the wind-up straight into the trick instead of giving the wind-up up first.
	private const float PopGrace = 0.1f;
	private float _windup;
	private bool _windupNollie;
	private float _windupLeft;

	// Seconds since the rider began stepping off the board (zero otherwise), and its physical situation on the board.
	private float _dismounting;
	private string _boardPhysics = "ground";

	// Seconds at the start of a dismount during which the rider keeps its situation on the board; let go of in a grab in
	// the air, the rider keeps it while the grab's let-go plays out (the hands take the board along), so the graph follows
	// on into the air dismount. Once the graph plays the let-go (GraphAirDismounting) it is kept until that ends: let go of
	// early in a grab's into clip, the let-go waits for that clip's end and then plays its own, and the window closed a
	// frame before it ended, so the graph chose a step off on the ground in mid-air.
	private const float DismountWindow = 0.1f;
	private const float GrabDismountWindow = 0.3f;

	// Whether the rider was grabbing as it last rode, and whether the step off under way began in a grab in the air (the
	// board's controls are let go as the rider steps off, so the grab is known from the riding before it).
	private bool _ridingGrab;
	private bool _grabDismount;

	// What the rider did the frame before, and its gait then: a mount keeps the gait it was started from (the step onto
	// the board starts from rest, which would read as standing), and riding straight onto foot with no step between is
	// the instant step off of a spawn.
	private RiderActivity _previousActivity = RiderActivity.Riding;
	private string _gait = "stand";
	private string _mountGait;
	private float _spawned = float.PositiveInfinity;

	// Seconds the spawn's step off is asked for.
	private const float SpawnWindow = 0.2f;

	// Setting off from a standstill the walker is at walking pace at once, but the clips step out of the stand first: the
	// standing gait is kept StartHold seconds into a start, long enough for the graph to choose its step out.
	private const float StartHold = 0.1f;
	private float _starting;

	// A jump on foot, as its clips are chosen: big or small, and landing level or lower; kept from take-off to landing.
	private string _jumpArc;
	private bool _wasAirborne;

	// Rising faster than this (m/s) as the feet leave the ground is a jump rather than a step off an edge, and a flight
	// longer than BigFlight seconds is a big one.
	private const float JumpRise = 0.2f;
	private const float BigFlight = 0.6f;

	internal void Fill( StateFacts facts, in RiderAnimationInput input, bool mirrored, float delta )
	{
		facts.Clear();
		var onFoot = input.Activity != RiderActivity.Riding && input.Activity != RiderActivity.Bailed;
		var releasing = input.Activity is RiderActivity.Dropping or RiderActivity.Throwing;
		var holding = input.HoldingBoard || input.Activity == RiderActivity.Carrying || releasing;
		facts.Speed = onFoot ? input.MoveSpeed : input.Speed;
		facts.Mirrored = mirrored;
		facts.Switch = false;
		facts.NoBoard = onFoot && !holding;
		facts.Physics = Physics( input );
		DarkslideOut( facts, input, mirrored );
		// Stepping off, the first moment keeps the rider's situation on the board (in the air, on a rail, off the deck in a
		// hippy jump) so the graph picks how it gets off; from then on the rider is on foot, and on-board states hand over.
		// A board rolling up a wall is on the ground, but a rider stepping off it there is in the air.
		if ( input.Activity == RiderActivity.Riding )
			_ridingGrab = !string.IsNullOrEmpty( input.Grab );
		else if ( input.Activity == RiderActivity.Dismounting && _dismounting == 0f )
			_grabDismount = _ridingGrab && _boardPhysics == "air";
		var leaving = input.Activity == RiderActivity.Dismounting
			&& (_dismounting < (_grabDismount ? GrabDismountWindow : DismountWindow) || (_grabDismount && GraphAirDismounting));
		if ( input.Activity == RiderActivity.Riding )
			_boardPhysics = facts.Physics;
		else if ( input.Activity == RiderActivity.Dismounting )
			facts.Physics = leaving ? (_boardPhysics == "ground" && input.Airborne ? "air" : _boardPhysics)
				: input.Airborne ? "offboardair" : "offboard";
		_dismounting = input.Activity == RiderActivity.Dismounting ? _dismounting + delta : 0f;
		facts.Grind = Grind( facts, input );
		_airTime = input.Airborne ? _airTime + delta : 0f;
		var turning = delta > 0f ? MathF.Abs( input.DeckUp - _lastDeckUp ) / delta : 0f;
		_lastDeckUp = input.DeckUp;
		// Turning over in a body flip the rider goes with the board, which is not upside down under its feet.
		_darkTime = input.DeckUp < -0.3f && turning < DarkSteadyRate && !input.BodyFlipping ? _darkTime + delta : 0f;

		var intents = facts.Intents;
		var anything = false;
		void Intent( string name, float value = 1f )
		{
			intents[ name ] = value;
			anything = true;
		}

		var popping = !string.IsNullOrEmpty( input.Trick ) && input.TimeSincePop < 0.15f;
		if ( input.Windup > 0.15f )
		{
			_windup = input.Windup;
			_windupNollie = input.WindupNose;
			_windupLeft = PopGrace;
		}
		else
		{
			_windupLeft -= delta;
		}
		var windingUp = input.Windup > 0.15f || _windupLeft > 0f;
		// Crouched for a pop, the rider is not pushing too: asked for both, the graph would swap between the push and the
		// wind-up every frame.
		if ( input.Pushing && !windingUp )
		{
			var explicitFeet = input.PushLeft || input.PushRight;
			if ( input.PushLeft ) Intent( "left-push" );
			if ( input.PushRight ) Intent( "right-push" );
			if ( !explicitFeet )
				Intent( mirrored ? "right-push" : "left-push" );
			if ( explicitFeet ? input.NewPush : !_pushing )
				Intent( "new-push" );
		}
		_pushing = input.Pushing;
		// The hippy jump is asked for as both feet pushing at once.
		if ( input.HippyCrouch )
		{
			Intent( "left-push" );
			Intent( "right-push" );
			if ( _hippyHeld <= NewHippyWindow )
				Intent( "new-push" );
			_hippyHeld += delta;
		}
		else
		{
			_hippyHeld = 0f;
		}
		if ( input.Brake > 0.2f )
			Intent( "brake", input.Brake );
		if ( input.Sliding )
			Intent( "slide" );
		// A revert spins the board out of a slide: backside out of a left slide, frontside out of a right one.
		if ( input.Reverting )
			Intent( _slideSide < 0 ? "slide-bs180" : "slide-fs180" );
		if ( input.Pumping )
			Intent( "crouch" );
		if ( input.Balance != 0 )
		{
			_balanceTime += delta;
			Intent( "manual", input.Balance > 1 ? 1f : -1f );
			Intent( "manual-engage-time", _balanceTime );
		}
		else
		{
			_balanceTime = 0f;
		}
		if ( windingUp )
			Intent( Anticipation( _windupNollie, input.WindupSpin ), _windup );
		if ( popping )
		{
			Intent( "trick" );
			if ( !_tricks.Pop( intents, input, mirrored ) )
				Intent( TrickIntent( input.Trick, input.TrickNollie ) );
		}
		// A late flick in the air: a late flip by the foot that flicked it, or with a grab held, a finger flip.
		if ( input.Airborne && !RiderTrickFacts.Redirects( input ) && LateIntent( input ) is { } late )
		{
			Intent( "trick" );
			Intent( late );
			if ( !string.IsNullOrEmpty( input.Grab ) )
				Intent( input.LateTrick.StartsWith( "shuvit" ) ? (input.LateFrontside ? "finger-flip-shuv" : "finger-flip-var")
					: "finger-flip" );
		}
		// How far the trick stick tweaks a grab up or down; a grab lets go only with the tweak eased off.
		if ( input.Airborne )
			intents[ "tweak-y" ] = input.Adjust;
		if ( !string.IsNullOrEmpty( input.Grab ) )
		{
			Intent( "grab" );
			Intent( input.Steer >= 0f ? "right-air-grab" : "left-air-grab" );
			// The graph picks the grab by its own names.
			if ( GrabIntent( input.Grab ) is { } grab )
				Intent( grab );
			// On a rail the grab is the grind's own, by the hand that reaches: the back hand to the toe side, the front
			// hand to the heel side, or both.
			if ( !string.IsNullOrEmpty( input.Grind ) && GrindGrabIntent( input.Grab ) is { } grindGrab )
				Intent( grindGrab );
		}
		// The board's own kick turn rule: slow, with the steering pushed over, on the ground and not in a manual.
		var wasKickTurning = _kickTurn.Turning;
		if ( _kickTurn.Step( input.Speed, input.Steer, !input.Airborne && string.IsNullOrEmpty( input.Grind ) && input.Balance == 0 ) )
		{
			Intent( "kick-turn", input.Steer );
			if ( !wasKickTurning )
				Intent( "new-kick-turn" );
		}
		if ( input.Activity == RiderActivity.Dismounting )
			Intent( "ob-dismount" );
		if ( input.Activity == RiderActivity.Mounting )
			Intent( "ob-mount" );
		// The mount control held: waiting at the lip of a drop-in (let go, the rider goes over).
		if ( input.MountHeld )
			Intent( "ob-mount-raw" );
		// Zero is still a supplied value: stop transitions compare this intent against a small threshold.
		// Pulled back (below zero) only at a drop-in's lip, where the graph steps back from it.
		intents[ "ob-steer-magnitude" ] = Math.Clamp( input.MoveAmount, -1f, 1f );
		anything |= input.MoveAmount > 0f;
		if ( holding )
			Intent( "ob-hold-board" );
		if ( input.Activity == RiderActivity.PickingUp )
			Intent( "ob-retrieve-board" );
		if ( releasing )
			Intent( "ob-throw-board" );
		// Riding one moment and on foot the next, with no step off between, is the instant step off of a spawn.
		var onFootNow = input.Activity is RiderActivity.Walking or RiderActivity.Carrying;
		if ( onFootNow && _previousActivity == RiderActivity.Riding )
			_spawned = 0f;
		if ( onFootNow && _spawned < SpawnWindow )
		{
			Intent( "ob-spawn-dismount" );
			_spawned += delta;
		}
		else
		{
			_spawned = float.PositiveInfinity;
		}
		if ( !anything && MathF.Abs( input.Steer ) < 0.05f )
			intents[ "no-input" ] = 1f;

		_tricks.Fill( facts, input, mirrored, delta );

		var known = facts.Facts;
		known[ "is-holding-skateboard" ] = holding ? 1f : 0f;
		known[ "is-retrieving-skateboard" ] = input.Activity == RiderActivity.PickingUp ? 1f : 0f;
		known[ "is-dropping-skateboard" ] = releasing ? 1f : 0f;
		known[ "loco-state" ] = onFoot ? 1f : 0f;
		// Use the director's current gait thresholds until the movement controller supplies authored gait data.
		var gait = input.MoveSpeed < 0.2f ? "stand" : input.MoveSpeed < 2.2f ? "walk" : input.MoveSpeed < 4.5f ? "run" : "sprint";
		if ( input.Activity == RiderActivity.Mounting )
		{
			_mountGait ??= _previousActivity is RiderActivity.Walking or RiderActivity.Carrying ? _gait : gait;
			gait = _mountGait;
		}
		else
		{
			_mountGait = null;
		}
		var standing = _previousActivity is RiderActivity.Walking or RiderActivity.Carrying && _gait == "stand";
		if ( onFootNow && standing && gait != "stand" && _starting < StartHold )
		{
			_starting += delta;
			gait = "stand";
		}
		else if ( gait == "stand" )
		{
			_starting = 0f;
		}
		facts.SetArgument( "loco-state", "locostate", gait );
		if ( onFootNow )
			_gait = gait;
		// The ground walked on picks the walking clips for ramps and stairs.
		known[ "ground-slope-type" ] = onFoot ? 1f : 0f;
		facts.SetArgument( "ground-slope-type", "slopetype", onFoot ? input.GroundSlope ?? GroundSlope.Flat : GroundSlope.Flat );
		// Rolling backwards for the stance (Speed is negative): at once above a walking pace, after a moment below it.
		var backwards = -input.Speed;
		_fakieTime = backwards > FakieSpeed ? _fakieTime + delta : 0f;
		known[ "is-riding-fakie" ] = backwards > SureFakieSpeed || (backwards > FakieSpeed && _fakieTime >= FakieConfirm) ? 1f : 0f;
		known[ "is-riding-switch" ] = input.Switch ? 1f : 0f;
		known[ "is-body-flipping" ] = input.BodyFlipping ? 1f : 0f;
		// Meeting a wall is not a landing: the rider rides on along it.
		known[ "has-tilt-to-large-for-preland" ] = input.WallRiding ? 1f : 0f;
		known[ "is-riding-goofy" ] = input.Goofy ? 1f : 0f;
		known[ "is-mirrored" ] = mirrored ? 1f : 0f;
		known[ "is-dark" ] = Dark ? 1f : 0f;
		known[ "is-skitching" ] = input.Skitching ? 1f : 0f;
		HandPlant( facts, input );
		known[ "allowed-to-trick" ] = 1f;
		known[ "ok-to-do-trick-on-stairs" ] = 1f;
		// Only a rider on the board (mounting it, or in the first moment of stepping off) has a board under it to land on.
		// A board held in the hand is not under the rider: mounting with it in the air puts it there first.
		var boardUnder = (input.Activity is RiderActivity.Riding or RiderActivity.Mounting || leaving) && !input.HoldingBoard;
		known[ "can-land-on-board" ] = boardUnder && (!input.OffBoard || input.OverBoard) ? 1f : 0f;
		// On foot in the air (a jump, a fall, a step off the board up high) the biped's own air states play, and its speed
		// picks the standing or running jump and dismount.
		known[ "is-in-biped-air" ] = onFoot && input.Airborne ? 1f : 0f;
		known[ "can-biped-land" ] = 1f;
		JumpArc( facts, input, onFoot );
		// At the lip of a ramp with the board in hand the rider can drop in; just over it, the board goes over the lip as on a
		// rail (the drop-in's own ride).
		if ( onFoot && float.IsFinite( input.EdgeDistance ) )
			known[ "dist-to-edge" ] = input.EdgeDistance;
		if ( input.Activity == RiderActivity.Riding && input.DroppingIn )
		{
			facts.Physics = "grind";
			known[ "is-dropping-in" ] = 1f;
		}
		FootPlant( facts, input );
		if ( onFoot )
			known[ "com-vel-compare" ] = input.MoveSpeed;
		known[ "trucks-or-deck-in-contact" ] = input.Airborne ? 0f : 1f;
		var slope = MathF.Abs( input.DeckPitch ) * 180f / MathF.PI;
		known[ "skate-slope" ] = slope;
		known[ "surface-slope" ] = slope;
		known[ "time-to-land" ] = !input.Airborne ? 0f
			: input.TimeToLand > 0f ? input.TimeToLand : MathF.Max( 0f, 0.6f - _airTime );
		known[ "ob-time-to-land" ] = input.OffBoard ? input.TimeToLandOnBoard : known[ "time-to-land" ];
		known[ "apex-reached" ] = input.OffBoard ? (input.OffBoardSpeed <= 0f ? 1f : 0f)
			: input.Airborne && input.VerticalSpeed <= 0f ? 1f : 0f;
		known[ "is-bumped" ] = 0f;
		// A push stops at the end of its stroke once the push is let go.
		known[ "break-out-of-push" ] = input.Pushing ? 0f : 1f;
		if ( input.SlideSide != 0 )
			_slideSide = input.SlideSide;
		known[ "can-enter-slide" ] = input.SlideSide != 0 ? 1f : 0f;
		known[ "should-leave-slide" ] = input.SlideSide == 0 ? 1f : 0f;
		facts.SetArgument( "can-enter-slide", "right", _slideSide > 0 ? "true" : "false" );
		facts.SetArgument( "should-leave-slide", "right", _slideSide > 0 ? "true" : "false" );
		known[ "has-landing-type" ] = input.LandingKind is null ? 0f : 1f;
		facts.SetArgument( "has-landing-type", "landing-type", input.LandingKind ?? "" );
		// While a landing absorbs (its clips run about half a second) a held crouch does not cut it short.
		known[ "clip-flag:absorb-land" ] = !input.Airborne && input.TimeSinceLanding < AbsorbTime ? 1f : 0f;
		// Only the board's own runout at speed onto the ground runs it out. A step off is not one: on-board states answer a
		// runout with a wipeout (a landing, a grab, a body flip) or a ground runout, which would play in mid-air.
		known[ "physics-wants-runout" ] = input.Activity == RiderActivity.Dismounting && input.Runout && !input.Airborne
			&& MathF.Abs( input.Speed ) > 3f ? 1f : 0f;
		_previousActivity = input.Activity;
	}

	// A foot off in a grab on the way down: the free foot reaches for the ground as the landing nears and plants on it just
	// before the board comes down (the graph's foot plants).
	private static void FootPlant( StateFacts facts, in RiderAnimationInput input )
	{
		var oneFoot = input.Activity == RiderActivity.Riding && input.Airborne && !string.IsNullOrEmpty( input.Grab )
			&& input.FeetOff is 1 or 2 && input.VerticalSpeed < 0f && input.TimeToLand > 0f;
		facts.Facts[ "should-prepare-one-foot-air-for-footplant" ] = oneFoot && input.TimeToLand < FootPlantReach ? 1f : 0f;
		facts.Facts[ "is-foot-planting" ] = oneFoot && input.TimeToLand < FootPlantContact ? 1f : 0f;
	}

	// Seconds before landing that the free foot reaches for the ground, and that it touches it.
	private const float FootPlantReach = 0.45f;
	private const float FootPlantContact = 0.12f;

	// A jump on foot is classed as its feet leave the ground, so the graph can pick its clips and, from those, its landing:
	// rising, it is a jump that lands level; already falling, it stepped off an edge and lands lower. A flight longer than
	// BigFlight is a big one.
	private void JumpArc( StateFacts facts, in RiderAnimationInput input, bool onFoot )
	{
		var airborne = onFoot && input.Airborne;
		if ( airborne && !_wasAirborne )
		{
			var size = input.TimeToLand >= BigFlight ? "big" : "sml";
			_jumpArc = size + (input.VerticalSpeed > JumpRise ? "-fwd" : "-dn");
		}
		else if ( !airborne )
		{
			_jumpArc = null;
		}
		_wasAirborne = airborne;
		facts.Facts[ "ob-jump-arc" ] = _jumpArc is null ? 0f : 1f;
		if ( _jumpArc is not null )
			facts.SetArgument( "ob-jump-arc", "arc", _jumpArc );
	}

	private static string Physics( in RiderAnimationInput input ) => input.Activity switch
	{
		RiderActivity.Bailed => "wipeout",
		RiderActivity.Walking or RiderActivity.Carrying or RiderActivity.PickingUp or RiderActivity.Dropping
			or RiderActivity.Throwing => input.Airborne ? "offboardair" : "offboard",
		RiderActivity.Riding when input.OffBoard => "offboard",
		_ => !string.IsNullOrEmpty( input.Grind ) ? "grind"
			// A trick pops from the ground: its first moments still count as grounded so the trick can start there.
			: input.Airborne && !(PopJustHappened( input )) ? "air" : "ground"
	};

	// Popped off a darkslide, the pop's first moments still count as on the rail, so the graph takes the trick from there:
	// its darkslide outs, which turn the board back over. A pop with no ollie in it goes out straight, or to the side the
	// rider steers (the steer read for the clips' stance).
	private void DarkslideOut( StateFacts facts, in RiderAnimationInput input, bool mirrored )
	{
		if ( input.TimeSincePop < _lastSincePop )
			_popFromDarkslide = Darksliding;
		_lastSincePop = input.TimeSincePop;
		if ( !_popFromDarkslide || !PopJustHappened( input ) )
			return;
		if ( facts.Physics == "ground" )
			facts.Physics = "grind";
		var steer = mirrored ? -input.Steer : input.Steer;
		facts.Facts[ "grind-trick-out-type-allowed" ] = 1f;
		facts.SetArgument( "grind-trick-out-type-allowed", "type", steer < -OutSteer ? "left" : steer > OutSteer ? "right" : "normal" );
	}

	private float _lastSincePop;
	private bool _popFromDarkslide;

	// Steering past this picks a darkslide's left or right out.
	private const float OutSteer = 0.3f;

	private static bool PopJustHappened( in RiderAnimationInput input ) =>
		!string.IsNullOrEmpty( input.Trick ) && input.TimeSincePop < PopWindow;

	// Seconds after a pop during which the rider still counts as on the ground.
	private const float PopWindow = 0.1f;

	// A hand plant on a ramp's lip, frontside or backside, with the part of it under way; while planted the feet can
	// come off: one by the push buttons' feet, both with the catch button.
	private static void HandPlant( StateFacts facts, in RiderAnimationInput input )
	{
		var known = facts.Facts;
		known[ "is-hand-planting" ] = input.Handplant is null ? 0f : 1f;
		known[ "should-play-hand-plant-anim" ] = input.Handplant is "into" or "out" ? 1f : 0f;
		if ( input.Handplant is null )
			return;
		facts.SetArgument( "is-hand-planting", "state", input.Airborne ? "air" : "ground" );
		facts.SetArgument( "is-hand-planting", "dir", input.HandplantFrontside ? "fs" : "bs" );
		facts.SetArgument( "should-play-hand-plant-anim", "anim", input.Handplant );
		if ( input.Handplant != "cycle" )
			return;
		if ( input.Catch || input.FeetOff == 3 )
			facts.Intents[ "hand-plant-dismount" ] = 1f;
		else if ( input.FeetOff != 0 )
			facts.Intents[ (input.FeetOff & 2) != 0 ? "hand-plant-one-foot-right" : "hand-plant-one-foot-left" ] = 1f;
	}

	// Grinds as the graph names them, with the routes it takes for some: a darkslide's approach side, and whether a
	// blunt is on the top of a ledge.
	private string Grind( StateFacts facts, in RiderAnimationInput input )
	{
		if ( string.IsNullOrEmpty( input.Grind ) )
		{
			_grindDark = null;
			Darksliding = false;
			return "";
		}
		_grindDark ??= Dark;
		Darksliding = _grindDark.Value && input.Grind is "boardslide" or "noseslide" or "tailslide";
		facts.Facts[ "is-grind-approach" ] = 1f;
		facts.SetArgument( "is-grind-approach", "facing", input.GrindFrontside ? "f" : "b" );
		// An end slide with the deck over a ledge's top takes the ledge-top (backslash) blunt routes.
		facts.Facts[ "is-grind-blunting-backslash" ] = input.GrindOnLedgeTop ? 1f : 0f;
		return GrindNames.Graph( input.Grind, input.GrindFrontside, input.GrindNoseToHeels, input.GrindBackward,
			input.GrindTwisted, input.GrindLow, _grindDark.Value, input.GrindOnLedgeTop );
	}

	// Grab on a rail by the hand that reaches: the back hand's grabs, the front hand's, or both hands.
	private static string GrindGrabIntent( string grab ) => grab switch
	{
		"stalegrab" or "method" or "tailgrab" or "stiffy" or "indy" or "nosecrail" => "grind-fs-grab",
		"mutegrab" or "melon" or "japan" or "nosegrab" or "seatbeltgrab" => "grind-bs-grab",
		"crailgrab" or "rocketair" => "grind-double-grab",
		_ => null
	};

	// Grab names as the graph's intents spell them: toeside and heelside grabs with either hand, both hands, nose and tail.
	private static string GrabIntent( string grab ) => grab switch
	{
		"mutegrab" => "mute-grab",
		"stalegrab" => "stale-grab",
		"melon" or "japan" => "bs-grab",
		"method" or "stiffy" => "fs-grab",
		"crailgrab" => "dbl-grab",
		"nosegrab" => "nose-grab",
		"indy" => "fs-grab",
		"nosecrail" => "crail-grab",
		"rocketair" => "rocket-air",
		"tailgrab" => "tail-grab",
		"seatbeltgrab" => "seatbelt-grab",
		_ => null
	};

	// The wind-up as the graph names it, off the tail or the nose; leaned to a side it winds up a shove-it, a 360
	// shove-it when leaned well over (toward the toes is frontside).
	private static string Anticipation( bool nose, float spin )
	{
		var lean = MathF.Abs( spin );
		var shove = lean < 0.5f ? null : lean < 0.87f ? "pop-shuvit" : "360-pop-shuvit";
		if ( shove is null )
			return nose ? "nollie-anticipation" : "ollie-anticipation";
		var side = spin > 0f ? (shove == "pop-shuvit" ? "fs-" : "fs") : "";
		return $"{(nose ? "n-" : "")}{side}{shove}-anticipation";
	}

	// Late flips as the graph names them: "l-f-" flicked by the front foot, "l-b-" by the back one.
	private static string LateIntent( in RiderAnimationInput input )
	{
		var foot = input.LateBackFoot ? "l-b-" : "l-f-";
		return input.LateTrick switch
		{
			"kickflip" => foot + "kickflip",
			"heelflip" => foot + "heelflip",
			"shuvit-high" or "shuvit-low" => input.LateFrontside ? "l-fs-shuvit" : "l-bs-shuvit",
			_ => null
		};
	}

	// Trick names as the graph's intents spell them; nollie variants carry an "n-" prefix.
	private static string TrickIntent( string trick, bool nollie )
	{
		var name = trick switch
		{
			"ollie" or "nollie" => nollie ? "nollie" : "ollie",
			"shuvit" => "pop-shuvit",
			"shuvit-frontside" => "fs-pop-shuvit",
			"shuvit360" => "360-pop-shuvit",
			"shuvit360-frontside" => "fs360-pop-shuvit",
			"treflip" => "360-flip",
			"varialkickflip" => "varial-kickflip",
			"varialheelflip" => "varial-heelflip",
			"inwardheelflip" => "inward-heelflip",
			"hardflip360" => "360-hardflip",
			"inwardheelflip360" => "360-inward-heelflip",
			_ => trick
		};
		return nollie && name != "nollie" ? "n-" + name : name;
	}
}
