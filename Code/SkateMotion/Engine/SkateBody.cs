using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using SkateMotion.Engine.Collision;

namespace SkateMotion.Engine;

internal sealed class SkateBody
{
	private readonly Rigidbody _body;
	private readonly BodyConfiguration _configuration;
	private readonly ArticulatedBoard _assembly;
	private readonly BoardPilot _pilot;
	private readonly GrindAirAssist _airAssist = new();
	private readonly SteeringResponse _steering = new();
	private float _grindTime;
	private readonly ArticulatedContacts _map;
	private readonly MapBoardContacts _generatedMap;
	private float _mass;
	private bool _recoverInitialOverlap;
	internal SkateSettings Settings { get; }
	internal float WheelRadius { get; }
	internal ReadOnlySpan<DynamicBody> Bodies => _assembly.Bodies;
	internal float Speed => _assembly.Body( 0 ).Velocity.Length();

	/// <summary>True when the rail being ground was on the deck's right as the board came onto it.</summary>
	internal bool GrindRailOnRight { get; private set; }

	/// <summary>True when, during a grind, the deck's nose lies across the rail on the side the deck's right faced as
	/// the board came onto it (it stays as it was while the nose is too close to the rail's line to tell).</summary>
	internal bool GrindNoseRight { get; private set; }

	// The nearest rail's last clear offset across the deck (metres, positive on its right) before a grind, and whether
	// the board was grinding at the last substep.
	private float _railSide;
	private bool _grinding;

	// Seconds off the rail, and how long a grind may lose its rail and still be the same grind.
	private float _offRail = 1f;
	private const float RailLossHold = 0.15f;

	// The deck's right before the grind (flat, metres), and how far across the rail's line the nose must be to say
	// which side it is on.
	private System.Numerics.Vector3 _approachRight;
	private const float NoseSideClear = 0.02f;

	/// <summary>True while the board rolls tail first faster than a walking pace.</summary>
	internal bool Fakie
	{
		get
		{
			var deck = _assembly.Deck;
			return System.Numerics.Vector3.Dot( deck.Velocity, deck.Forward ) < -FakieSpeed;
		}
	}

	// Speed backwards (m/s) above which the board counts as riding fakie.
	private const float FakieSpeed = 0.5f;
	internal Vector3 ReleaseVelocity { get; private set; }
	internal Vector3 ReleaseAngularVelocity { get; private set; }
	internal Vector3 ReleasePosition { get; private set; }
	internal SkateTelemetry ReleaseTelemetry { get; private set; }
	private bool _bailed;

	internal SkateTelemetry CaptureTelemetry()
	{
		var states = new SkateBodyState[ ArticulatedBoard.DynamicCount ];
		for ( var i = 0; i < states.Length; i++ )
		{
			ref var body = ref _assembly.Body( i );
			states[ i ] = new( (SkateBodyPart)i, SkateUnits.ToEngine( body.Position ),
				SkateUnits.ToEngine( body.Rotation ), SkateUnits.ToEngine( body.Velocity ),
				SkateUnits.ToEngine( body.AngularVelocity ) );
		}
		var contacts = _generatedMap.CaptureContacts( _map );
		return new( states, contacts, _assembly.MaximumAnchorError(), _assembly.KineticEnergy() );
	}

	internal SkateBody( Rigidbody body, BoardRig rig )
	{
		_body = body;
		_configuration = new( body );
		// The rig holds the profile's settings, sized to the board model where one was measured.
		Settings = rig.Settings;
		WheelRadius = rig.WheelRadius;
		Span<System.Numerics.Vector3> centers = stackalloc System.Numerics.Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			centers[ i ] = SkateUnits.Metres( rig.Origins[ i ] )
				- System.Numerics.Vector3.UnitZ * (Settings.RideHeight - WheelRadius);
		_assembly = new( Settings, centers, WheelRadius );
		_pilot = new( Settings );
		// The wheel sweeps and the contact generation share the board's read collision shapes.
		var shapes = new MapShapeCache();
		_map = new( body, Settings, WheelRadius, shapes );
		_generatedMap = new( body, Settings, WheelRadius, shapes, new CollisionMapContactContext
		{
			MaximumExtension = 0x3f000000u,
			TriangleContext = new( 0x3f7fbe77u, 0x3c23d70au, false )
		}, 0xbf800000u, true );
		_mass = Settings.Mass;
		Enable();
	}

	/// <summary>
	/// Whether the rider's pushing foot is on the ground, as the animation shows it, or null without an animation
	/// (pushes then follow a fixed stroke).
	/// </summary>
	internal bool? PushContact { get; set; }

	/// <summary>The rider's animation holds the board dark side up (the skateboard's DarkSideUp).</summary>
	internal bool DarkSideUp { get; set; }

	/// <summary>True while an animation shows the board's tricks; see <see cref="BoardPilot.AnimatedTricks"/>.</summary>
	internal bool AnimatedTricks
	{
		get => _pilot.AnimatedTricks;
		set => _pilot.AnimatedTricks = value;
	}

	/// <summary>Force in newtons applied to the deck during the next step only (towing, pushes).</summary>
	internal System.Numerics.Vector3 ExternalForce { get; set; }

	internal PhysicsStep Step( SkateInput input, float step, ReadOnlySpan<RailSegment> rails )
	{
		if ( !float.IsFinite( step ) || step <= 0f || step > 0.1f )
			throw new ArgumentOutOfRangeException( nameof( step ) );
		var controls = new ControlInput( input.Steer, input.Push, input.Brake,
			input.FlickX, input.FlickY, input.Lean, input.Slide, Grab: input.Grab != SkateAirGrab.None, Goofy: input.Goofy, Pump: input.Pump,
			PushContact: PushContact ) { HandPlant = input.HandPlant, DarkSideUp = DarkSideUp }.Sanitized();
		var steps = Math.Max( 1, (int)MathF.Ceiling( step * 240f ) );
		var delta = step / steps;
		var result = default( PhysicsStep );
		var trick = TrickKind.None;
		var nose = false;
		var landed = false;
		var runout = false;
		var impact = 0f;
		var collision = 0f;
		var wall = Settings.Recovered && _pilot.Ridden ? WallAhead() : null;
		for ( var i = 0; i < steps; i++ )
		{
			_assembly.BeginStep();
			var grindShare = Settings.Recovered ? HandlingCurves.GrindFrictionShare( _grindTime ) : 1f;
			_map.GrindFrictionScale = grindShare;
			_generatedMap.GrindFrictionScale = grindShare;
			// Wheels slide sideways in a powerslide and as a kick turn swings the board round.
			var sliding = Sliding();
			var contacts = _map.Sample( _assembly, delta, !_bailed && (Settings.Recovered ? _pilot.PowerSliding || _pilot.KickTurning : controls.Slide), rails, sliding );
			if ( _recoverInitialOverlap || _map.HasDeepPenetration )
			{
				_recoverInitialOverlap = false;
				if ( _assembly.RecoverInitialOverlap( contacts ) )
					contacts = _map.Sample( _assembly, delta, !_bailed && (Settings.Recovered ? _pilot.PowerSliding || _pilot.KickTurning : controls.Slide), rails, sliding );
			}
			var incoming = _assembly.Deck;
			// Coming down on a rail, a board turned across it in the air lines up across it: a slide.
			var landingRail = Settings.Recovered && _pilot.Ridden && !rails.IsEmpty
				? _airAssist.Landing( incoming.Position, Math.Min( _assembly.Body( 1 ).Position.Z, _assembly.Body( 2 ).Position.Z ),
					incoming.Velocity, rails, Settings.Gravity, HandlingCurves.GrindNudge( Settings.Difficulty ) )
				: null;
			result = _pilot.Apply( _assembly, contacts, controls, delta, _map.Grind, wall, landingRail );
			_slideLanding = landingRail is { } landing
				&& MathF.Abs( System.Numerics.Vector3.Dot( _assembly.Deck.Forward, landing ) ) < RailContacts.SlideAlong;
			// Measured handling: a rider in the air near a rail is carried onto it. Coming down just beside one, the board
			// is moved the last few centimetres onto its line and stops drifting across it; farther out the lock-on
			// steers its flight toward the rail.
			if ( Settings.Recovered && _pilot.Ridden && result.Mode == MotionMode.Airborne && !rails.IsEmpty )
			{
				var flying = _assembly.Deck;
				var lowest = Math.Min( _assembly.Body( 1 ).Position.Z, _assembly.Body( 2 ).Position.Z );
				var (move, stop) = _airAssist.Move( flying.Position, lowest, flying.Velocity, rails, Settings.Gravity, delta,
					HandlingCurves.GrindNudge( Settings.Difficulty ), flying.Forward, flying.AngularVelocity.Z, GrindControl.Reach( _assembly ) );
				// The move coming down short of the rail by itself (the jump's whole move used up before the trucks come
				// down), the lock-on steers across the rest alongside it, so the two add up: run one after the other, the
				// steering began too late for its bend limit and a fakie came down 4 in off the rail on its wheels.
				var alongside = move != System.Numerics.Vector3.Zero && _airAssist.Short > 0f && !_slideLanding;
				var steer = alongside ? GrindAssist.VelocityChange( flying.Position, lowest, flying.Velocity, rails, Settings.Gravity,
					delta, HandlingCurves.GrindReach( Settings.Difficulty ), _airAssist.Covering ) : System.Numerics.Vector3.Zero;
				var change = move != System.Numerics.Vector3.Zero ? (steer != System.Numerics.Vector3.Zero ? steer : stop)
					: GrindAssist.VelocityChange( flying.Position, lowest, flying.Velocity, rails, Settings.Gravity, delta,
						HandlingCurves.GrindReach( Settings.Difficulty ) );
				for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
				{
					_assembly.Body( part ).Velocity += change;
					_assembly.Body( part ).Position += move;
				}
			}
			else if ( result.Mode != MotionMode.Airborne )
			{
				_airAssist.Reset();
			}
			if ( ExternalForce != System.Numerics.Vector3.Zero )
				_assembly.AddForce( 0, ExternalForce, _assembly.Deck.Position );
			var released = result.Mode == MotionMode.Bailed && !_bailed;
			if ( released )
				CaptureRelease( incoming, false );
			// On a rail the trucks are locked on it and do not steer: steering twists the board about its grinding truck
			// instead, and a truck steered on top of that turns its wheels into the rail's side.
			var steering = _bailed || result.Grind != GrindKind.None ? 0f : Settings.Recovered
				? _steering.Angle( Settings, controls.Steer, Speed, controls.Push > 0f,
					result.Mode is MotionMode.Manual or MotionMode.NoseManual, delta )
				: Settings.SteeringAngle( controls.Steer, Speed );
			_generatedMap.Step( _assembly, _map, !_bailed && (Settings.Recovered ? _pilot.PowerSliding || _pilot.KickTurning : controls.Slide), steering, delta,
				sliding );
			_map.ApplyReactions( _assembly, delta );
			if ( released )
				ReleaseTelemetry = CaptureTelemetry();
			if ( result.Trick != TrickKind.None )
			{
				trick = result.Trick;
				nose = result.TrickNose;
			}
			landed |= result.Landed;
			_grindTime = result.Grind != GrindKind.None ? _grindTime + delta : 0f;
			// A rail lost for a moment (a truck bouncing on it) is still the same grind: its sides stay as they were.
			_offRail = result.Grind != GrindKind.None ? 0f : _offRail + delta;
			TrackRailSide( rails, _offRail < RailLossHold );
			TrackNoseSide( result );
			runout |= result.Runout;
			impact = Math.Max( impact, result.ImpactSpeed );
			// The deck meeting the rail it comes down across or slides along is the slide itself, not a knock: counted, it
			// played the rider's bump over the slide's first half second.
			if ( !(_map.Grind.Active || _slideLanding || result.Grind != GrindKind.None) )
				collision = Math.Max( collision, result.CollisionSpeed );
		}
		var deck = _assembly.Deck;
		_body.WorldPosition = SkateUnits.Inches( deck.Position );
		_body.WorldRotation = SkateUnits.ToEngine( deck.Rotation );
		_body.Velocity = SkateUnits.Inches( deck.Velocity );
		_body.AngularVelocity = SkateUnits.ToEngine( deck.AngularVelocity );
		ExternalForce = System.Numerics.Vector3.Zero;
		return result with { Trick = trick, TrickNose = nose, Landed = landed, ImpactSpeed = impact, CollisionSpeed = collision, Runout = runout,
			PowerSlide = result.PowerSlide, Reverting = result.Reverting, BodyFlip = result.BodyFlip,
			WallRiding = result.WallRiding, Crouch = result.Crouch };
	}

	// Sliding a rail on the deck, or coming down across one to slide it: then the rail contact alone holds the board on
	// the rail, the board's own collision with it left out. Its trucks' collision runs along the deck, and the rail's
	// sides face the deck's underside, so either could catch the rail under a deck's end and throw the board off it.
	private bool Sliding() => _map.Grind.Kind is GrindKind.Boardslide or GrindKind.Noseslide or GrindKind.Tailslide
		|| _slideLanding;

	// The board is coming down across the rail it will land on.
	private bool _slideLanding;

	/// <summary>Why the board last bailed (see <see cref="BoardPilot.BailReason"/>).</summary>
	internal string BailReason => _pilot.BailReason;

	/// <summary>Rights a board lying upside down, raised onto its wheels (see <see cref="BoardPilot.TurnUpright"/>).</summary>
	internal void TurnUpright()
	{
		BoardPilot.TurnUpright( _assembly, Settings.RideHeight );
		var deck = _assembly.Deck;
		_body.WorldPosition = SkateUnits.Inches( deck.Position );
		_body.WorldRotation = SkateUnits.ToEngine( deck.Rotation );
		_body.Velocity = Vector3.Zero;
		_body.AngularVelocity = Vector3.Zero;
	}

	/// <summary>The lip of the last hand plant (see <see cref="BoardPilot.HandPlantLip"/>).</summary>
	internal System.Numerics.Vector3 HandPlantLip => _pilot.HandPlantLip;

	/// <summary>Level direction out of the wall under the hand plant's lip.</summary>
	internal System.Numerics.Vector3 HandPlantOutward => _pilot.HandPlantOutward;

	internal void Bail()
	{
		if ( _bailed ) return;
		CaptureRelease( _assembly.Deck );
		_pilot.Bail();
	}

	private void CaptureRelease( BoardBody incoming, bool captureTelemetry = true )
	{
		ReleaseTelemetry = captureTelemetry ? CaptureTelemetry() : null;
		_bailed = true;
		ReleaseVelocity = SkateUnits.Inches( incoming.Velocity );
		ReleaseAngularVelocity = SkateUnits.ToEngine( incoming.AngularVelocity );
		ReleasePosition = SkateUnits.Inches( incoming.Position );
		_mass = Settings.BoardMass;
		_assembly.SetRiderAttached( false );
		_configuration.Apply( _mass );
		// A board held dark side up goes on upside down, as it was shown.
		if ( DarkSideUp )
			BoardPilot.FlipOver( _assembly );
	}

	// A steep surface the board's travel meets within WallLook seconds, and how high above the floor below it that is.
	private WallSight? WallAhead()
	{
		var deck = _assembly.Deck;
		var velocity = SkateUnits.ToEngine( deck.Velocity );
		var speed = velocity.Length;
		if ( speed < 1f )
			return null;
		var start = SkateUnits.Inches( deck.Position );
		var end = start + velocity * (WallLook * SkateUnits.InchesPerMetre);
		var hit = _body.Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( _body.GameObject ).Run();
		if ( !hit.Hit || MathF.Abs( hit.Normal.z ) >= 0.5f )
			return null;
		var front = hit.HitPosition + hit.Normal * 12f;
		var floor = _body.Scene.Trace.Ray( front, front + Vector3.Down * 400f ).IgnoreGameObjectHierarchy( _body.GameObject ).Run();
		var height = floor.Hit ? (hit.HitPosition.z - floor.HitPosition.z) / SkateUnits.InchesPerMetre : 10f;
		return new WallSight( SkateUnits.Metres( hit.HitPosition ), SkateUnits.ToCore( hit.Normal ), height );
	}

	// Off a rail, which side of the deck the nearest one is on (kept while it is too close to tell); on starting a
	// grind, that side, or with none seen the way the board was moving across.
	private void TrackRailSide( ReadOnlySpan<RailSegment> rails, bool grinding )
	{
		var deck = _assembly.Deck;
		var right = -deck.Left;
		if ( !grinding )
		{
			_approachRight = new System.Numerics.Vector3( right.X, right.Y, 0f );
			var nearest = float.MaxValue;
			foreach ( var rail in rails )
			{
				if ( !rail.IsValid )
					continue;
				var offset = rail.Closest( deck.Position, out _ ) - deck.Position;
				var flat = new System.Numerics.Vector3( offset.X, offset.Y, 0f );
				if ( flat.LengthSquared() >= nearest )
					continue;
				nearest = flat.LengthSquared();
				var across = System.Numerics.Vector3.Dot( flat, right );
				if ( MathF.Abs( across ) > RailSideClear )
					_railSide = across;
			}
		}
		else if ( !_grinding )
		{
			GrindRailOnRight = _railSide != 0f ? _railSide > 0f : System.Numerics.Vector3.Dot( deck.Velocity, right ) > 0f;
			// Until the nose is clearly to one side, it is taken to be on the side the board came from.
			GrindNoseRight = !GrindRailOnRight;
		}
		_grinding = grinding;
	}

	// While grinding, which side of the rail the nose is on, measured against the deck's right before the grind.
	private void TrackNoseSide( in PhysicsStep result )
	{
		if ( result.Grind == GrindKind.None )
			return;
		var across = System.Numerics.Vector3.Dot( result.GrindShape.NoseOffset, _approachRight );
		if ( MathF.Abs( across ) > NoseSideClear )
			GrindNoseRight = across > 0f;
	}

	// Metres across the deck a rail must be to count as on one side.
	private const float RailSideClear = 0.02f;

	// How far ahead (seconds of travel) a wall is looked for.
	private const float WallLook = 0.5f;

	internal void Reset()
	{
		ReleaseTelemetry = null;
		_airAssist.Reset();
		_bailed = false;
		_pilot.Reset();
		_assembly.Reset( SkateUnits.Metres( _body.WorldPosition ), SkateUnits.ToCore( _body.WorldRotation ),
			SkateUnits.Metres( _body.Velocity ), SkateUnits.ToCore( _body.AngularVelocity ) );
		_map.Reset( _assembly.Deck.Up );
		_generatedMap.Refresh();
		_recoverInitialOverlap = true;
	}

	internal void Enable()
	{
		Reset();
		_configuration.Apply( _mass );
		_body.MotionEnabled = false;
	}

	internal void Disable() => _configuration.Restore();

	internal void SetRiderAttached( bool attached )
	{
		// A rider stepping off a board held dark side up (a dark landing run out) leaves it upside down, as it was shown;
		// a bail has already let it go so.
		if ( !attached && !_bailed && DarkSideUp )
		{
			BoardPilot.FlipOver( _assembly );
			_body.WorldRotation = SkateUnits.ToEngine( _assembly.Deck.Rotation );
		}
		if ( attached ) _bailed = false;
		_mass = attached ? Settings.Mass : Settings.BoardMass;
		_assembly.SetRiderAttached( attached );
		_pilot.SetRiderAttached( attached );
		_configuration.Apply( _mass );
	}
}
