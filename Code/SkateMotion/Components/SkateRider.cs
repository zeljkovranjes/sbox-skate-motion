using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Animation;
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

	/// <summary>Clip set in the game's data folder (FileSystem.Data); empty uses the built-in animation.</summary>
	[Property, Group( "Animation" )] public string AnimationClips { get; set; } = "";

	/// <summary>Animation graph JSON in the game's data folder that tags and plays the clip set.</summary>
	[Property, Group( "Animation" )] public string AnimationGraph { get; set; } = "";

	/// <summary>Preferred clip style tag (for example a signature set); empty plays the standard motions.</summary>
	[Property, Group( "Animation" )] public string AnimationStyle { get; set; } = "";

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

	/// <summary>True while the rider rides switch: the other foot forward, the board's tail leading.</summary>
	public bool RidingSwitch => _animator?.Switch ?? false;

	/// <summary>
	/// Lowest gap in inches between any part of a foot or shin and the board's deck this frame (negative: a foot is inside the
	/// deck; null: no foot over it). For checking foot placement on custom riders and boards.
	/// </summary>
	public float? FootDeckGap => _animator?.FootDeckGap;

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
	private bool _mountAfterDrop;
	private readonly LocalSkateInput _input = new();
	private readonly HippyJump _hippyJump = new();
	private RiderControllerInput _controller;
	private Vector3 _walk;
	private bool _jump;
	private int _gesture;
	internal GameObject VisualObject => _animator?.Visual;

	/// <summary>Starts a visible step onto a reachable board, placing a carried board down first.</summary>
	public bool RequestMount()
	{
		if ( Mode == SkateRiderMode.Carrying )
		{
			if ( !RequestDrop() ) return false;
			_mountAfterDrop = true;
			return true;
		}
		return BeginInteraction( RiderAction.Mount );
	}

	/// <summary>Starts a collision-constrained step off the board.</summary>
	public bool RequestDismount()
	{
		if ( !BeginInteraction( RiderAction.Dismount ) ) return false;
		Board.SetRiderAttached( false );
		return true;
	}

	/// <summary>Reaches for a nearby board before lifting it into the carrying pose.</summary>
	public bool RequestPickUp() => BeginInteraction( RiderAction.PickUp );

	/// <summary>Moves the held board into release position before resuming its physical simulation.</summary>
	public bool RequestDrop() => BeginInteraction( RiderAction.Drop );

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
			Board.Enabled = false;
			var captured = _interaction.UsesSourcePickup ? _motor.BeginRetrieval() : _motor.PickUp( false );
			if ( !captured )
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
		var reached = _interaction.AtDestination;
		if ( reached && _interaction.UsesSourcePickup )
			_motor.CompleteRetrieval();
		_interaction.End();
		LastInteractionSucceeded = reached && (action switch
		{
			RiderAction.Mount => Mount(),
			RiderAction.Drop => Drop(),
			_ => true
		});
		if ( _mountAfterDrop )
		{
			_mountAfterDrop = false;
			if ( LastInteractionSucceeded ) RequestMount();
		}
	}

	/// <summary>Supplies world-space walking direction, a one-shot jump request, and whether to sprint while running.</summary>
	public void SetWalkInput( Vector3 direction, bool jump = false, bool sprint = false )
	{
		_walk = direction;
		_jump |= jump;
		_sprinting = sprint;
	}

	private bool _sprinting;

	/// <summary>Steps beside the board if there is room, preserving the rider's momentum.</summary>
	public bool Dismount()
	{
		if ( IsProxy || _motor is null || !_motor.Dismount() ) return false;
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
		Board.SetRiderAttached( true );
		Board.ResetSimulation();
		return true;
	}

	/// <summary>Stands beside the settled ragdoll when there is room for the walking controller.</summary>
	public bool Recover()
	{
		if ( IsProxy || _motor is null || !_motor.Recover( _animator.Ragdoll ) ) return false;
		_animator.Recover( GameObject );
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
		RequestDismount();
	}

	// Running into something side-on while upright: step off and run it out.
	private void OnRunout()
	{
		if ( IsProxy || Mode != SkateRiderMode.Riding ) return;
		RequestDismount();
	}

	// Deck impacts that do not bail knock the rider; the clip graph plays a short bump.
	private void OnBoardCollided( float speed )
	{
		if ( speed < 1f || Mode != SkateRiderMode.Riding || Board.State.IsBailed ) return;
		PlayAnimation( new Dictionary<string, string> { [ "family" ] = "bump", [ "action" ] = "frontknock", [ "phase" ] = "once" }, 0.45f );
	}

	private void OnBoardBailed()
	{
		if ( IsProxy || _motor is null || Mode != SkateRiderMode.Riding ) return;
		_motor.FollowBoard();
		_animator.Bail( Board.ReleaseVelocity, Board.ReleaseAngularVelocity, Board.ReleasePosition,
			(Board.Profile?.Mass ?? 80f) - 3f, Board.Profile?.Gravity ?? 9.81f );
		_motor.Bail();
		Board.SetRiderAttached( false );
	}

	protected override void OnEnabled()
	{
		if ( _motor is not null && Board.IsValid() ) Subscribe();
	}

	protected override void OnDisabled()
	{
		if ( !Board.IsValid() ) return;
		Board.Bailed -= OnBoardBailed;
		Board.Collided -= OnBoardCollided;
		Board.RunoutRequested -= OnRunout;
		Board.TrickStarted -= _animator.OnTrick;
		Board.Landed -= _animator.OnLanded;
	}

	private void Subscribe()
	{
		Board.Bailed += OnBoardBailed;
		Board.Collided += OnBoardCollided;
		Board.RunoutRequested += OnRunout;
		Board.TrickStarted += _animator.OnTrick;
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
		_motor.FollowBoard();
		Subscribe();
		// The host simulates the board; the rider's owner drives it with forwarded input.
		if ( Networking.IsHost && Network.Owner is not null )
			Board.SetDriver( Network.Owner );
	}

	protected override void OnUpdate()
	{
		if ( _motor is null || !Board.IsValid() ) return;
		if ( Board.State.IsBailed && Mode == SkateRiderMode.Riding ) OnBoardBailed();
		if ( Mode == SkateRiderMode.Bailed ) _motor.FollowRagdoll( _animator.Ragdoll.Position );
		_motor.FollowBoard();
		if ( !_animator.UpdateClips( Activity(), Board.State, Board.CurrentInput, _motor.Velocity, Goofy, AnimationStyle, Time.Delta,
			moveAmount: _walk.WithZ( 0f ).Length, holdingBoard: Mode == SkateRiderMode.Carrying, hippy: _hippyJump ) )
		{
			_animator.Update( Mode, Board.State, _motor.Velocity, _motor.Grounded, Goofy );
			if ( IsInteracting ) _animator.UpdateInteraction( _interaction, Goofy );
		}
		_motor.UpdateCarryPose( _animator.CarriedBoardPose, !IsInteracting );
		if ( IsInteracting && !IsProxy ) _interaction.ShowPickup();
		CheckHeadFirst();
		Board.PushContact = Mode == SkateRiderMode.Riding ? _animator.PushContact : null;
		_interaction.ObserveRetrievalRequest( _animator.RetrieveRequested );
		if ( IsProxy || !ReadLocalInput ) return;
		// Sample through interactions too, so held controls do not become fresh edges afterward.
		_controller?.Update( Mode, Board.State, IsInteracting, Goofy, WorldRotation.Forward, Time.Delta );
		if ( Input.UsingController )
		{
			ApplyController();
			return;
		}
		if ( IsInteracting ) return;
		if ( Mode == SkateRiderMode.Riding && Input.Pressed( "SkateBail" ) ) Board.Bail();
		if ( Mode == SkateRiderMode.Riding && Input.Pressed( "SkateGesture" ) ) NextGesture();
		if ( Input.Pressed( "SkateMount" ) )
		{
			if ( Mode == SkateRiderMode.Bailed ) Recover();
			else if ( Mode == SkateRiderMode.Riding ) RequestDismount();
			else RequestMount();
		}
		if ( Input.Pressed( "SkatePickup" ) )
		{
			if ( Mode == SkateRiderMode.Carrying ) RequestDrop();
			else RequestPickUp();
		}
		if ( Mode == SkateRiderMode.Riding ) Board.SetInput( WhileJumping( _input.Read( Goofy, Time.Delta ) ) );
		else if ( Mode != SkateRiderMode.Bailed ) SetWalkInput( ViewRelative( Input.AnalogMove ), Input.Pressed( "Jump" ), Input.Down( "SkateSprint" ) );
	}

	// Movement keys walk relative to where the scene camera looks (forward is away from the camera), not the world.
	private Vector3 ViewRelative( Vector3 move )
	{
		var camera = Scene.Camera;
		return camera.IsValid() ? Rotation.FromYaw( camera.WorldRotation.Yaw() ) * move : WorldRotation * move;
	}

	private void ApplyController()
	{
		if ( _controller is null )
		{
			Board.SetInput( default );
			SetWalkInput( Vector3.Zero );
			return;
		}
		if ( IsInteracting )
		{
			Board.SetInput( default );
			return;
		}
		if ( Mode == SkateRiderMode.Bailed )
		{
			if ( _controller.Recover ) Recover();
			return;
		}
		if ( Mode == SkateRiderMode.Riding )
		{
			Board.SetInput( WhileJumping( _controller.BoardInput( Goofy ) ) );
			if ( _controller.Bail ) Board.Bail();
			else if ( _controller.Toggle ) RequestDismount();
			return;
		}
		SetWalkInput( _controller.Walking, _controller.Jump, _controller.Sprint );
		if ( _controller.Toggle ) RequestMount();
		else if ( Mode == SkateRiderMode.Carrying && _controller.Drop ) RequestDrop();
		else if ( Mode == SkateRiderMode.Walking && _controller.Retrieve ) RequestPickUp();
	}

	private RiderActivity Activity()
	{
		if ( IsInteracting )
		{
			return _interaction.Action switch
			{
				RiderAction.Mount => RiderActivity.Mounting,
				RiderAction.Dismount => RiderActivity.Dismounting,
				RiderAction.PickUp => RiderActivity.PickingUp,
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
		AutoRecover();
		if ( Mode == SkateRiderMode.Riding && !IsInteracting )
			StepHippyJump( Time.Delta );
		else
			_hippyJump.Reset();
		if ( IsInteracting )
			StepInteraction( Time.Delta );
		else
		{
			_motor.Walk( _walk, _jump, Time.Delta, _sprinting );
			_jump = false;
		}
		_animator.Smoother.Record( WorldTransform, Time.Now, Time.Delta );
	}

	protected override void OnDestroy() => _animator?.Destroy();
}
