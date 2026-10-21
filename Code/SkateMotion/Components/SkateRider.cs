using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;
using SkateMotion.Core.Vehicle;
using SkateMotion.Engine;
using SkateMotion.Systems;

namespace SkateMotion.Components;

/// <summary>A rider who can skate, walk, carry their board and remount.</summary>
[Title( "Skate Rider" ), Category( "Skate Motion" )]
public sealed class SkateRider : Component
{
	/// <summary>The skateboard owned by this rider.</summary>
	[Property] public Skateboard Board { get; set; }

	/// <summary>Optional Citizen-compatible avatar; null uses the built-in Citizen.</summary>
	[Property] public Model CharacterModel { get; set; }

	/// <summary>Use keyboard/controller actions locally; disable for scripted input.</summary>
	[Property] public bool ReadLocalInput { get; set; } = true;

	/// <summary>Numeric controller profile in the game's local data folder.</summary>
	[Property, Group( "Controls" )] public string ControllerProfile { get; set; } = "";

	/// <summary>Ride with the right foot forward.</summary>
	[Property] public bool Goofy { get; set; }

	/// <summary>
	/// Clip set among the mounted files or in the game's data folder; by default the rider this package ships. Empty uses
	/// the built-in animation.
	/// </summary>
	[Property, Group( "Animation" )] public string AnimationClips { get; set; } = DefaultAnimationClips;

	/// <summary>Animation graph JSON that tags and plays the clip set, found as the clip set is.</summary>
	[Property, Group( "Animation" )] public string AnimationGraph { get; set; } = DefaultAnimationGraph;

	/// <summary>The clip set this package ships, played by <see cref="DefaultAnimationGraph"/>.</summary>
	public const string DefaultAnimationClips = "data/skater/skater-corrected.sman";

	/// <summary>The graph this package ships for <see cref="DefaultAnimationClips"/>.</summary>
	public const string DefaultAnimationGraph = "data/skater/skater.graph.json";

	/// <summary>Preferred clip style tag (for example a signature set); empty plays the standard motions.</summary>
	[Property, Group( "Animation" )] public string AnimationStyle { get; set; } = "";

	/// <summary>
	/// Whether walking and carrying the board answer the controls at once, as a game's own player controller does (quick
	/// to start, stop and turn); off, the rider eases in and out of a walk as a skater on foot does.
	/// </summary>
	[Property, Group( "Controls" )] public bool ResponsiveWalking { get; set; }

	/// <summary>Farthest (metres) the board is called back to the hand from.</summary>
	[Property, Group( "Controls" )] public float RecallRange { get; set; } = Core.Physics.BoardReach.Farthest;

	/// <summary>
	/// Whether only a board in sight can be called back to the hand; off, it flies back through whatever is in the way,
	/// as a thrown hammer called home does.
	/// </summary>
	[Property, Group( "Controls" )] public bool RecallNeedsSight { get; set; } = true;

	/// <summary>Seconds a board called from afar shakes where it lies before it flies to the hand; 0 for none.</summary>
	[Property, Group( "Controls" )] public float RecallShake { get; set; }

	/// <summary>
	/// Raised where the rider is simulated as its board is called to the hand: the seconds it shakes where it lies first
	/// (see <see cref="RecallShake"/>), then the seconds it flies. A game can time a sound or an effect from it.
	/// </summary>
	public event System.Action<float, float> BoardCalled;

	/// <summary>Whether a bailed rider gets up by themselves once they have lain still for a while.</summary>
	[Property, Group( "Riding" )] public bool RecoverAutomatically { get; set; } = true;

	/// <summary>Collision-aware walking controller on the rider object.</summary>
	[RequireComponent] public CharacterController Walker { get; set; }

	/// <summary>Current skating, walking or carrying mode.</summary>
	public SkateRiderMode Mode => _motor?.Mode ?? SkateRiderMode.Riding;

	/// <summary>Name of the playing clip-graph state, or null when the built-in animation is used.</summary>
	public string AnimationState => _animator?.ClipState;

	/// <summary>Name of the clip playing now, or null when the built-in animation is used.</summary>
	public string AnimationMotion => _animator?.ClipMotion;

	/// <summary>
	/// The board-handling motion playing over the body (FrontInto, FrontCycle, FrontOut, BackInto, BackCycle, BackOut, Drop
	/// or Throw), or null when none plays.
	/// </summary>
	public string AnimationOverlay => _animator?.HandlingMotion;

	/// <summary>True while the rider rides switch: the other foot forward, the board's tail leading.</summary>
	public bool RidingSwitch => _animator?.Switch ?? false;

	/// <summary>
	/// Lowest gap in inches between any part of a foot or shin and the board's deck this frame (negative: a foot is inside the
	/// deck; null: no foot over it). For checking foot placement on custom riders and boards.
	/// </summary>
	public float? FootDeckGap => _animator?.FootDeckGap;

	/// <summary>
	/// Gap in inches between the left foot's sole and the board's deck this frame, on the side of the deck it stands on
	/// (negative: inside the deck; null: not over it, not riding, or let go of the board by the animation). For checking that
	/// each foot stands on the deck.
	/// </summary>
	public float? LeftSoleDeckGap => _animator?.SoleDeckGaps?[ 0 ];

	/// <summary>As <see cref="LeftSoleDeckGap"/>, for the right foot.</summary>
	public float? RightSoleDeckGap => _animator?.SoleDeckGaps?[ 1 ];

	/// <summary>
	/// How high (inches) the animation's clip holds the left ball of the foot over its own board this frame (null: not
	/// riding). Higher than it stands, the clip is lifting that foot (stepping it, an ollie's).
	/// </summary>
	public float? LeftClipToeHeight => _animator?.ClipToeHeights?[ 0 ];

	/// <summary>As <see cref="LeftClipToeHeight"/>, for the right foot.</summary>
	public float? RightClipToeHeight => _animator?.ClipToeHeights?[ 1 ];

	/// <summary>
	/// Gap (inches) between the hand holding the board, measured from the rider's mesh, and the deck as shown; negative
	/// when the hand is inside the deck, null when the board is not held or the hand is not over the deck.
	/// </summary>
	public float? HandDeckGap => Board.IsValid() ? _animator?.HandDeckGap( Board.WorldTransform ) : null;

	/// <summary>
	/// Plays a clip-set motion described by tags (at least "family" and "action"; optionally style, side,
	/// height, lean, speed, strength, direction, detail, kind and phase) for the given seconds.
	/// </summary>
	public void PlayAnimation( IReadOnlyDictionary<string, string> tags, float seconds )
	{
		ArgumentNullException.ThrowIfNull( tags );
		_animator?.Request( tags, seconds );
	}

	/// <summary>Actions the loaded clip graph offers for a family (optionally one style), e.g. every gesture.</summary>
	public IReadOnlyList<string> AnimationActions( string family, string style = null ) =>
		_animator?.Actions( family, style ) ?? Array.Empty<string>();

	/// <summary>Plays the next gesture in the clip graph: board gestures while riding, grind gestures on a rail.</summary>
	public void NextGesture( float seconds = 2f )
	{
		var style = Board.State.Grind != SkateGrind.None ? "grind" : "board";
		var actions = AnimationActions( "gesture", style );
		if ( actions.Count == 0 ) return;
		var action = actions[ _gesture++ % actions.Count ];
		PlayAnimation( new Dictionary<string, string>
		{
			[ "family" ] = "gesture", [ "action" ] = action, [ "style" ] = style, [ "phase" ] = "cycle"
		}, seconds );
	}

	/// <summary>True while a gameplay pickup, drop, mount or dismount is moving into place.</summary>
	public bool IsInteracting => _interaction?.Active ?? false;

	/// <summary>Whether the last completed gameplay interaction reached its destination.</summary>
	public bool LastInteractionSucceeded { get; private set; }

	private RiderMotor _motor;
	private RiderAnimator _animator;
	private RiderInteraction _interaction;
	private readonly Core.Physics.HeldBoardMotion _held = new();
	private readonly LocalSkateInput _input = new();
	private readonly HippyJump _hippyJump = new();
	private RiderControllerInput _controller;
	private Vector3 _walk;
	private bool _jump;
	private int _gesture;
	internal GameObject VisualObject => _animator?.Visual;

	/// <summary>Reads the stock walk the rider's legs follow on foot, once a frame after every update.</summary>
	internal void ReadLegs() => _animator?.ReadLegs();

	// Where the rider is shown this frame, smoothed between fixed steps; the camera follows it on foot and in a bail.
	internal Transform ShownTransform => _animator?.Smoother.At( WorldTransform, Time.Now ) ?? WorldTransform;

	/// <summary>
	/// Starts a visible step onto a reachable board. With the board in hand the rider puts it down under its feet and gets
	/// on as it goes, walking, running or sprinting onto it, or in the air under its feet before it lands.
	/// </summary>
	public bool RequestMount()
	{
		// Standing at the lip of a ramp with the board in hand, the rider drops in instead: it steps up to the lip, sets the
		// board's tail on it and waits there while the mount control is held, going over as it is let go.
		if ( Mode == SkateRiderMode.Carrying && _motor.Velocity.WithZ( 0f ).Length < DropInStill
			&& BeginInteraction( RiderAction.DropIn ) )
			return true;
		if ( Mode == SkateRiderMode.Carrying )
			return BeginInteraction( RiderAction.Mount );
		if ( !BeginInteraction( RiderAction.Mount ) ) return false;
		// A board left upside down (a dark landing run out, a dark bail) is turned back over as the rider steps on.
		Board.TurnUpright();
		return true;
	}

	/// <summary>Starts a collision-constrained step off the board.</summary>
	public bool RequestDismount() => BeginDismount( false );

	// Whether the step off under way is the board's own runout rather than the rider getting off.
	private bool _runout;

	private bool BeginDismount( bool runout )
	{
		if ( IsProxy ) return false;
		// Getting off can move the rider (up to a hippy jump's flight, out of a wall it rode along) after its model was
		// placed for this frame: the model stays where it is shown and follows from the next frame.
		var shown = _animator.Visual.WorldTransform;
		// Off the deck in a hippy jump the body is up above the board: it steps off from there, still rising or falling,
		// and is shown from there rather than easing up from the board.
		var flying = _hippyJump.Flying;
		var rising = _hippyJump.VerticalSpeed * SkateUnits.InchesPerMetre;
		// Let go of in a grab in the air, the hands already hold the board: the rider takes it along.
		var grabbing = !runout && Board.State.IsAirborne && Board.CurrentInput.Grab != SkateAirGrab.None;
		if ( flying )
		{
			var flight = SkateUnits.Inches( _hippyJump.Offset );
			WorldPosition += flight;
			_animator.Smoother.Shift( flight );
		}
		var stepping = BeginInteraction( RiderAction.Dismount );
		_animator.Visual.WorldTransform = shown;
		if ( !stepping ) return false;
		// Riding, the rider's steps were recorded where following the board put it each frame; on foot they are its own
		// physics steps. It goes on from the board's, so its shown place does not jump by the difference a step later.
		Board.HandStepsTo( _animator.Smoother, WorldTransform );
		if ( flying ) _motor.Rise( rising );
		_runout = runout;
		_hippyJump.Clear();
		Board.SetRiderAttached( false );
		if ( grabbing && _motor.TakeBoard() )
		{
			Board.Enabled = false;
			_motor.KeepPlace();
		}
		return true;
	}

	/// <summary>Reaches for a nearby board before lifting it into the carrying pose.</summary>
	public bool RequestPickUp()
	{
		Configure();
		if ( !BeginInteraction( RiderAction.PickUp ) ) return false;
		// A board left upside down is turned back over as the rider reaches for it, so the hand takes it the right way up.
		Board.TurnUpright();
		return true;
	}

	/// <summary>Lets the held board drop beside the rider as the hand lets go of it.</summary>
	public bool RequestDrop() => BeginInteraction( RiderAction.Drop );

	/// <summary>Tosses the held board down ahead of the rider, letting go of it as the throw's swing does.</summary>
	public bool RequestThrow() => BeginInteraction( RiderAction.Throw );

	private bool BeginInteraction( RiderAction action )
	{
		if ( IsProxy || _interaction is null || !_interaction.Begin( action ) ) return false;
		_walk = Vector3.Zero;
		_jump = false;
		LastInteractionSucceeded = false;
		_animator.BeginInteraction();
		return true;
	}

	private void StepInteraction( float delta )
	{
		_interaction.Step( delta );
		if ( _interaction.CaptureBoard )
		{
			// The board is called to the hand: it leaves the world's simulation and flies in, held once it arrives.
			Board.Enabled = false;
			if ( !_motor.BeginRetrieval() )
			{
				Board.Enabled = true;
				_interaction.End();
				return;
			}
			if ( _interaction.UsesSourcePickup )
				_interaction.AdvancePickup( delta );
		}
		if ( !_interaction.Finished ) return;
		var action = _interaction.Action;
		var fromHand = _interaction.MountsFromHand;
		var reached = _interaction.AtDestination;
		if ( reached && action == RiderAction.PickUp )
			_motor.CompleteRetrieval();
		_mountSpent |= action == RiderAction.DropIn && !reached && _mountHeld;
		_interaction.End();
		LastInteractionSucceeded = reached && (action != RiderAction.Mount || fromHand || Mount());
	}

	// Slower than this (inches per second) the rider counts as standing for a drop-in; seconds after dropping in that the
	// board still goes over the lip (the clips' drop-in ride).
	private const float DropInStill = 40f;
	private const float DroppingIn = 0.4f;
	private float _droppedIn = float.NegativeInfinity;

	// Whether the mount control is held (letting it go at a lip drops in), and whether a drop-in was backed out of with it
	// still held: until it is let go it starts nothing more, so the rider does not step straight back up to the lip.
	private bool _mountHeld;
	private bool _mountSpent;

	// How fast (degrees per second) the rider turns toward a board it reaches for on a side no reach covers, and how
	// quickly (degrees per second squared) it takes that turn up and lets it go, so the body neither starts nor stops
	// turning at full speed in a frame.
	private const float ReachTurnRate = 360f;
	private const float ReachTurnSpeedUp = 1440f;
	private readonly Core.Vehicle.EasedTurn _reachTurn = new( ReachTurnRate, ReachTurnSpeedUp );

	// Each frame while the hands handle the board: a held board follows the hand; a drop or throw lets go of it as the
	// animation's hand does (or once it has gone on long enough without one), moving as the hand moved it; a mount from the
	// hand puts it down under the rider once the hand has brought it to the ground, or in the air as the air mount ends.
	private void HandleBoard()
	{
		var shown = _animator.CarriedBoardPose;
		var holding = _interaction.CarriesBoard;
		if ( holding && _interaction.Action is RiderAction.Drop or RiderAction.Throw && shown is { } hand )
			_held.Add( SkateUnits.ToCore( hand.Position ), SkateUnits.ToCore( hand.Rotation ), Time.Delta );
		else if ( !holding )
			_held.Reset();
		if ( !holding || IsProxy ) return;
		var pose = shown ?? Board.WorldTransform;
		if ( _interaction.Action is RiderAction.Drop or RiderAction.Throw )
		{
			if ( !_animator.BoardReleased && !_interaction.ReleaseDue ) return;
			var moving = shown is not null && _held.Velocity != System.Numerics.Vector3.Zero;
			if ( _interaction.Release( pose, moving ? SkateUnits.ToEngine( _held.Velocity ) : null,
				moving ? SkateUnits.ToEngine( _held.Spin ) : null ) )
				Board.Enabled = true;
			_held.Reset();
		}
	}

	// Getting on from the hand or dropping in: once the hand has brought the board down, it goes under the rider and the
	// rider gets on it. Done before the frame's animation, where the hand held the board the frame before: the rider moves
	// onto the board here, and moved after the animation it carried the body it had just placed along for a frame.
	private void PutBoardDown()
	{
		if ( !IsInteracting || IsProxy || !_interaction.CarriesBoard ) return;
		var pose = _animator.CarriedBoardPose ?? Board.WorldTransform;
		var rideHeight = Board.Dimensions.RideHeight * SkateUnits.InchesPerMetre;
		if ( _interaction.DropsIn )
		{
			if ( !_interaction.PlaceDropIn( pose, rideHeight ) ) return;
			Board.Enabled = true;
			// The board the clip has tipped over the lip goes on from there to where it went down on the slope.
			Board.ShowPutDown( _animator.Smoother, pose );
			Board.SetRiderAttached( true );
			_droppedIn = Time.Now;
			return;
		}
		if ( !_interaction.MountsFromHand ) return;
		// On the ground the board goes down as the mount's clip has the rider on it (the graph has gone on to riding: the
		// clip's own moment, where its board is under its feet), or at the latest when the mount has run its time.
		var onBoard = _animator.ClipState?.StartsWith( "OnBoard" ) == true;
		var down = _interaction.MountsInAir ? _interaction.MountDue : _interaction.MountDue || onBoard;
		if ( !down || !_interaction.PlaceAndMount( pose, rideHeight ) ) return;
		Board.Enabled = true;
		Board.ShowPutDown( _animator.Smoother );
		Board.SetRiderAttached( true );
	}

	// Reaching for a board on the side no reach covers, the rider turns toward it; true while it does.
	private bool TurnToReach( float delta )
	{
		if ( _interaction.Action != RiderAction.PickUp || Mode != SkateRiderMode.Walking )
		{
			_reachTurn.Reset();
			return false;
		}
		var turn = _animator.ReachTurn;
		_motor.Turn( _reachTurn.Step( turn, delta ) );
		return turn != 0f || _reachTurn.Rate != 0f;
	}

	/// <summary>Supplies world-space walking direction, a one-shot jump request, and whether to sprint while running.</summary>
	public void SetWalkInput( Vector3 direction, bool jump = false, bool sprint = false )
	{
		_walk = direction;
		_jump |= jump;
		_sprinting = sprint;
	}

	// The motor follows the rider's walking and recall options as they are now.
	private void Configure()
	{
		if ( _motor is null ) return;
		_motor.Responsive = ResponsiveWalking;
		_motor.RecallRange = RecallRange;
		_motor.RecallNeedsSight = RecallNeedsSight;
		if ( _interaction is not null )
			_interaction.Shake = RecallShake;
	}

	// How hard the rider is told to walk, as its clips see it. Walking responsively, the stick let go for under MoveGrace
	// seconds (going from one key to another) keeps the walk's clips going: seen at once, a gap of a few frames started a
	// stop clip and ran on out of it, the body snapping between them.
	private float ShownMoveAmount()
	{
		var amount = _walk.WithZ( 0f ).Length;
		if ( !ResponsiveWalking || amount > 0.01f )
		{
			_lastMoveAmount = amount;
			_moveLetGo = 0f;
			return amount;
		}
		_moveLetGo += Time.Delta;
		return _moveLetGo < MoveGrace ? _lastMoveAmount : amount;
	}

	private float _lastMoveAmount;
	private float _moveLetGo;
	private const float MoveGrace = 0.1f;

	private bool _sprinting;

	/// <summary>Steps beside the board if there is room, preserving the rider's momentum.</summary>
	public bool Dismount()
	{
		if ( IsProxy || _motor is null || !_motor.Dismount() ) return false;
		Board.SetRiderAttached( false );
		return true;
	}

	/// <summary>
	/// Puts a riding rider on its feet at <paramref name="place"/> (a player taking its place there, say), leaving the board
	/// where it is: unlike <see cref="Dismount"/> it needs no room beside the board.
	/// </summary>
	public bool StandAt( Transform place )
	{
		if ( IsProxy || _motor is null || !_motor.StandAt( place ) ) return false;
		_animator.Smoother.Reset();
		Board.SetRiderAttached( false );
		return true;
	}

	/// <summary>Picks up a nearby slow-moving board that is within line of sight.</summary>
	public bool PickUp()
	{
		if ( IsProxy || _motor is null || !_motor.CanPickUp ) return false;
		var enabled = Board.Enabled;
		Board.Enabled = false;
		if ( _motor.PickUp() ) return true;
		Board.Enabled = enabled;
		return false;
	}

	/// <summary>Drops the held board into clear space in front of the rider.</summary>
	public bool Drop()
	{
		if ( IsProxy || _motor is null || !_motor.Drop() ) return false;
		Board.Enabled = true;
		return true;
	}

	/// <summary>Mounts a nearby upright board; a carried board is placed down first.</summary>
	public bool Mount()
	{
		if ( IsProxy || _motor is null ) return false;
		if ( Mode == SkateRiderMode.Carrying && !Drop() ) return false;
		if ( !_motor.Mount() ) return false;
		NetBailed = false;
		Board.TurnUpright();
		Board.SetRiderAttached( true );
		Board.ResetSimulation();
		return true;
	}

	/// <summary>Stands beside the settled ragdoll when there is room for the walking controller.</summary>
	public bool Recover()
	{
		if ( IsProxy || _motor is null || !_motor.Recover( _animator.Ragdoll ) ) return false;
		_animator.Recover( GameObject );
		NetBailed = false;
		_walk = Vector3.Zero;
		_jump = false;
		return true;
	}

	// The body rides on the board, so a rider upside down in the air whose body reaches the ground lands head first:
	// that is a bail, and the body never passes through the ground.
	private void CheckHeadFirst()
	{
		if ( IsProxy || Mode != SkateRiderMode.Riding || !Board.State.IsAirborne || Board.State.IsBailed ) return;
		if ( Board.WorldRotation.Up.z > 0f || _animator.HeadPosition is not { } head ) return;
		var hit = Scene.Trace.Ray( Board.WorldPosition + Board.WorldRotation.Up * HeadClearStart, head )
			.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( Board.GameObject ).Run();
		if ( hit.Hit && !hit.StartedSolid )
			Board.Bail();
	}

	// Inches above the deck where the body line starts (clear of the deck and trucks).
	private const float HeadClearStart = 6f;

	// A bailed rider gets up after lying still for SettledFor seconds once AutoRecoverAfter have passed, and in any
	// case after RecoverAtLatest seconds.
	private const float AutoRecoverAfter = 3.5f;
	private const float SettledFor = 2.5f;
	private const float RecoverAtLatest = 20f;

	private void AutoRecover()
	{
		if ( !RecoverAutomatically || Mode != SkateRiderMode.Bailed || _animator.Ragdoll is not { } ragdoll ) return;
		if ( (ragdoll.Elapsed >= AutoRecoverAfter && ragdoll.Settled >= SettledFor) || ragdoll.Elapsed >= RecoverAtLatest )
			Recover();
	}

	// Off the board in a hippy jump the rider has no hold on it. The jump control itself still reads, so letting go of
	// it in the air is not taken for a fresh crouch.
	private SkateInput WhileJumping( SkateInput input ) =>
		_hippyJump.Flying ? new SkateInput( Goofy: input.Goofy ) { HippyJump = input.HippyJump } : input;

	private void StepHippyJump( float delta )
	{
		var falling = MathF.Max( 0f, -_hippyJump.VerticalSpeed );
		var offset = _hippyJump.Offset;
		var outcome = _hippyJump.Step( Board.CurrentInput.HippyJump, SkateUnits.Metres( _motor.Velocity ),
			!Board.State.IsAirborne && !Board.State.IsBailed, Board.Profile?.Gravity ?? 9.8f, delta );
		if ( outcome == HippyJump.Outcome.Landed )
		{
			_animator.OnHippyLanded( falling );
			return;
		}
		if ( outcome != HippyJump.Outcome.Missed ) return;
		// The board went out from under the jump: the rider comes down where it is and runs it out.
		WorldPosition += SkateUnits.Inches( offset ).WithZ( 0f );
		BeginDismount( true );
	}

	// Running into something side-on while upright: step off and run it out.
	private void OnRunout()
	{
		if ( IsProxy || Mode != SkateRiderMode.Riding ) return;
		BeginDismount( true );
	}

	// Deck impacts that do not bail knock the rider; the clip graph plays a short bump. The deck meeting a rail as the board
	// comes down onto it is the slide itself, and a board held dark side up rides on its deck: neither is a knock, and an
	// impact waits KnockWait to see whether the board locks onto a rail before it counts.
	private void OnBoardCollided( float speed )
	{
		if ( speed < 1f || Mode != SkateRiderMode.Riding || Board.State.IsBailed ) return;
		if ( Board.State.Grind != SkateGrind.None || Board.DarkSideUp || Time.Now - _grinded < AfterGrind )
		{
			_knock = null;
			return;
		}
		_knock ??= Time.Now;
	}

	// When the board last rode a rail: leaving one, the deck or a truck catching the rail's end or edge is the board
	// coming off it, not a knock.
	private float _grinded = float.NegativeInfinity;

	// Seconds after a grind ends that an impact is still the board coming off the rail.
	private const float AfterGrind = 0.4f;

	private float? _knock;

	// Seconds a deck impact waits for the board to lock onto a rail before it knocks the rider.
	private const float KnockWait = 0.06f;

	private void Knock()
	{
		if ( Board.State.Grind != SkateGrind.None )
			_grinded = Time.Now;
		if ( _knock is not { } knocked || Time.Now - knocked < KnockWait ) return;
		_knock = null;
		if ( Mode != SkateRiderMode.Riding || Board.State.IsBailed || Board.State.Grind != SkateGrind.None || Board.DarkSideUp
			|| knocked - _grinded < AfterGrind ) return;
		PlayAnimation( new Dictionary<string, string> { [ "family" ] = "bump", [ "action" ] = "frontknock", [ "phase" ] = "once" }, 0.45f );
	}

	private void OnBoardBailed()
	{
		if ( IsProxy || _motor is null || Mode != SkateRiderMode.Riding ) return;
		_motor.FollowBoard();
		NetBailVelocity = Board.ReleaseVelocity;
		NetBailSpin = Board.ReleaseAngularVelocity;
		NetBailOrigin = Board.ReleasePosition;
		NetBailed = true;
		_animator.Bail( NetBailVelocity, NetBailSpin, NetBailOrigin, BailMass, BailGravity );
		_motor.Bail();
		Board.SetRiderAttached( false );
	}

	// The owner's bail, shared so every other player sees the rider fall: that it let go, and how it was moving then.
	[Sync] private bool NetBailed { get; set; }
	[Sync] private Vector3 NetBailVelocity { get; set; }
	[Sync] private Vector3 NetBailSpin { get; set; }
	[Sync] private Vector3 NetBailOrigin { get; set; }
	private bool _shownBail;

	private float BailMass => (Board.Profile?.Mass ?? 80f) - 3f;
	private float BailGravity => Board.Profile?.Gravity ?? 9.81f;

	// The owner's mode, shared so every other player's copy walks, carries or rides as it does.
	[Sync] private SkateRiderMode NetMode { get; set; }

	// Another player's rider is not simulated here: its walk is shown from how fast its synced place moves (smoothed
	// over about a tenth of a second).
	private Vector3 _proxyVelocity;
	private Vector3 _proxyLast;
	private Vector3 ShownVelocity => IsProxy ? _proxyVelocity : _motor.Velocity;

	private void ShowSharedMode()
	{
		if ( !_shownBail && NetMode != SkateRiderMode.Bailed && Mode != NetMode )
			_motor.ShowMode( NetMode );
		if ( Time.Delta > 0f )
		{
			var moved = (WorldPosition - _proxyLast) / Time.Delta;
			_proxyVelocity = Vector3.Lerp( _proxyVelocity, moved, 1f - MathF.Exp( -10f * Time.Delta ) );
		}
		_proxyLast = WorldPosition;
	}

	/// <summary>
	/// Blends the rider's first poses from a body pose (model-space bone transforms, in the rider model's bone order)
	/// over seconds: the player's own body as it was, so taking its place shows no jump.
	/// </summary>
	public void BlendFrom( Transform[] modelPose, float seconds ) => _animator?.BlendFrom( modelPose, seconds );

	// Another player's rider: its ragdoll is made here from the owner's bail, and put away when the owner rides again.
	// Returns true while it lies there, when nothing else of the riding update applies.
	private bool ShowSharedBail()
	{
		if ( NetBailed && !_shownBail )
		{
			_animator.Bail( NetBailVelocity, NetBailSpin, NetBailOrigin, BailMass, BailGravity );
			_shownBail = true;
		}
		else if ( !NetBailed && _shownBail )
		{
			_animator.Recover( GameObject );
			_shownBail = false;
		}
		return _shownBail;
	}

	protected override void OnEnabled()
	{
		if ( _motor is not null && Board.IsValid() ) Subscribe();
	}

	protected override void OnDisabled()
	{
		// Taken away before it started, it never subscribed.
		if ( !Board.IsValid() || _animator is null ) return;
		Board.Bailed -= OnBoardBailed;
		Board.Collided -= OnBoardCollided;
		Board.RunoutRequested -= OnRunout;
		Board.TrickStarted -= OnBoardTrick;
		Board.Landed -= _animator.OnLanded;
	}

	private void OnBoardTrick( SkateTrick trick ) => _animator.OnTrick( trick, Board.LastTrickFromNose );

	private void Subscribe()
	{
		Board.Bailed += OnBoardBailed;
		Board.Collided += OnBoardCollided;
		Board.RunoutRequested += OnRunout;
		Board.TrickStarted += OnBoardTrick;
		Board.Landed += _animator.OnLanded;
	}

	protected override void OnStart()
	{
		if ( !Board.IsValid() || Board.GameObject == GameObject )
			throw new InvalidOperationException( "Place SkateRider on its own object and assign its Skateboard." );
		var rig = Board.Components.Get<SkateboardRig>();
		_motor = new( GameObject, Board.GameObject, Board.Body, Walker );
		_animator = new( GameObject, Board.GameObject, CharacterModel, rig?.FrontFoot, rig?.RearFoot );
		var controllerProfile = ControllerInputProfile.Load( ControllerProfile );
		_controller = controllerProfile is null ? null : new( controllerProfile );
		var boardControls = Board.Components.Get<SkateControls>();
		if ( boardControls.IsValid() ) boardControls.Enabled = false;
		_animator.UseClips( Scene.GetSystem<RiderAnimationLibrary>().Get( AnimationClips, AnimationGraph ) );
		_interaction = new( GameObject, Board.GameObject, Board.Body, _motor, _animator.RetrievalProfile );
		_interaction.Called += ( shake, flight ) => BoardCalled?.Invoke( shake, flight );
		_motor.FollowBoard();
		Subscribe();
		// The host simulates the board; the rider's owner drives it with forwarded input.
		if ( Networking.IsHost && Network.Owner is not null )
			Board.SetDriver( Network.Owner );
	}

	protected override void OnUpdate()
	{
		if ( _motor is null || !Board.IsValid() ) return;
		if ( IsProxy && ShowSharedBail() ) return;
		if ( IsProxy ) ShowSharedMode();
		else NetMode = Mode;
		if ( Board.State.IsBailed && Mode == SkateRiderMode.Riding ) OnBoardBailed();
		if ( Mode == SkateRiderMode.Bailed && _animator.Ragdoll is { } ragdoll ) _motor.FollowRagdoll( ragdoll.Position );
		PutBoardDown();
		_motor.FollowBoard();
		// On foot the animation is told whether the rider stands or is in the air (a jump, a fall, a step off up high).
		if ( Mode is SkateRiderMode.Walking or SkateRiderMode.Carrying )
			_animator.SetFooting( !_motor.Airborne, _motor.Velocity.z / SkateUnits.InchesPerMetre );
		Knock();
		_animator.PlantLip = Board.HandPlantLip;
		// Dropping in, the board the clips set down at the lip lies where they have it against where the rider stands there.
		_animator.BoardRestFrame = IsInteracting ? _interaction.DropInStand : null;
		if ( PoseThisFrame( out var poseDelta )
			&& !_animator.UpdateClips( Activity(), Board.State, Board.CurrentInput, ShownVelocity, Goofy, AnimationStyle, poseDelta,
			// Backing out of a drop-in is the stick pulled back from the lip, as the graph's step back reads it.
			moveAmount: _interaction.BacksOut ? -1f : ShownMoveAmount(), holdingBoard: Mode == SkateRiderMode.Carrying,
			hippy: _hippyJump,
			runout: _runout && IsInteracting, groundSlope: _motor.GroundSlope, edgeDistance: _motor.EdgeDistance,
			mountHeld: _mountHeld, droppingIn: Mode == SkateRiderMode.Riding && Time.Now - _droppedIn < DroppingIn ) )
		{
			_animator.Update( Mode, Board.State, ShownVelocity, _motor.Grounded, Goofy );
			if ( IsInteracting ) _animator.UpdateInteraction( _interaction, Goofy );
		}
		// Down on the ground dark side up the board cannot be ridden on its grip: as the graph's dark ground state has it, the
		// rider steps off and runs it out, leaving the board upside down.
		if ( !IsProxy && Mode == SkateRiderMode.Riding && !IsInteracting && _animator.DarkOnGround && !Board.State.IsBailed )
			BeginDismount( true );
		_motor.UpdateCarryPose( _animator.CarriedBoardPose, !IsInteracting || _interaction.CarriesBoard );
		if ( IsInteracting ) HandleBoard();
		else _held.Reset();
		if ( IsInteracting && !IsProxy ) _interaction.ShowPickup();
		CheckHeadFirst();
		Board.PushContact = Mode == SkateRiderMode.Riding ? _animator.PushContact : null;
		Board.DarkSideUp = Mode == SkateRiderMode.Riding && _animator.DarkSideUp;
		_interaction.ObserveRetrievalRequest( _animator.RetrieveRequested, _animator.HandlingMotion is not null );
		// A landing played inside an interaction (getting off in the air) carries the walker as its clip carries the body.
		_interaction.LandingTravel = _animator.LandingTravel;
		// The drop-in's clips (stepping up to the lip, going over it) set the pace of its steps.
		var droppingIn = _animator.ClipState?.Contains( "DropIn" ) == true;
		_interaction.ObserveTravel( droppingIn ? _animator.ClipTravel : null, droppingIn ? _animator.ClipTravelOffset : null );
		if ( IsProxy || !ReadLocalInput ) return;
		// The keyboard and mouse and the first controller are read every frame and merged control by control
		// (DeviceInputMerge), so a drifting stick or a resting trigger never locks out held keys, nor keys a controller.
		var raw = LocalControllerInput.Read();
		var pad = DeviceInputMerge.Settle( raw );
		var move = DeviceMerge.KeyboardMove( raw );
		// Sample through interactions too, so held controls do not become fresh edges afterward.
		_controller?.Update( Mode, Board.State, IsInteracting, Goofy, WorldRotation.Forward, Time.Delta, pad );
		var keys = RiderKeys.Read( keyboardOnly: _controller is not null );
		_mountHeld = keys.MountHeld || _controller?.ToggleHeld == true;
		if ( IsInteracting )
		{
			// At the lip of a drop-in the mount control is held to wait and let go to go over; the stick pulled back backs out.
			_interaction.ObserveMountHeld( _mountHeld );
			if ( _interaction.DropsIn )
				_interaction.ObserveWalking( WalkingInput( pad, move ) );
			// Picking the board up, dropping or throwing it, only the hands are busy: the rider keeps walking.
			if ( _interaction.AllowsWalking )
				SetWalkInput( WalkingInput( pad, move ), false, keys.Sprint || _controller?.Sprint == true );
			return;
		}
		if ( Mode == SkateRiderMode.Riding && (keys.Bail || _controller?.Bail == true) ) Board.Bail();
		if ( Mode == SkateRiderMode.Riding && keys.Gesture ) NextGesture();
		var toggle = Mode == SkateRiderMode.Bailed ? _controller?.Recover == true : _controller?.Toggle == true;
		_mountSpent &= _mountHeld;
		if ( (keys.Mount || toggle) && !_mountSpent )
		{
			if ( Mode == SkateRiderMode.Bailed ) Recover();
			else if ( Mode == SkateRiderMode.Riding ) RequestDismount();
			else RequestMount();
		}
		// The pickup key drops a held board or calls the board back to the hand; the throw key tosses a held board down
		// ahead (calling it back when it is not held). On a controller, as the triggers do: the left one drops, the right
		// one throws, either calls the board back.
		var throwing = keys.Throw || _controller?.Throw == true;
		if ( keys.Pickup )
		{
			if ( Mode == SkateRiderMode.Carrying ) RequestDrop();
			else RequestPickUp();
		}
		else if ( Mode == SkateRiderMode.Carrying && throwing ) RequestThrow();
		else if ( Mode == SkateRiderMode.Carrying && _controller?.Drop == true ) RequestDrop();
		else if ( Mode == SkateRiderMode.Walking && (keys.Throw || _controller?.Retrieve == true) ) RequestPickUp();
		if ( Mode == SkateRiderMode.Riding ) Board.SetInput( WhileJumping( RidingInput( pad, move ) ) );
		else if ( Mode != SkateRiderMode.Bailed )
			SetWalkInput( WalkingInput( pad, move ), keys.Jump || _controller?.Jump == true, keys.Sprint || _controller?.Sprint == true );
	}

	// Board input from the keys and the controller (through its profile when there is one), merged.
	private SkateInput RidingInput( ControllerReading pad, Vector3 move )
	{
		var keys = _input.ReadKeys( Goofy, Time.Delta, move );
		var controller = _controller?.BoardInput( Goofy ) ?? LocalSkateInput.Pad( pad, Goofy );
		return DeviceMerge.Board( keys, DeviceMerge.SteeringKey( move ), controller, DeviceInputMerge.Activity( pad ) );
	}

	// Walking from the movement keys (relative to the view) or the left stick, whichever is in use.
	private Vector3 WalkingInput( ControllerReading pad, Vector3 move )
	{
		var controller = _controller?.Walking ?? ViewRelative( new Vector3( pad.LeftY, -pad.LeftX, 0f ) );
		return SkateUnits.ToEngine( DeviceInputMerge.Walk( SkateUnits.ToCore( ViewRelative( move ) ),
			DeviceMerge.MovementKey( move ), SkateUnits.ToCore( controller ), DeviceInputMerge.Activity( pad ) ) );
	}

	// Movement keys walk relative to where the scene camera looks (forward is away from the camera), not the world.
	private Vector3 ViewRelative( Vector3 move )
	{
		var camera = Scene.Camera;
		return camera.IsValid() ? Rotation.FromYaw( camera.WorldRotation.Yaw() ) * move : WorldRotation * move;
	}

	// Another player's rider far from the camera is posed less often: from LodNear metres every second frame, then every
	// fourth from twice that and every eighth from four times it, each pose covering the time since the last. A crowd of
	// riders then costs about what a few near ones do; the player's own rider is posed every frame.
	private const float LodNear = 12f;
	private int _lodFrame;
	private float _lodDelta;

	private bool PoseThisFrame( out float delta )
	{
		_lodDelta += Time.Delta;
		delta = _lodDelta;
		var every = 1;
		if ( IsProxy && Scene.Camera is { } camera )
		{
			var metres = camera.WorldPosition.Distance( WorldPosition ) / SkateUnits.InchesPerMetre;
			every = metres < LodNear ? 1 : metres < LodNear * 2f ? 2 : metres < LodNear * 4f ? 4 : 8;
		}
		if ( ++_lodFrame < every )
			return false;
		_lodFrame = 0;
		_lodDelta = 0f;
		return true;
	}

	private RiderActivity Activity()
	{
		if ( IsInteracting )
		{
			return _interaction.Action switch
			{
				// Once a mount from the hand or a drop-in has put the board down and the rider on it, it rides (the board goes
				// down as the clip has its board under the feet): shown as getting on, the body would be placed from the
				// rider's new place on the board without the deck's offset.
				RiderAction.Mount or RiderAction.DropIn => Mode == SkateRiderMode.Riding ? RiderActivity.Riding : RiderActivity.Mounting,
				RiderAction.Dismount => RiderActivity.Dismounting,
				RiderAction.PickUp => RiderActivity.PickingUp,
				RiderAction.Throw => RiderActivity.Throwing,
				_ => RiderActivity.Dropping
			};
		}
		return Mode switch
		{
			SkateRiderMode.Walking => RiderActivity.Walking,
			SkateRiderMode.Carrying => RiderActivity.Carrying,
			SkateRiderMode.Bailed => RiderActivity.Bailed,
			_ => RiderActivity.Riding
		};
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || _motor is null || !Board.IsValid() ) return;
		_animator.Ragdoll?.Step( Time.Delta );
		Configure();
		AutoRecover();
		if ( Mode == SkateRiderMode.Riding && !IsInteracting )
			StepHippyJump( Time.Delta );
		else
			_hippyJump.Clear();
		if ( IsInteracting )
		{
			StepInteraction( Time.Delta );
			if ( IsInteracting && _interaction.AllowsWalking )
				_motor.Walk( _walk, false, Time.Delta, _sprinting, turn: !TurnToReach( Time.Delta ),
					travel: _animator.LandingTravel );
		}
		else
		{
			// A landing goes on as its clip carries the body, over the feet it plants.
			_motor.Walk( _walk, _jump, Time.Delta, _sprinting, travel: _jump ? null : _animator.LandingTravel );
			_jump = false;
		}
		_animator.Smoother.Record( _motor.ShownTransform( Time.Delta ), Time.Now, Time.Delta );
	}

	protected override void OnDestroy() => _animator?.Destroy();
}
