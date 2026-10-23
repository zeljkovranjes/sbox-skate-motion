using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Networking;
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

	/// <summary>
	/// Speed into the ground (metres a second) above which a landing bails, in place of the profile's; zero keeps the
	/// profile's. A flat landing off a 3 m drop comes down at about 7.7 m/s, off 6 m about 10.8 and off 10 m about 14; a
	/// landing tipped too far still bails at any speed.
	/// </summary>
	[Property, Range( 0f, 30f )] public float HardestLanding { get; set; }

	/// <summary>Build an original deck, two trucks and four simulated wheels at startup.</summary>
	[Property] public bool GenerateGeometry { get; set; } = true;

	/// <summary>
	/// Any skateboard model, shown in place of the generated board: its deck, trucks and four wheels are found from its
	/// own geometry (or its bones), so its trucks steer, its deck leans into turns and its wheels spin, and the board
	/// rides on the model's own wheelbase, track, wheel size and height. Changing it rebuilds the board.
	/// </summary>
	[Property, Title( "Board Model" )]
	public Model BoardModel
	{
		get => _boardModel;
		set
		{
			if ( _boardModel == value )
				return;
			_boardModel = value;
			if ( _adapter is not null )
				Rebuild();
		}
	}

	/// <summary>
	/// The board model's lower levels of detail, nearest first, each a model of its own (a model's own lower levels cannot
	/// be read from code). Each is taken apart along the parts found on <see cref="BoardModel"/>, so its wheels spin and its
	/// trucks steer as the first level's do, and switches in where <see cref="BoardModel"/>'s own levels do (the switch
	/// thresholds of its LOD groups). Changing them rebuilds the board.
	/// </summary>
	[Property, Title( "Board Model Levels" )]
	public List<Model> BoardModelLevels
	{
		get => _boardModelLevels;
		set
		{
			if ( _boardModelLevels == value )
				return;
			_boardModelLevels = value ?? new List<Model>();
			if ( _adapter is not null )
				Rebuild();
		}
	}

	private Model _boardModel;
	private List<Model> _boardModelLevels = new();
	private BoxCollider _deckCollider;

	/// <summary>The board's size as simulated, and where its shown parts come from.</summary>
	public SkateBoardDimensions Dimensions { get; private set; }

	/// <summary>How far the deck dips into the current turn on its trucks, radians (positive: its left side down).</summary>
	public float Lean => _visuals?.Roll ?? 0f;

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

	/// <summary>True when the trick last reported by <see cref="TrickStarted"/> was popped off the nose: its nollie
	/// version.</summary>
	public bool LastTrickFromNose { get; private set; }

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

	/// <summary>
	/// Whether the rider's animation holds the board dark side up (a dark catch, a dark landing, a darkslide), set each
	/// frame by an animated rider: on the ground the board then skids on its grip instead of rolling and is not pushed,
	/// and a bail lets it go upside down, as it was shown.
	/// </summary>
	public bool DarkSideUp { get; set; }

	/// <summary>
	/// Whether the rider's animation is planting a foot for a foot-plant trick (a boneless, a fastplant, a no comply) and
	/// has not yet sprung off it, set each frame by an animated rider: the trick's pop waits for it, so the board leaves
	/// the ground as the rider springs off the planted foot. Without it the pop comes a moment after the trick is asked for.
	/// </summary>
	public bool FootPlanting { get; set; }

	/// <summary>
	/// Connection whose forwarded input the board accepts, chosen by the host; empty accepts only input set where the
	/// board is simulated. The board is simulated by its network owner (the host when it has none): give a rider
	/// ownership and its own client simulates the board it rides, with no round trip, and the host pays for no board but
	/// its own.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public Guid DriverId { get; set; }

	// The simulated state and the controls it is given, written by whoever simulates the board: every other player shows
	// the board, and its rider's tricks, grabs, grinds and flips, from them (NetBoard).
	[Sync] private float NetSpeed { get; set; }
	[Sync] private int NetWheels { get; set; }
	[Sync] private float NetLandingSpeed { get; set; }
	[Sync] private float NetCrouch { get; set; }
	[Sync] private int NetPowerSlide { get; set; }
	[Sync] private int NetBodyFlip { get; set; }
	[Sync] private SkateBalanceMode NetBalance { get; set; }
	[Sync] private SkateGrind NetGrind { get; set; }
	[Sync] private SkateGrind NetNamedGrind { get; set; }
	[Sync] private int NetStateFlags { get; set; }
	[Sync] private float NetSteer { get; set; }
	[Sync] private float NetPush { get; set; }
	[Sync] private float NetBrake { get; set; }
	[Sync] private float NetFlickX { get; set; }
	[Sync] private float NetFlickY { get; set; }
	[Sync] private float NetLean { get; set; }
	[Sync] private SkateAirGrab NetGrab { get; set; }
	[Sync] private SkatePushFeet NetPushFeet { get; set; }
	[Sync] private SkatePushFeet NetFeetOff { get; set; }
	[Sync] private int NetInputFlags { get; set; }

	private const float RailSearchInches = 60f;
	private const float CollisionEventSpeed = 1f;
	private readonly RailSegment[] _rails = new RailSegment[ Core.Animation.GrindEdgeQuery.Capacity ];
	private readonly System.Collections.Generic.List<RailSolid> _solids = new();

	private SkateInput _input;
	private bool _forwardInput;
	private bool _simulated;
	internal Func<SkateInput> InputOverride { get; set; }
	// The controls the board's owner gives it, as shared, where the board is shown rather than simulated.
	private SkateInput _sharedInput;
	internal SkateInput CurrentInput => IsProxy && !_forwardInput ? _sharedInput : _input;
	internal event Action<SkateInput> Stepped;
	private SkateBody _adapter;
	private float _displayedAt = float.NegativeInfinity;

	// Seconds after the last display pose that the board still counts as animated.
	private const float AnimationTimeout = 0.5f;

	// Seconds a display pose may go without being renewed before it is let go, the seconds it is let go over, and the
	// seconds a bail lets it go over (from the bail itself, while the shown and physical boards are still close).
	private const float DisplayStale = 0.1f;
	private const float DisplayRelease = 0.2f;
	private const float BailRelease = 0.12f;
	private float _release = DisplayRelease;
	private BoardVisuals _visuals;
	private readonly StepSmoother _smooth = new();
	private bool _riderAttached = true;
	internal Vector3 ReleaseVelocity => IsProxy ? NetReleaseVelocity : _adapter?.ReleaseVelocity ?? Body.Velocity;
	internal Vector3 ReleaseAngularVelocity => IsProxy ? NetReleaseSpin : _adapter?.ReleaseAngularVelocity ?? Body.AngularVelocity;
	internal Vector3 ReleasePosition => IsProxy ? NetReleasePosition : _adapter?.ReleasePosition ?? WorldPosition;

	// How the board was moving as the rider let go, for a rider driving it from another client to fall with: sent with
	// the bail itself.
	[Sync] private Vector3 NetReleaseVelocity { get; set; }
	[Sync] private Vector3 NetReleaseSpin { get; set; }
	[Sync] private Vector3 NetReleasePosition { get; set; }

	private void ShareRelease()
	{
		NetReleaseVelocity = ReleaseVelocity;
		NetReleaseSpin = ReleaseAngularVelocity;
		NetReleasePosition = ReleasePosition;
	}

	/// <summary>Changes between loaded rider mass and a freely rolling unattended board.</summary>
	public void SetRiderAttached( bool attached )
	{
		_riderAttached = attached;
		Wake();
		_input = default;
		var before = WorldRotation;
		_adapter?.SetRiderAttached( attached );
		// Stepping off a board held dark side up turns it over at once (it is left as it was shown): the shown board is
		// placed again now, from the turned board, rather than its trucks and wheels turning over with it until next frame.
		if ( _adapter is not null && Body.IsValid() && Vector3.Dot( before.Up, WorldRotation.Up ) < 0f )
			_visuals?.Update( _adapter.Bodies, Simulating, RenderTransform, _solids, State.Grind != SkateGrind.None );
	}

	/// <summary>Sets held controls until the next call; where the board is not simulated they are sent to its owner. While
	/// an input override plays (a scripted timeline), it stands in for them: taken only in the board's steps, it left the
	/// rider's animation reading the rider's own controls on frames without a step, so a held grab let go and grabbed
	/// again in the air.</summary>
	public void SetInput( SkateInput input )
	{
		_input = InputOverride is { } scripted ? scripted() : input;
		_forwardInput = IsProxy;
	}

	/// <summary>Host only: lets one connection drive this board; null accepts only input set where it is simulated.</summary>
	public void SetDriver( Connection connection )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;
		DriverId = connection?.Id ?? Guid.Empty;
	}

	[Rpc.Owner( NetFlags.Unreliable )]
	private void ReceiveInput( float steer, float push, float brake, float flickX, float flickY, float lean,
		bool slide, bool goofy, bool pump, SkateAirGrab grab, bool gesture, SkatePushFeet pushFeet, bool newPush, bool hippyJump,
		bool catching, SkatePushFeet feetOff, bool handPlant )
	{
		if ( Rpc.Caller is null || DriverId == Guid.Empty || Rpc.Caller.Id != DriverId )
			return;
		_input = new SkateInput( steer, push, brake, flickX, flickY, lean, slide, goofy, pump, grab, gesture )
		{
			PushFeet = pushFeet,
			NewPush = newPush,
			HippyJump = hippyJump,
			Catch = catching,
			FeetOff = feetOff,
			HandPlant = handPlant
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

	// Where the hand of the board's last plant goes, in world space: the top of the wall the board left, found once per
	// lip by raising a level ray into the wall from its highest wheel contact until it stops hitting.
	internal Vector3 HandPlantLip
	{
		get
		{
			if ( _adapter is null )
				return WorldPosition;
			var lip = SkateUnits.Inches( _adapter.HandPlantLip );
			if ( _plantLipFrom != lip )
			{
				_plantLipFrom = lip;
				_plantLip = WallTop( lip, -SkateUnits.ToEngine( _adapter.HandPlantOutward ) );
			}
			return _plantLip;
		}
	}

	private Vector3? _plantLipFrom;
	private Vector3 _plantLip;

	// How far (in) above a wheel's last touch the wall's top is looked for.
	private const float WallSearch = 16f;

	private Vector3 WallTop( Vector3 point, Vector3 inward )
	{
		if ( inward.IsNearZeroLength || !WallAt( point, inward ) || WallAt( point + Vector3.Up * WallSearch, inward ) )
			return point;
		var low = 0f;
		var high = WallSearch;
		for ( var i = 0; i < 6; i++ )
		{
			var middle = (low + high) * 0.5f;
			if ( WallAt( point + Vector3.Up * middle, inward ) )
				low = middle;
			else
				high = middle;
		}
		return point + Vector3.Up * low;
	}

	private bool WallAt( Vector3 point, Vector3 inward ) =>
		Scene.Trace.Ray( point - inward * 4f, point + inward * 4f ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( SkateTags.Rider ).IgnoreDynamic().Run().Hit;

	/// <summary>Disables rider assistance; native deck collision continues.</summary>
	public void Bail()
	{
		if ( IsProxy || State.IsBailed )
			return;
		_adapter?.Bail();
		State = State with { IsBailed = true, Balance = SkateBalanceMode.None };
		ShareRelease();
		ReleaseDisplay();
		Bailed?.Invoke();
	}

	// The rider let go of the board (a bail): whatever display pose its animation held the shown board at is handed back to
	// the physical board from now, over BailRelease, rather than staying where the animation last had it (in the air).
	private void ReleaseDisplay()
	{
		_displayedAt = float.NegativeInfinity;
		_release = BailRelease;
	}

	/// <summary>Clears transient controls and gesture state after an explicit game-managed respawn.</summary>
	public void ResetSimulation()
	{
		_input = default;
		_adapter?.Reset();
		State = default;
		Wake();
	}

	/// <summary>
	/// Whether the board is asleep: no one rides it and it has lain still, so its step is skipped until it is ridden,
	/// moved, set going or reset. Most boards in a busy game are lying about; asleep they cost nothing.
	/// </summary>
	public bool Asleep { get; private set; }

	// A board no one rides counts as still when it is within SleepDrift inches and SleepTurn degrees of where it was
	// SleepAfter seconds before; moved more than that while asleep (picked up, knocked, placed), it wakes from there.
	private const float SleepAfter = 1f;
	private const float SleepDrift = 0.5f;
	private const float SleepTurn = 1f;
	private Transform? _stillFrom;
	private float _stillFor;

	private void Wake()
	{
		Asleep = false;
		_stillFrom = null;
		_stillFor = 0f;
	}

	// Whether this step is skipped: asleep, or falling asleep now.
	private bool Sleeping()
	{
		if ( _riderAttached )
		{
			if ( Asleep ) Wake();
			_stillFrom = null;
			return false;
		}
		var moved = _stillFrom is { } from && (WorldPosition.Distance( from.Position ) > SleepDrift
			|| Rotation.Difference( WorldRotation, from.Rotation ).Angle() > SleepTurn);
		if ( Asleep )
		{
			if ( !moved )
				return true;
			// Moved from outside while asleep: the simulation goes on from where and how it was left.
			Wake();
			_adapter.Reset();
			return false;
		}
		if ( _stillFrom is null || moved )
		{
			_stillFrom = WorldTransform;
			_stillFor = 0f;
			return false;
		}
		_stillFor += Time.Delta;
		if ( _stillFor < SleepAfter )
			return false;
		Asleep = true;
		Body.Velocity = Vector3.Zero;
		Body.AngularVelocity = Vector3.Zero;
		return true;
	}

	protected override void OnStart()
	{
		// Spawned from a package loaded into a game already running, the board's scene has none of its systems yet.
		SceneSystems.Ensure( Scene );
		Build();
	}

	private void Build()
	{
		var settings = SkateProfiles.Read( Profile );
		if ( HardestLanding > 0f )
			settings = settings with { BailSpeed = HardestLanding };
		var radius = Profile?.WheelRadius ?? 0.0285f;
		string problem = null;
		var parts = BoardModel is null ? null : BoardModelParts.Of( BoardModel, BoardModelLevels, out problem );
		if ( BoardModel is not null && parts is null )
			Log.Warning( $"Skateboard {GameObject.Name}: the parts of {BoardModel.ResourceName} could not be found (it {problem}), so it is "
				+ "shown whole, without steering trucks or spinning wheels. Separate meshes for its wheels, or bones named for "
				+ "its wheels and trucks (see Skateboard Rig), let them be found." );
		var whole = parts is null ? BoardModel : null;
		var rig = Components.Get<SkateboardRig>()?.Read( settings, radius, parts, whole )
			?? new BoardRig( GameObject, settings, radius, parts: parts ) { DeckModel = whole };
		var generate = GenerateGeometry && BoardModel is null;
		_visuals = new( GameObject, rig.Settings, rig.WheelRadius, rig, generate );
		Dimensions = Measure( rig, generate );
		if ( (generate || parts is not null) && (Components.Get<BoxCollider>() is null || _deckCollider.IsValid()) )
		{
			_deckCollider ??= Components.Create<BoxCollider>();
			// The deck's size its simulation collides at (the generated deck's, or the board model's measured one).
			_deckCollider.Scale = new Vector3( rig.Settings.DeckCollisionLength, rig.Settings.DeckWidth, rig.Settings.DeckThickness )
				* SkateUnits.InchesPerMetre;
		}
		_adapter = new( Body, rig );
		_adapter.SetRiderAttached( _riderAttached );
	}

	// A board model changed while running: the shown board and its simulation are built again where the board is.
	private void Rebuild()
	{
		_visuals?.Destroy();
		_adapter?.Disable();
		_smooth.Reset();
		Build();
	}

	private SkateBoardDimensions Measure( BoardRig rig, bool generated )
	{
		var settings = rig.Settings;
		var layout = rig.Parts?.Layout;
		var bones = rig.PartBones;
		var source = layout is not null
			? bones is not null && bones[ (int)Core.BoardModel.BoardPart.FrontLeftWheel ] >= 0
				&& bones[ (int)Core.BoardModel.BoardPart.RearRightWheel ] >= 0 ? SkateBoardSource.Bones
				: layout.Source == Core.BoardModel.BoardPartSource.Pieces ? SkateBoardSource.Pieces : SkateBoardSource.Clusters
			: generated ? SkateBoardSource.Generated
			: BoardModel is not null ? SkateBoardSource.Whole : SkateBoardSource.Rig;
		// The generated deck's size, or a custom deck model's box.
		var deck = layout is null && !generated ? _visuals?.DeckBounds : null;
		var length = layout?.Length ?? (deck?.Size.x ?? BoardGeometry.DeckLength * SkateUnits.InchesPerMetre) / SkateUnits.InchesPerMetre;
		var width = layout?.Width ?? (deck?.Size.y ?? BoardGeometry.DeckWidth * SkateUnits.InchesPerMetre) / SkateUnits.InchesPerMetre;
		var thickness = layout?.Thickness ?? (deck is null ? BoardGeometry.DeckThickness : MathF.Min( deck.Value.Size.z / SkateUnits.InchesPerMetre, 0.03f ));
		return new SkateBoardDimensions( length, width, thickness, settings.Wheelbase, settings.Track, rig.WheelRadius,
			settings.RideHeight, source );
	}

	// What the shown board's lean and wheels follow this frame: the controls the board was given and what it is doing.
	private BoardMotion Motion()
	{
		var state = State;
		var touching = state.IsAirborne || state.Grind != SkateGrind.None ? 0
			: state.Balance == SkateBalanceMode.Manual ? 0b1100
			: state.Balance == SkateBalanceMode.NoseManual ? 0b0011
			: 0b1111;
		var lean = !_riderAttached || state.IsBailed || DarkSideUp || state.Grind != SkateGrind.None || state.PowerSlide != 0
			|| state.Reverting || state.WallRiding || state.HandPlanting ? Core.Vehicle.DeckLeanMode.Level
			: state.IsAirborne ? Core.Vehicle.DeckLeanMode.Airborne
			: state.Balance != SkateBalanceMode.None ? Core.Vehicle.DeckLeanMode.Manual
			: Core.Vehicle.DeckLeanMode.Carving;
		var input = _riderAttached ? _input : default;
		return new BoardMotion( input.Steer, state.Speed, input.Push > 0f, lean, touching );
	}

	protected override void OnUpdate()
	{
		// The shown board rests on any rail it reaches, wherever an animation holds it.
		SceneSystems.Get<GrindRails>( Scene )?.CollectSolids( WorldPosition, RailSearchInches, _solids );
		if ( _visuals is not null )
		{
			_visuals.Grind = State.Grind;
			_visuals.GrindOnLedgeTop = State.GrindOnLedgeTop;
		}
		// A display pose nobody renews (an animation that stopped with the clip's board shown) is handed back to the physical
		// board, rather than holding the shown board where the clip last had it; a bail hands it back at once (ReleaseDisplay).
		if ( Time.Now - _displayedAt > DisplayStale )
			_visuals?.FadeDisplay( Time.Delta / _release );
		if ( _adapter is not null && Body.IsValid() )
		{
			_visuals?.Animate( _adapter.Bodies, Motion(), Time.Delta );
			_visuals?.Update( _adapter.Bodies, Simulating, RenderTransform, _solids, State.Grind != SkateGrind.None );
		}
	}

	/// <summary>
	/// Turns a board lying upside down back over for a rider getting on it: physics rights it at once, raised onto its
	/// wheels, and the shown board rolls over from where it lay instead of flipping in a frame.
	/// </summary>
	internal void TurnUpright()
	{
		if ( _adapter is null || IsProxy || WorldRotation.Up.z >= 0f )
			return;
		var before = RenderTransform;
		_adapter.TurnUpright();
		_smooth.Reset();
		_visuals?.BeginTurnOver( before, WorldTransform );
		if ( Body.IsValid() )
			_visuals?.Update( _adapter.Bodies, Simulating, RenderTransform, _solids, State.Grind != SkateGrind.None );
	}

	/// <summary>
	/// Shows the board at a pose other than its physical one, blended by weight (0 = physical), e.g. where a
	/// rider's trick animation holds it. Collisions and physics are unaffected.
	/// </summary>
	public void SetDisplayPose( Transform pose, float weight )
	{
		// A board whose tricks an animation shows takes no flip or spin of its own from the pop.
		_displayedAt = Time.Now;
		_release = DisplayRelease;
		_visuals?.SetDisplay( pose, weight );
		if ( _adapter is not null && Body.IsValid() )
			_visuals?.Update( _adapter.Bodies, Simulating, RenderTransform, _solids, State.Grind != SkateGrind.None );
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

	/// <summary>
	/// Where the deck is shown this frame (the physical pose unless a display pose is blended in); while the board is
	/// disabled (carried), where its deck is drawn as it is carried.
	/// </summary>
	public Transform DisplayedTransform => Enabled ? _visuals?.DisplayedDeck ?? RenderTransform : _visuals?.DrawnDeck ?? WorldTransform;

	Vector3 IBoardDisplay.RailLift => _visuals?.Lift ?? Vector3.Zero;

	void IBoardDisplay.HoldSlidePitch( bool hold )
	{
		if ( _visuals is not null )
			_visuals.HoldSlide = hold;
	}

	Transform IBoardDisplay.RiddenTransform => _visuals?.Leaned( RenderTransform ) ?? RenderTransform;

	System.Collections.Generic.IReadOnlyList<RailSolid> IBoardDisplay.RailSolids => _solids;

	// True while this board's own simulation moves its bodies: not while a rider carries it (the board is disabled), nor
	// on a client, which only reads the host's state. Its trucks and wheels are then shown where physics holds them.
	private bool Simulating => Active && !IsProxy;

	/// <summary>
	/// The physical pose as shown this frame: interpolated between the last two physics steps, so the board and
	/// anything placed on it move smoothly at any frame rate.
	/// </summary>
	public Transform RenderTransform => _smooth.At( WorldTransform, Time.Now );

	/// <summary>
	/// The board was just put down where it is by the rider carrying it (getting on with it, or dropping in): it is shown
	/// going on as <paramref name="rider"/> showed the rider moving, carried onto the board. Enabled again with no steps
	/// recorded it stood a physics step behind and then jumped on, and the rider on it with it.
	/// </summary>
	internal void ShowPutDown( StepSmoother rider ) => _smooth.Follow( rider, WorldTransform );

	/// <summary>
	/// As <see cref="ShowPutDown(StepSmoother)"/>, the board going on over <see cref="PutDownEase"/> from
	/// <paramref name="shown"/>, where it was shown a moment ago (tipped over a drop-in's lip by the rider's clip), to where
	/// it went down, rather than appearing there.
	/// </summary>
	internal void ShowPutDown( StepSmoother rider, Transform shown )
	{
		_smooth.Follow( rider, WorldTransform );
		_smooth.EaseFrom( shown, WorldTransform, Time.Now, PutDownEase );
	}

	// Seconds a board put down from where it was shown takes to go on to where it went down.
	private const float PutDownEase = 0.25f;

	/// <summary>
	/// The rider at <paramref name="placed"/> is stepping off: <paramref name="rider"/> takes over the board's recorded
	/// steps, moved onto the rider, so the rider is shown going on as the board it rode was shown.
	/// </summary>
	internal void HandStepsTo( StepSmoother rider, Transform placed ) => rider.Follow( _smooth, placed );

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
			_simulated = false;
			UpdateProxy();
			return;
		}
		if ( _adapter is null || !Body.IsValid() || !Body.PhysicsBody.IsValid() )
			return;
		// Taken over (this peer became its owner): the simulation goes on from where the board was last seen.
		if ( !_simulated )
		{
			_simulated = true;
			_adapter.Reset();
		}
		if ( Sleeping() )
			return;
		if ( InputOverride is not null )
			_input = InputOverride();

		var wasBailed = State.IsBailed;
		var previousGrind = State.Grind;
		var wasPlanting = State.HandPlanting;
		var shownBefore = RenderTransform;
		var started = System.Diagnostics.Stopwatch.GetTimestamp();
		var railCount = SceneSystems.Get<GrindRails>( Scene )?.Collect( WorldPosition, RailSearchInches, _rails ) ?? 0;
		_adapter.AnimatedTricks = Time.Now - _displayedAt < AnimationTimeout;
		_adapter.PushContact = PushContact;
		_adapter.DarkSideUp = DarkSideUp;
		_adapter.FootPlanting = FootPlanting;
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
			HandPlanting = result.HandPlanted,
			Crouch = result.Crouch,
			GrindRailOnRight = _adapter.GrindRailOnRight,
			GrindNoseRight = _adapter.GrindNoseRight,
			GrindBackward = result.GrindShape.Backward,
			GrindTwisted = result.GrindShape.Twisted,
			GrindLow = result.GrindShape.Low,
			GrindOnLedgeTop = result.GrindShape.OnLedgeTop,
			NamedGrind = (SkateGrind)result.NamedGrind,
			NamedBackward = result.NamedGrindShape.Backward,
			NamedTwisted = result.NamedGrindShape.Twisted,
			NamedLow = result.NamedGrindShape.Low,
			NamedOnLedgeTop = result.NamedGrindShape.OnLedgeTop
		};
		if ( result.Trick != Core.TrickKind.None )
		{
			LastTrickFromNose = result.TrickNose;
			TrickStarted?.Invoke( (SkateTrick)result.Trick );
			ShareTrick( (SkateTrick)result.Trick, result.TrickNose );
		}
		if ( result.Landed )
		{
			Landed?.Invoke( result.ImpactSpeed );
			ShareLanding( result.ImpactSpeed );
		}
		if ( State.IsBailed && !wasBailed )
		{
			ShareRelease();
			ReleaseDisplay();
			Bailed?.Invoke();
		}
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
		if ( wasPlanting && !State.HandPlanting && !State.IsBailed )
		{
			// The hand plant let go: physics turned the board nose down into the ramp in one step. Shown (and so the rider
			// riding it), it turns there from where it was shown over ShownEase.PlantLetGo, falling as physics has it.
			_smooth.Reset();
			_smooth.Record( WorldTransform, Time.Now, Time.Delta );
			_smooth.EaseFrom( shownBefore, WorldTransform, Time.Now, Core.Animation.ShownEase.PlantLetGo );
		}
		else
		{
			_smooth.Record( WorldTransform, Time.Now, Time.Delta );
		}
		if ( _adapter is not null && Body.IsValid() && Simulating )
			_visuals?.RecordStep( _adapter.Bodies, Time.Now, Time.Delta );
	}

	private void PublishState()
	{
		NetSpeed = State.Speed;
		NetWheels = State.GroundedWheels;
		NetLandingSpeed = State.LandingSpeed;
		NetCrouch = State.Crouch;
		NetPowerSlide = State.PowerSlide;
		NetBodyFlip = State.BodyFlip;
		NetBalance = State.Balance;
		NetGrind = State.Grind;
		NetNamedGrind = State.NamedGrind;
		NetStateFlags = NetBoard.StateFlags( State );
		NetSteer = _input.Steer;
		NetPush = _input.Push;
		NetBrake = _input.Brake;
		NetFlickX = _input.FlickX;
		NetFlickY = _input.FlickY;
		NetLean = _input.Lean;
		NetGrab = _input.Grab;
		NetPushFeet = _input.PushFeet;
		NetFeetOff = _input.FeetOff;
		NetInputFlags = NetBoard.InputFlags( _input );
	}

	// The owner's tricks and landings, raised again on every other player's copy of the board, which does not simulate
	// them: its rider plays them there as it does on the owner's screen.
	[Rpc.Broadcast]
	private void ShareTrick( SkateTrick trick, bool fromNose )
	{
		if ( !IsProxy )
			return;
		LastTrickFromNose = fromNose;
		TrickStarted?.Invoke( trick );
	}

	[Rpc.Broadcast]
	private void ShareLanding( float speed )
	{
		if ( IsProxy )
			Landed?.Invoke( speed );
	}

	// Clients never simulate: they forward their own input and read the host's state.
	private void UpdateProxy()
	{
		if ( _forwardInput )
			ReceiveInput( _input.Steer, _input.Push, _input.Brake, _input.FlickX, _input.FlickY, _input.Lean,
				_input.Slide, _input.Goofy, _input.Pump, _input.Grab, _input.Gesture, _input.PushFeet, _input.NewPush, _input.HippyJump,
				_input.Catch, _input.FeetOff, _input.HandPlant );
		State = NetBoard.State( NetSpeed, NetWheels, NetLandingSpeed, NetCrouch, NetPowerSlide, NetBodyFlip, NetBalance, NetGrind,
			NetNamedGrind, NetStateFlags );
		_sharedInput = NetBoard.Input( NetSteer, NetPush, NetBrake, NetFlickX, NetFlickY, NetLean, NetGrab, NetPushFeet, NetFeetOff,
			NetInputFlags );
	}
}
