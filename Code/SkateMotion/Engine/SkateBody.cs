using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;
using SkateMotion.Resources;
using SkateMotion.Engine.Collision;

namespace SkateMotion.Engine;

internal sealed class SkateBody
{
	private readonly Rigidbody _body;
	private readonly BodyConfiguration _configuration;
	private readonly ArticulatedBoard _assembly;
	private readonly BoardPilot _pilot;
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

	internal SkateBody( Rigidbody body, SkateProfile profile, BoardRig rig )
	{
		_body = body;
		_configuration = new( body );
		Settings = SkateProfiles.Read( profile );
		WheelRadius = profile?.WheelRadius ?? 0.0285f;
		Span<System.Numerics.Vector3> centers = stackalloc System.Numerics.Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			centers[ i ] = SkateUnits.Metres( rig.Origins[ i ] )
				- System.Numerics.Vector3.UnitZ * (Settings.RideHeight - WheelRadius);
		_assembly = new( Settings, centers, WheelRadius );
		_pilot = new( Settings );
		_map = new( body, Settings, WheelRadius );
		_generatedMap = new( body, Settings, WheelRadius, new CollisionMapContactContext
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
			PushContact: PushContact ).Sanitized();
		var steps = Math.Max( 1, (int)MathF.Ceiling( step * 240f ) );
		var delta = step / steps;
		var result = default( PhysicsStep );
		var trick = TrickKind.None;
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
			var contacts = _map.Sample( _assembly, delta, !_bailed && (Settings.Recovered ? _pilot.PowerSliding : controls.Slide), rails );
			if ( _recoverInitialOverlap || _map.HasDeepPenetration )
			{
				_recoverInitialOverlap = false;
				if ( _assembly.RecoverInitialOverlap( contacts ) )
					contacts = _map.Sample( _assembly, delta, !_bailed && (Settings.Recovered ? _pilot.PowerSliding : controls.Slide), rails );
			}
			var incoming = _assembly.Deck;
			result = _pilot.Apply( _assembly, contacts, controls, delta, _map.Grind, wall );
			// Measured handling: a rider in the air near a rail is carried onto it.
			if ( Settings.Recovered && _pilot.Ridden && result.Mode == MotionMode.Airborne && !rails.IsEmpty )
			{
				var flying = _assembly.Deck;
				var lowest = Math.Min( _assembly.Body( 1 ).Position.Z, _assembly.Body( 2 ).Position.Z );
				var change = GrindAssist.VelocityChange( flying.Position, lowest, flying.Velocity, rails, Settings.Gravity, delta,
					HandlingCurves.GrindReach( Settings.Difficulty ) );
				if ( change != System.Numerics.Vector3.Zero )
				{
					for ( var part = 0; part < ArticulatedBoard.DynamicCount; part++ )
						_assembly.Body( part ).Velocity += change;
				}
			}
			if ( ExternalForce != System.Numerics.Vector3.Zero )
				_assembly.AddForce( 0, ExternalForce, _assembly.Deck.Position );
			var released = result.Mode == MotionMode.Bailed && !_bailed;
			if ( released )
				CaptureRelease( incoming, false );
			var steering = _bailed ? 0f : Settings.Recovered
				? _steering.Angle( Settings, controls.Steer, Speed, controls.Push > 0f,
					result.Mode is MotionMode.Manual or MotionMode.NoseManual, delta )
				: Settings.SteeringAngle( controls.Steer, Speed );
			_generatedMap.Step( _assembly, _map, !_bailed && (Settings.Recovered ? _pilot.PowerSliding : controls.Slide), steering, delta );
			_map.ApplyReactions( _assembly, delta );
			if ( released )
				ReleaseTelemetry = CaptureTelemetry();
			if ( result.Trick != TrickKind.None )
				trick = result.Trick;
			landed |= result.Landed;
			_grindTime = result.Grind != GrindKind.None ? _grindTime + delta : 0f;
			runout |= result.Runout;
			impact = Math.Max( impact, result.ImpactSpeed );
			collision = Math.Max( collision, result.CollisionSpeed );
		}
		var deck = _assembly.Deck;
		_body.WorldPosition = SkateUnits.Inches( deck.Position );
		_body.WorldRotation = SkateUnits.ToEngine( deck.Rotation );
		_body.Velocity = SkateUnits.Inches( deck.Velocity );
		_body.AngularVelocity = SkateUnits.ToEngine( deck.AngularVelocity );
		ExternalForce = System.Numerics.Vector3.Zero;
		return result with { Trick = trick, Landed = landed, ImpactSpeed = impact, CollisionSpeed = collision, Runout = runout,
			PowerSlide = result.PowerSlide, Reverting = result.Reverting, BodyFlip = result.BodyFlip,
			WallRiding = result.WallRiding, Crouch = result.Crouch };
	}

	/// <summary>Why the board last bailed (see <see cref="BoardPilot.BailReason"/>).</summary>
	internal string BailReason => _pilot.BailReason;

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

	// How far ahead (seconds of travel) a wall is looked for.
	private const float WallLook = 0.5f;

	internal void Reset()
	{
		ReleaseTelemetry = null;
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
		if ( attached ) _bailed = false;
		_mass = attached ? Settings.Mass : Settings.BoardMass;
		_assembly.SetRiderAttached( attached );
		_pilot.SetRiderAttached( attached );
		_configuration.Apply( _mass );
	}
}
