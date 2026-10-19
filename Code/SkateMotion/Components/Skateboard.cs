using Sandbox;
using SkateMotion.Core.Grinding;
using SkateMotion.Engine;
using SkateMotion.Resources;
using SkateMotion.Systems;

namespace SkateMotion.Components;

/// <summary>Force-driven skating on the active scene's colliders. Supply controls with SetInput.</summary>
[Title( "Skateboard" ), Category( "Skate Motion" )]
public sealed class Skateboard : Component, IBoardDisplay
{
	/// <summary>Handling profile; null uses the estimated Balanced settings.</summary>
	[Property] public SkateProfile Profile { get; set; }

	/// <summary>Build an original deck, two trucks and four simulated wheels at startup.</summary>
	[Property] public bool GenerateGeometry { get; set; } = true;

	/// <summary>Native collision representation of the simulated deck.</summary>
	[RequireComponent] public Rigidbody Body { get; set; }

	/// <summary>Latest physics telemetry in SI units.</summary>
	public SkateState State { get; private set; }

	/// <summary>Wall-clock cost of the last fixed step on this machine, including scene queries, in milliseconds.</summary>
	public float StepMilliseconds { get; private set; }

	/// <summary>Allocates a diagnostic body snapshot on request; returns null before the simulation starts.</summary>
	public SkateTelemetry CaptureTelemetry() => _adapter?.CaptureTelemetry();

	/// <summary>Raised when an input gesture produces a pop impulse.</summary>
	public event Action<SkateTrick> TrickStarted;

	/// <summary>Raised when wheel support returns, with approach speed in metres per second.</summary>
	public event Action<float> Landed;

	/// <summary>Raised once when a manual request or unsafe collision disables rider assistance.</summary>
	public event Action Bailed;

	/// <summary>Raised when the board locks onto a grind rail or changes grind.</summary>
	public event Action<SkateGrind> GrindStarted;

	/// <summary>Raised when the board leaves a grind rail.</summary>
	public event Action GrindEnded;

	/// <summary>Raised when the deck strikes a surface, with approach speed in metres per second.</summary>
	public event Action<float> Collided;

	/// <summary>The rider ran into something side-on while upright: they step off and run it out instead of bailing.</summary>
	public event Action RunoutRequested;

	/// <summary>
	/// Whether the rider's pushing foot is on the ground, set each frame by an animated rider so a push adds speed
	/// while the foot is down; null without one.
	/// </summary>
	public bool? PushContact { get; set; }

	/// <summary>Connection whose input the host accepts; empty accepts only input set on the host itself.</summary>
	[Sync( SyncFlags.FromHost )] public Guid DriverId { get; set; }

	[Sync( SyncFlags.FromHost )] private float NetSpeed { get; set; }
	[Sync( SyncFlags.FromHost )] private int NetWheels { get; set; }
	[Sync( SyncFlags.FromHost )] private bool NetAirborne { get; set; }
	[Sync( SyncFlags.FromHost )] private bool NetBailed { get; set; }
	[Sync( SyncFlags.FromHost )] private SkateBalanceMode NetBalance { get; set; }
	[Sync( SyncFlags.FromHost )] private SkateGrind NetGrind { get; set; }

	private const float RailSearchInches = 60f;
	private const float CollisionEventSpeed = 1f;
	private readonly RailSegment[] _rails = new RailSegment[ Core.Animation.GrindEdgeQuery.Capacity ];

	private SkateInput _input;
	private bool _forwardInput;
	internal Func<SkateInput> InputOverride { get; set; }
	internal SkateInput CurrentInput => _input;
	internal event Action<SkateInput> Stepped;
	private SkateBody _adapter;
	private float _displayedAt = float.NegativeInfinity;

	// Seconds after the last display pose that the board still counts as animated.
	private const float AnimationTimeout = 0.5f;
	private BoardVisuals _visuals;
	private readonly StepSmoother _smooth = new();
	private bool _riderAttached = true;
	internal Vector3 ReleaseVelocity => _adapter?.ReleaseVelocity ?? Body.Velocity;
	internal Vector3 ReleaseAngularVelocity => _adapter?.ReleaseAngularVelocity ?? Body.AngularVelocity;
	internal Vector3 ReleasePosition => _adapter?.ReleasePosition ?? WorldPosition;

	/// <summary>Changes between loaded rider mass and a freely rolling unattended board.</summary>
	public void SetRiderAttached( bool attached )
	{
		_riderAttached = attached;
		_input = default;
		_adapter?.SetRiderAttached( attached );
	}

	/// <summary>Sets held controls until the next call; on a networked client they are sent to the host.</summary>
	public void SetInput( SkateInput input )
	{
		_input = input;
		_forwardInput = IsProxy;
	}

	/// <summary>Host only: lets one connection drive this board; null restores host-only input.</summary>
	public void SetDriver( Connection connection )
	{
		if ( IsProxy )
			return;
		DriverId = connection?.Id ?? Guid.Empty;
	}

	[Rpc.Host( NetFlags.Unreliable )]
	private void ReceiveInput( float steer, float push, float brake, float flickX, float flickY, float lean,
		bool slide, bool goofy, bool pump, SkateAirGrab grab, bool gesture, SkatePushFeet pushFeet, bool newPush, bool hippyJump )
	{
		if ( Rpc.Caller is null || DriverId == Guid.Empty || Rpc.Caller.Id != DriverId )
			return;
		_input = new SkateInput( steer, push, brake, flickX, flickY, lean, slide, goofy, pump, grab, gesture )
		{
			PushFeet = pushFeet,
			NewPush = newPush,
			HippyJump = hippyJump
		};
	}

	/// <summary>Sets push strength without changing the remaining controls.</summary>
	public void Push( float strength = 1f ) => _input = _input with { Push = strength };

	/// <summary>Sets brake strength without changing the remaining controls.</summary>
	public void Brake( float strength = 1f ) => _input = _input with { Brake = strength };

	/// <summary>Sets signed steering without changing the remaining controls.</summary>
	public void Steer( float amount ) => _input = _input with { Steer = amount };

	/// <summary>Switches the gesture interpretation between regular and goofy stance.</summary>
	public void SetGoofy( bool goofy ) => _input = _input with { Goofy = goofy };

	/// <summary>Adds a force (newtons, world space) to the deck for the next physics step, e.g. a tow.</summary>
	public void ApplyForce( Vector3 newtons )
	{
		if ( IsProxy || _adapter is null )
			return;
		_adapter.ExternalForce += SkateUnits.ToCore( newtons );
	}

	/// <summary>Why the board last bailed: "impact", "landing tilt", "grind balance" or "requested"; null before any bail.</summary>
	public string BailReason => _adapter?.BailReason;

	/// <summary>Disables rider assistance; native deck collision continues.</summary>
	public void Bail()
	{
		if ( IsProxy || State.IsBailed )
			return;
		_adapter?.Bail();
		State = State with { IsBailed = true, Balance = SkateBalanceMode.None };
		Bailed?.Invoke();
	}

	/// <summary>Clears transient controls and gesture state after an explicit game-managed respawn.</summary>
	public void ResetSimulation()
	{
		_input = default;
		_adapter?.Reset();
		State = default;
	}

	protected override void OnStart()
	{
		var settings = SkateProfiles.Read( Profile );
		var radius = Profile?.WheelRadius ?? 0.0285f;
		var rig = Components.Get<SkateboardRig>()?.Read( settings, radius )
			?? new BoardRig( GameObject, settings, radius );
		_visuals = new( GameObject, settings, radius, rig, GenerateGeometry );
		if ( GenerateGeometry )
		{
			if ( Components.Get<BoxCollider>() is null )
			{
				var collider = Components.Create<BoxCollider>();
				collider.Scale = new Vector3( 0.8f, 0.22f, 0.025f ) * SkateUnits.InchesPerMetre;
			}
		}
		_adapter = new( Body, Profile, rig );
		_adapter.SetRiderAttached( _riderAttached );
	}

	protected override void OnUpdate()
	{
		if ( _adapter is not null && Body.IsValid() )
			_visuals?.Update( _adapter.Bodies, Body.MotionEnabled, RenderTransform );
	}

	/// <summary>
	/// Shows the board at a pose other than its physical one, blended by weight (0 = physical), e.g. where a
	/// rider's trick animation holds it. Collisions and physics are unaffected.
	/// </summary>
	public void SetDisplayPose( Transform pose, float weight )
	{
		// A board whose tricks an animation shows takes no flip or spin of its own from the pop.
		_displayedAt = Time.Now;
		_visuals?.SetDisplay( pose, weight );
		if ( _adapter is not null && Body.IsValid() )
			_visuals?.Update( _adapter.Bodies, Body.MotionEnabled, RenderTransform );
	}

	/// <summary>
	/// Deck top and underside at a point in the frame of <see cref="DisplayedTransform"/>, from the deck model's
	/// triangles; null off the deck or without a deck model.
	/// </summary>
	public (float Top, float Bottom)? DeckSurface( float x, float y ) => _visuals?.DeckSurface( x, y );

	/// <summary>The deck model's box in the frame of <see cref="DisplayedTransform"/>, or null without a deck model.</summary>
	public BBox? DeckBounds => _visuals?.DeckBounds;

	/// <summary>The whole board (deck, trucks, wheels) in the frame of <see cref="DisplayedTransform"/>, or null without a deck model.</summary>
	public BBox? BoardBounds => _visuals?.BoardBounds;

	/// <summary>Each truck and wheel as a box in the frame of <see cref="DisplayedTransform"/>.</summary>
	public System.Collections.Generic.IReadOnlyList<BBox> PartBounds => _visuals?.PartBounds ?? [];

	/// <summary>Where the deck is shown this frame (the physical pose unless a display pose is blended in).</summary>
	public Transform DisplayedTransform => _visuals?.DisplayedDeck ?? RenderTransform;

	/// <summary>
	/// The physical pose as shown this frame: interpolated between the last two physics steps, so the board and
	/// anything placed on it move smoothly at any frame rate.
	/// </summary>
	public Transform RenderTransform => _smooth.At( WorldTransform, Time.Now );

	protected override void OnEnabled()
	{
		_smooth.Reset();
		_adapter?.Enable();
	}

	protected override void OnDestroy()
	{
		_adapter?.Disable();
		_visuals?.Destroy();
	}

	protected override void OnDisabled()
	{
		_input = default;
		_adapter?.Reset();
		_adapter?.Disable();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
		{
			UpdateProxy();
			return;
		}
		if ( _adapter is null || !Body.IsValid() || !Body.PhysicsBody.IsValid() )
			return;
		if ( InputOverride is not null )
			_input = InputOverride();

		var wasBailed = State.IsBailed;
		var previousGrind = State.Grind;
		var started = System.Diagnostics.Stopwatch.GetTimestamp();
		var railCount = Scene.GetSystem<GrindRails>()?.Collect( WorldPosition, RailSearchInches, _rails ) ?? 0;
		_adapter.AnimatedTricks = Time.Now - _displayedAt < AnimationTimeout;
		_adapter.PushContact = PushContact;
		var result = _adapter.Step( _riderAttached ? _input : default, Time.Delta, _rails.AsSpan( 0, railCount ) );
		StepMilliseconds = (float)System.Diagnostics.Stopwatch.GetElapsedTime( started ).TotalMilliseconds;
		State = new( _adapter.Speed,
			result.WheelCount, result.WheelCount == 0,
			result.Mode == Core.MotionMode.Bailed, result.ImpactSpeed )
		{
			Balance = result.Mode switch
			{
				Core.MotionMode.Manual => SkateBalanceMode.Manual,
				Core.MotionMode.NoseManual => SkateBalanceMode.NoseManual,
				_ => SkateBalanceMode.None
			},
			Grind = (SkateGrind)result.Grind,
			PowerSlide = result.PowerSlide,
			Reverting = result.Reverting,
			Fakie = _adapter.Fakie,
			BodyFlip = result.BodyFlip,
			WallRiding = result.WallRiding,
			Crouch = result.Crouch
		};
		if ( result.Trick != Core.TrickKind.None )
			TrickStarted?.Invoke( (SkateTrick)result.Trick );
		if ( result.Landed )
			Landed?.Invoke( result.ImpactSpeed );
		if ( State.IsBailed && !wasBailed )
			Bailed?.Invoke();
		if ( State.Grind != previousGrind )
		{
			if ( State.Grind == SkateGrind.None )
				GrindEnded?.Invoke();
			else
				GrindStarted?.Invoke( State.Grind );
		}
		if ( result.CollisionSpeed > CollisionEventSpeed )
			Collided?.Invoke( result.CollisionSpeed );
		if ( result.Runout && _riderAttached )
			RunoutRequested?.Invoke();
		Stepped?.Invoke( _input );
		PublishState();
		_smooth.Record( WorldTransform, Time.Now, Time.Delta );
	}

	private void PublishState()
	{
		NetSpeed = State.Speed;
		NetWheels = State.GroundedWheels;
		NetAirborne = State.IsAirborne;
		NetBailed = State.IsBailed;
		NetBalance = State.Balance;
		NetGrind = State.Grind;
	}

	// Clients never simulate: they forward their own input and read the host's state.
	private void UpdateProxy()
	{
		if ( _forwardInput )
			ReceiveInput( _input.Steer, _input.Push, _input.Brake, _input.FlickX, _input.FlickY, _input.Lean,
				_input.Slide, _input.Goofy, _input.Pump, _input.Grab, _input.Gesture, _input.PushFeet, _input.NewPush, _input.HippyJump );
		State = new( NetSpeed, NetWheels, NetAirborne, NetBailed, 0f ) { Balance = NetBalance, Grind = NetGrind };
	}
}
