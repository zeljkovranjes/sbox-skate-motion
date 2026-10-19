namespace SkateMotion.Core.Animation;

/// <summary>
/// Describes the rider to a state graph each frame: intents from the controls (held ones with their amount,
/// "new-" ones only on the frame they start), the physical situation, the grind, speed, stance and mirroring.
/// </summary>
internal sealed class RiderStateFacts
{
	private bool _pushing;
	private bool _kickTurning;
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
	private int _slideSide = 1;
	private float _lastDeckUp = 1f;

	// The deck counts as dark side up once it has stayed upside down and steady (its up axis turning slower than
	// DarkSteadyRate per second) for DarkHold seconds; a flipping board passes through upside down while spinning.
	private const float DarkHold = 0.15f;
	private const float DarkSteadyRate = 3f;

	// Seconds a landing counts as absorbing.
	private const float AbsorbTime = 0.5f;

	internal void Fill( StateFacts facts, in RiderAnimationInput input, bool mirrored, float delta )
	{
		facts.Clear();
		var onFoot = input.Activity != RiderActivity.Riding && input.Activity != RiderActivity.Bailed;
		var holding = input.HoldingBoard || input.Activity is RiderActivity.Carrying or RiderActivity.Dropping;
		facts.Speed = onFoot ? input.MoveSpeed : input.Speed;
		facts.Mirrored = mirrored;
		facts.Switch = false;
		facts.NoBoard = onFoot && !holding;
		facts.Physics = Physics( input );
		facts.Grind = input.Grind ?? "";
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

		if ( input.Pushing )
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
		if ( input.Windup > 0.15f )
			Intent( input.TrickNollie ? "nollie-anticipation" : "ollie-anticipation", input.Windup );
		if ( !string.IsNullOrEmpty( input.Trick ) && input.TimeSincePop < 0.15f )
		{
			Intent( "trick" );
			Intent( TrickIntent( input.Trick, input.TrickNollie ) );
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
		}
		var kickTurn = MathF.Abs( input.Speed ) < 1f && MathF.Abs( input.Steer ) > 0.8f && !input.Airborne;
		if ( kickTurn )
		{
			Intent( "kick-turn", input.Steer );
			if ( !_kickTurning )
				Intent( "new-kick-turn" );
		}
		_kickTurning = kickTurn;
		if ( input.Activity == RiderActivity.Dismounting )
			Intent( "ob-dismount" );
		if ( input.Activity == RiderActivity.Mounting )
			Intent( "ob-mount" );
		// Zero is still a supplied value: stop transitions compare this intent against a small threshold.
		intents[ "ob-steer-magnitude" ] = Math.Clamp( input.MoveAmount, 0f, 1f );
		anything |= input.MoveAmount > 0f;
		if ( holding )
			Intent( "ob-hold-board" );
		if ( input.Activity == RiderActivity.PickingUp )
			Intent( "ob-retrieve-board" );
		if ( input.Activity == RiderActivity.Dropping )
			Intent( "ob-throw-board" );
		if ( !anything && MathF.Abs( input.Steer ) < 0.05f )
			intents[ "no-input" ] = 1f;

		var known = facts.Facts;
		known[ "is-holding-skateboard" ] = holding ? 1f : 0f;
		known[ "is-retrieving-skateboard" ] = input.Activity == RiderActivity.PickingUp ? 1f : 0f;
		known[ "is-dropping-skateboard" ] = input.Activity == RiderActivity.Dropping ? 1f : 0f;
		known[ "loco-state" ] = onFoot ? 1f : 0f;
		// Use the director's current gait thresholds until the movement controller supplies authored gait data.
		var gait = input.MoveSpeed < 0.2f ? "stand" : input.MoveSpeed < 2.2f ? "walk" : input.MoveSpeed < 4.5f ? "run" : "sprint";
		facts.SetArgument( "loco-state", "locostate", gait );
		// Terrain-specific gait selection still needs a ground classifier; the current adapter supplies the flat gait.
		known[ "ground-slope-type" ] = onFoot ? 1f : 0f;
		facts.SetArgument( "ground-slope-type", "slopetype", "flat" );
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
		known[ "is-dark" ] = _darkTime >= DarkHold ? 1f : 0f;
		known[ "is-skitching" ] = input.Skitching ? 1f : 0f;
		known[ "is-hand-planting" ] = input.Handplant is not null ? 1f : 0f;
		known[ "allowed-to-trick" ] = 1f;
		known[ "ok-to-do-trick-on-stairs" ] = 1f;
		known[ "can-land-on-board" ] = !input.OffBoard || input.OverBoard ? 1f : 0f;
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
		known[ "physics-wants-runout" ] = input.Activity == RiderActivity.Dismounting && MathF.Abs( input.Speed ) > 3f ? 1f : 0f;
	}

	private static string Physics( in RiderAnimationInput input ) => input.Activity switch
	{
		RiderActivity.Bailed => "wipeout",
		RiderActivity.Walking or RiderActivity.Carrying or RiderActivity.PickingUp or RiderActivity.Dropping
			=> input.Airborne ? "offboardair" : "offboard",
		RiderActivity.Riding when input.OffBoard => "offboard",
		_ => !string.IsNullOrEmpty( input.Grind ) ? "grind"
			// A trick pops from the ground: its first moments still count as grounded so the trick can start there.
			: input.Airborne && !(PopJustHappened( input )) ? "air" : "ground"
	};

	private static bool PopJustHappened( in RiderAnimationInput input ) =>
		!string.IsNullOrEmpty( input.Trick ) && input.TimeSincePop < PopWindow;

	// Seconds after a pop during which the rider still counts as on the ground.
	private const float PopWindow = 0.1f;

	// Grab names as the graph's intents spell them: toeside and heelside grabs with either hand, both hands, nose and tail.
	private static string GrabIntent( string grab ) => grab switch
	{
		"mutegrab" => "mute-grab",
		"stalegrab" => "stale-grab",
		"melon" or "japan" => "bs-grab",
		"method" or "stiffy" => "fs-grab",
		"crailgrab" => "dbl-grab",
		"nosegrab" => "nose-grab",
		"tailgrab" => "tail-grab",
		"seatbeltgrab" => "seatbelt-grab",
		_ => null
	};

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
			_ => trick
		};
		return nollie && name != "nollie" ? "n-" + name : name;
	}
}
